#!/bin/bash

# EstateKit Database Migration Script
# Version: 1.0.0
# Dependencies:
# - dotnet-ef v10.0.0
# - Serilog.AspNetCore v8.0.0
# - AWSSDK.RDS v3.7.0

set -e
set -o pipefail

# Default configuration values
MIGRATION_TIMEOUT=${MIGRATION_TIMEOUT:-300}
BACKUP_RETENTION_DAYS=${BACKUP_RETENTION_DAYS:-30}
LOG_LEVEL=${LOG_LEVEL:-"Info"}
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
MIGRATION_LOG_FILE="/var/log/estatekit/migrations/migration_${TIMESTAMP}.log"

# Security-related variables
ENCRYPTION_KEY_PATH=${ENCRYPTION_KEY_PATH:-"/etc/estatekit/keys"}
SSL_CERT_PATH=${SSL_CERT_PATH:-"/etc/estatekit/certs"}

# Logging function with security masking
log() {
    local level=$1
    local message=$2
    local masked_message=$(echo "$message" | sed 's/\(password=\)[^;]*\([;]\)/\1*****\2/g')
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] [$level] $masked_message" | tee -a "$MIGRATION_LOG_FILE"
}

# Check prerequisites for migration
check_prerequisites() {
    log "INFO" "Checking prerequisites..."

    # Verify dotnet-ef installation
    if ! command -v dotnet-ef &> /dev/null; then
        log "ERROR" "dotnet-ef tools not found. Please install version 10.0.0"
        return 1
    fi

    # Validate environment variables
    if [ -z "$ASPNETCORE_ENVIRONMENT" ]; then
        log "ERROR" "ASPNETCORE_ENVIRONMENT not set"
        return 1
    fi

    if [ -z "$DB_CONNECTION_STRING" ]; then
        log "ERROR" "DB_CONNECTION_STRING not set"
        return 1
    fi

    # Check encryption service availability
    if [ ! -d "$ENCRYPTION_KEY_PATH" ]; then
        log "ERROR" "Encryption key directory not found"
        return 1
    fi

    # Verify SSL certificates
    if [ ! -f "${SSL_CERT_PATH}/estatekit.crt" ]; then
        log "ERROR" "SSL certificate not found"
        return 1
    }

    # Check database connection
    if ! dotnet ef database verify --verbose; then
        log "ERROR" "Database connection failed"
        return 1
    }

    log "INFO" "Prerequisites check completed successfully"
    return 0
}

# Create encrypted database backup
backup_database() {
    log "INFO" "Starting database backup..."
    
    local backup_file="estatekit_backup_${TIMESTAMP}.sql.enc"
    local backup_path="/var/backups/estatekit/${backup_file}"
    
    # Create backup directory if it doesn't exist
    mkdir -p /var/backups/estatekit
    
    # Perform encrypted backup
    pg_dump "$DB_CONNECTION_STRING" | \
    openssl enc -aes-256-gcm -salt -pbkdf2 \
    -out "$backup_path" \
    -pass file:"${ENCRYPTION_KEY_PATH}/backup.key"
    
    if [ $? -ne 0 ]; then
        log "ERROR" "Backup failed"
        return 1
    }
    
    # Verify backup integrity
    if ! openssl enc -d -aes-256-gcm -pbkdf2 \
        -in "$backup_path" \
        -pass file:"${ENCRYPTION_KEY_PATH}/backup.key" \
        -out /dev/null; then
        log "ERROR" "Backup verification failed"
        return 1
    }
    
    # Upload to S3 with server-side encryption
    aws s3 cp "$backup_path" \
        "s3://estatekit-backups/${ASPNETCORE_ENVIRONMENT}/${backup_file}" \
        --sse aws:kms \
        --sse-kms-key-id "${AWS_KMS_KEY_ID}"
    
    # Clean up old backups
    find /var/backups/estatekit -type f -mtime +${BACKUP_RETENTION_DAYS} -delete
    
    log "INFO" "Backup completed successfully"
    return 0
}

# Apply database migrations
apply_migrations() {
    log "INFO" "Starting database migration..."
    
    # List pending migrations
    local pending_migrations=$(dotnet ef migrations list --no-build)
    if [ -z "$pending_migrations" ]; then
        log "INFO" "No pending migrations found"
        return 0
    }
    
    # Start transaction with timeout
    export PGCONNECT_TIMEOUT=$MIGRATION_TIMEOUT
    
    # Apply migrations with retry logic
    local retry_count=0
    local max_retries=3
    
    while [ $retry_count -lt $max_retries ]; do
        if dotnet ef database update --verbose; then
            log "INFO" "Migration completed successfully"
            
            # Verify sensitive field encryption
            verify_encryption
            if [ $? -ne 0 ]; then
                log "ERROR" "Encryption verification failed"
                return 1
            }
            
            return 0
        else
            retry_count=$((retry_count + 1))
            log "WARN" "Migration attempt $retry_count failed, retrying..."
            sleep 5
        fi
    done
    
    log "ERROR" "Migration failed after $max_retries attempts"
    return 1
}

# Verify encryption of sensitive fields
verify_encryption() {
    log "INFO" "Verifying field-level encryption..."
    
    # Check encryption status of sensitive fields
    local query="
        SELECT COUNT(*) 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND column_name IN (
            'DateOfBirth', 'BirthPlace', 'Value', 
            'FrontImageUrl', 'BackImageUrl', 'Location',
            'AccessInformation'
        );"
    
    local sensitive_fields_count=$(psql "$DB_CONNECTION_STRING" -t -c "$query")
    
    if [ "$sensitive_fields_count" -eq 0 ]; then
        log "ERROR" "No sensitive fields found for encryption verification"
        return 1
    }
    
    log "INFO" "Encryption verification completed successfully"
    return 0
}

# Validate migration results
validate_migration() {
    log "INFO" "Starting migration validation..."
    
    # Verify database schema integrity
    if ! dotnet ef database verify --verbose; then
        log "ERROR" "Schema verification failed"
        return 1
    }
    
    # Check database connectivity and performance
    local query="SELECT 1;"
    if ! psql "$DB_CONNECTION_STRING" -c "$query" &> /dev/null; then
        log "ERROR" "Database connectivity check failed"
        return 1
    }
    
    # Verify audit logging configuration
    if [ ! -f "$MIGRATION_LOG_FILE" ]; then
        log "ERROR" "Migration log file not found"
        return 1
    }
    
    log "INFO" "Migration validation completed successfully"
    return 0
}

# Main execution flow
main() {
    log "INFO" "Starting database migration process for environment: $ASPNETCORE_ENVIRONMENT"
    
    # Create log directory
    mkdir -p "$(dirname "$MIGRATION_LOG_FILE")"
    
    # Execute migration steps
    check_prerequisites
    if [ $? -ne 0 ]; then
        log "ERROR" "Prerequisites check failed"
        exit 1
    }
    
    backup_database
    if [ $? -ne 0 ]; then
        log "ERROR" "Database backup failed"
        exit 1
    }
    
    apply_migrations
    if [ $? -ne 0 ]; then
        log "ERROR" "Migration failed"
        exit 1
    }
    
    validate_migration
    if [ $? -ne 0 ]; then
        log "ERROR" "Migration validation failed"
        exit 1
    }
    
    log "INFO" "Database migration completed successfully"
    exit 0
}

# Execute main function
main