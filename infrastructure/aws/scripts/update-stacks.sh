#!/bin/bash

# EstateKit Personal Information API System - Stack Update Script
# Version: 1.0.0
# AWS CLI Version: 2.x
# Description: Orchestrates CloudFormation stack updates with dependency management and rollback handling

set -euo pipefail

# Global variables
readonly STACK_PREFIX="estatekit"
readonly AWS_REGION="${AWS_REGION:-us-east-1}"
readonly ENVIRONMENT="${1:-staging}"
readonly UPDATE_TIMEOUT=3600
readonly WAIT_INTERVAL=30
readonly MAX_RETRIES=120

# Stack names
readonly NETWORKING_STACK="${STACK_PREFIX}-networking-${ENVIRONMENT}"
readonly SECURITY_STACK="${STACK_PREFIX}-security-${ENVIRONMENT}"
readonly DATABASE_STACK="${STACK_PREFIX}-database-${ENVIRONMENT}"
readonly BUSINESS_API_STACK="${STACK_PREFIX}-business-api-${ENVIRONMENT}"
readonly DATA_API_STACK="${STACK_PREFIX}-data-api-${ENVIRONMENT}"

# Template paths
readonly TEMPLATE_DIR="../cloudformation"
readonly NETWORKING_TEMPLATE="${TEMPLATE_DIR}/networking.yml"
readonly SECURITY_TEMPLATE="${TEMPLATE_DIR}/security.yml"
readonly DATABASE_TEMPLATE="${TEMPLATE_DIR}/database.yml"
readonly BUSINESS_API_TEMPLATE="${TEMPLATE_DIR}/business-api.yml"
readonly DATA_API_TEMPLATE="${TEMPLATE_DIR}/data-api.yml"

# Logging functions
log_info() {
    echo "[INFO] $(date '+%Y-%m-%d %H:%M:%S') - $1"
}

log_error() {
    echo "[ERROR] $(date '+%Y-%m-%d %H:%M:%S') - $1" >&2
}

log_warning() {
    echo "[WARNING] $(date '+%Y-%m-%d %H:%M:%S') - $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Verify AWS CLI installation and version
    if ! command -v aws &> /dev/null; then
        log_error "AWS CLI is not installed"
        exit 1
    fi

    # Check AWS credentials
    if ! aws sts get-caller-identity &> /dev/null; then
        log_error "Invalid AWS credentials"
        exit 1
    }

    # Verify template files exist
    local templates=("$NETWORKING_TEMPLATE" "$SECURITY_TEMPLATE" "$DATABASE_TEMPLATE" "$BUSINESS_API_TEMPLATE" "$DATA_API_TEMPLATE")
    for template in "${templates[@]}"; do
        if [[ ! -f "$template" ]]; then
            log_error "Template file not found: $template"
            exit 1
        fi
    done

    log_info "Prerequisites check passed"
}

# Validate CloudFormation template
validate_template() {
    local template_file=$1
    local template_name=$(basename "$template_file")
    
    log_info "Validating template: $template_name"
    
    if ! aws cloudformation validate-template \
        --template-body "file://${template_file}" \
        --region "$AWS_REGION" &> /dev/null; then
        log_error "Template validation failed: $template_name"
        return 1
    fi
    
    log_info "Template validation passed: $template_name"
    return 0
}

