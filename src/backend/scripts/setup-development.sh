#!/bin/bash

# EstateKit Personal Information API Development Environment Setup
# Version: 1.0.0
# Dependencies:
# - docker-compose v3.8
# - .NET SDK v9.0
# - openssl v3.0

set -euo pipefail
trap cleanup EXIT

# Source required scripts
source ./migrate-database.sh
source ./run-tests.sh

# Global configuration
readonly SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
readonly LOG_FILE="${SCRIPT_DIR}/setup.log"
readonly REQUIRED_DISK_SPACE=10737418240  # 10GB in bytes
readonly MIN_DOCKER_VERSION="20.10.0"
readonly MIN_DOTNET_VERSION="9.0.0"
readonly HEALTH_CHECK_TIMEOUT=300
readonly CLEANUP_ON_FAILURE=true

# Logging utilities
log() {
    local level=$1
    local message=$2
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] [$level] $message" | tee -a "$LOG_FILE"
}

# Check prerequisites for development environment
check_prerequisites() {
    log "INFO" "Checking prerequisites..."

    # Verify Docker installation
    if ! command -v docker &> /dev/null; then
        log "ERROR" "Docker not found. Please install Docker version ${MIN_DOCKER_VERSION} or later"
        return 1
    fi

    # Check Docker version
    local docker_version=$(docker version --format '{{.Server.Version}}' 2>/dev/null)
    if ! [[ "$(printf '%s\n' "$MIN_DOCKER_VERSION" "$docker_version" | sort -V | head -n1)" = "$MIN_DOCKER_VERSION" ]]; then
        log "ERROR" "Docker version ${MIN_DOCKER_VERSION} or higher required. Found: $docker_version"
        return 1
    }

    # Verify .NET SDK
    if ! command -v dotnet &> /dev/null; then
        log "ERROR" ".NET SDK not found. Please install .NET SDK ${MIN_DOTNET_VERSION}"
        return 1
    }

    # Check .NET version
    local dotnet_version=$(dotnet --version)
    if ! [[ "$dotnet_version" =~ ^${MIN_DOTNET_VERSION} ]]; then
        log "ERROR" ".NET SDK version ${MIN_DOTNET_VERSION} required. Found: $dotnet_version"
        return 1
    }

    # Check available disk space
    local available_space=$(df -B1 . | awk 'NR==2 {print $4}')
    if [ "$available_space" -lt "$REQUIRED_DISK_SPACE" ]; then
        log "ERROR" "Insufficient disk space. Required: 10GB, Available: $(numfmt --to=iec-i --suffix=B $available_space)"
        return 1
    }

    # Verify required ports availability
    local required_ports=(5432 6379 5000 5001)
    for port in "${required_ports[@]}"; do
        if netstat -tuln | grep -q ":$port "; then
            log "ERROR" "Port $port is already in use"
            return 1
        fi
    done

    # Check OpenSSL installation
    if ! command -v openssl &> /dev/null; then
        log "ERROR" "OpenSSL not found. Please install OpenSSL v3.0 or later"
        return 1
    }

    log "INFO" "Prerequisites check completed successfully"
    return 0
}

# Setup development environment
setup_environment() {
    log "INFO" "Setting up development environment..."

    # Generate secure database password
    DB_PASSWORD=$(openssl rand -base64 32)
    export DB_PASSWORD

    # Set environment variables
    export ASPNETCORE_ENVIRONMENT="Development"
    export DB_USER="app"

    # Create docker-compose override
    cat > ../docker-compose.override.yml <<EOF
version: '3.8'
services:
  business-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DB_USER=${DB_USER}
      - DB_PASSWORD=${DB_PASSWORD}
    volumes:
      - ${SCRIPT_DIR}/../certs:/app/certs:ro

  data-api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DB_USER=${DB_USER}
      - DB_PASSWORD=${DB_PASSWORD}
    volumes:
      - ${SCRIPT_DIR}/../certs:/app/certs:ro
EOF

    # Generate development certificates
    mkdir -p ../certs
    openssl req -x509 -nodes -days 365 -newkey rsa:4096 \
        -keyout ../certs/dev.key -out ../certs/dev.crt \
        -subj "/CN=localhost" \
        -addext "subjectAltName=DNS:localhost,DNS:business-api,DNS:data-api"

    chmod 600 ../certs/dev.key
    chmod 644 ../certs/dev.crt

    log "INFO" "Environment setup completed"
    return 0
}

# Start infrastructure services
start_infrastructure() {
    log "INFO" "Starting infrastructure services..."

    # Pull required images
    docker-compose pull

    # Start services
    docker-compose up -d postgres redis

    # Wait for PostgreSQL
    local retries=0
    while ! docker-compose exec -T postgres pg_isready -U ${DB_USER} > /dev/null 2>&1; do
        if [ $retries -eq $HEALTH_CHECK_TIMEOUT ]; then
            log "ERROR" "PostgreSQL failed to start within timeout period"
            return 1
        fi
        sleep 1
        ((retries++))
    done

    # Wait for Redis
    retries=0
    while ! docker-compose exec -T redis redis-cli ping > /dev/null 2>&1; do
        if [ $retries -eq $HEALTH_CHECK_TIMEOUT ]; then
            log "ERROR" "Redis failed to start within timeout period"
            return 1
        fi
        sleep 1
        ((retries++))
    done

    log "INFO" "Infrastructure services started successfully"
    return 0
}

# Setup database
setup_database() {
    log "INFO" "Setting up database..."

    # Apply migrations
    if ! apply_migrations; then
        log "ERROR" "Database migration failed"
        return 1
    fi

    # Verify schema
    if ! verify_schema; then
        log "ERROR" "Schema verification failed"
        return 1
    }

    log "INFO" "Database setup completed successfully"
    return 0
}

# Build services
build_services() {
    log "INFO" "Building services..."

    # Build Business API
    if ! dotnet build ../Business.API/Business.API.csproj \
        --configuration Debug \
        --no-restore; then
        log "ERROR" "Business API build failed"
        return 1
    fi

    # Build Data API
    if ! dotnet build ../Data.API/Data.API.csproj \
        --configuration Debug \
        --no-restore; then
        log "ERROR" "Data API build failed"
        return 1
    fi

    # Run unit tests
    if ! run_unit_tests; then
        log "ERROR" "Unit tests failed"
        return 1
    fi

    log "INFO" "Services built successfully"
    return 0
}

# Cleanup resources
cleanup() {
    if [ "${CLEANUP_ON_FAILURE}" = "true" ] && [ $? -ne 0 ]; then
        log "INFO" "Performing cleanup..."

        # Stop containers
        docker-compose down -v 2>/dev/null || true

        # Remove certificates
        rm -rf ../certs 2>/dev/null || true

        # Remove override file
        rm -f ../docker-compose.override.yml 2>/dev/null || true

        log "INFO" "Cleanup completed"
    fi
}

# Main execution
main() {
    log "INFO" "Starting development environment setup..."

    # Create log directory
    mkdir -p "$(dirname "$LOG_FILE")"

    # Execute setup steps
    check_prerequisites || exit 1
    setup_environment || exit 1
    start_infrastructure || exit 1
    setup_database || exit 1
    build_services || exit 1

    log "INFO" "Development environment setup completed successfully"
    exit 0
}

# Execute main function
main