# Wait for stack update completion
wait_for_update() {
    local stack_name=$1
    local timeout=$2
    local start_time=$(date +%s)
    local status
    
    log_info "Waiting for stack update: $stack_name"
    
    while true; do
        status=$(aws cloudformation describe-stacks \
            --stack-name "$stack_name" \
            --region "$AWS_REGION" \
            --query 'Stacks[0].StackStatus' \
            --output text)
            
        case "$status" in
            UPDATE_COMPLETE)
                log_info "Stack update completed successfully: $stack_name"
                return 0
                ;;
            UPDATE_ROLLBACK_COMPLETE)
                log_error "Stack update rolled back: $stack_name"
                return 1
                ;;
            UPDATE_FAILED|UPDATE_ROLLBACK_FAILED)
                log_error "Stack update failed: $stack_name"
                return 1
                ;;
            UPDATE_IN_PROGRESS|UPDATE_ROLLBACK_IN_PROGRESS)
                local current_time=$(date +%s)
                local elapsed_time=$((current_time - start_time))
                
                if [ $elapsed_time -ge "$timeout" ]; then
                    log_error "Stack update timed out: $stack_name"
                    return 1
                fi
                
                sleep "$WAIT_INTERVAL"
                ;;
            *)
                log_error "Unknown stack status: $status"
                return 1
                ;;
        esac
    done
}

# Update stack with change set review
update_stack() {
    local stack_name=$1
    local template_file=$2
    local parameters=$3
    
    log_info "Creating change set for stack: $stack_name"
    
    # Create change set
    local change_set_name="${stack_name}-$(date +%s)"
    if ! aws cloudformation create-change-set \
        --stack-name "$stack_name" \
        --template-body "file://${template_file}" \
        --parameters "$parameters" \
        --capabilities CAPABILITY_NAMED_IAM \
        --change-set-name "$change_set_name" \
        --region "$AWS_REGION"; then
        log_error "Failed to create change set: $stack_name"
        return 1
    fi
    
    # Wait for change set creation
    aws cloudformation wait change-set-create-complete \
        --stack-name "$stack_name" \
        --change-set-name "$change_set_name" \
        --region "$AWS_REGION"
    
    # Execute change set
    log_info "Executing change set: $change_set_name"
    if ! aws cloudformation execute-change-set \
        --stack-name "$stack_name" \
        --change-set-name "$change_set_name" \
        --region "$AWS_REGION"; then
        log_error "Failed to execute change set: $change_set_name"
        return 1
    fi
    
    # Wait for stack update
    wait_for_update "$stack_name" "$UPDATE_TIMEOUT"
}

# Main update orchestration
main() {
    log_info "Starting stack update process for environment: $ENVIRONMENT"
    
    # Check prerequisites
    check_prerequisites
    
    # Update networking stack
    log_info "Updating networking stack..."
    if ! update_stack "$NETWORKING_STACK" "$NETWORKING_TEMPLATE" "[{\"ParameterKey\":\"EnvironmentName\",\"ParameterValue\":\"$ENVIRONMENT\"}]"; then
        log_error "Failed to update networking stack"
        exit 1
    fi
    
    # Update security stack
    log_info "Updating security stack..."
    if ! update_stack "$SECURITY_STACK" "$SECURITY_TEMPLATE" "[{\"ParameterKey\":\"EnvironmentName\",\"ParameterValue\":\"$ENVIRONMENT\"}]"; then
        log_error "Failed to update security stack"
        exit 1
    fi
    
    # Update database stack
    log_info "Updating database stack..."
    if ! update_stack "$DATABASE_STACK" "$DATABASE_TEMPLATE" "[{\"ParameterKey\":\"EnvironmentName\",\"ParameterValue\":\"$ENVIRONMENT\"}]"; then
        log_error "Failed to update database stack"
        exit 1
    fi
    
    # Update business API stack
    log_info "Updating business API stack..."
    if ! update_stack "$BUSINESS_API_STACK" "$BUSINESS_API_TEMPLATE" "[{\"ParameterKey\":\"EnvironmentName\",\"ParameterValue\":\"$ENVIRONMENT\"}]"; then
        log_error "Failed to update business API stack"
        exit 1
    fi
    
    # Update data API stack
    log_info "Updating data API stack..."
    if ! update_stack "$DATA_API_STACK" "$DATA_API_TEMPLATE" "[{\"ParameterKey\":\"Environment\",\"ParameterValue\":\"$ENVIRONMENT\"}]"; then
        log_error "Failed to update data API stack"
        exit 1
    fi
    
    log_info "Stack update process completed successfully"
}

# Execute main function
main