#!/bin/bash

# EstateKit Infrastructure Stack Creation Script
# Version: 1.0
# AWS CLI Version Required: 2.x
# Description: Creates AWS CloudFormation stacks for EstateKit Personal Information API system

set -euo pipefail

# Global variables
STACK_PREFIX="estatekit"
AWS_REGION="${AWS_REGION:-us-east-1}"
ENVIRONMENT="${1:-staging}"
MAX_RETRIES=30
WAIT_TIME=60

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Log functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check AWS CLI version
    if ! aws --version | grep -q "aws-cli/2"; then
        log_error "AWS CLI version 2.x is required"
        return 1
    fi

    # Check AWS credentials
    if ! aws sts get-caller-identity &>/dev/null; then
        log_error "AWS credentials not configured"
        return 1
    }

    # Check required template files
    local required_templates=(
        "../cloudformation/networking.yml"
        "../cloudformation/security.yml"
        "../cloudformation/database.yml"
        "../cloudformation/business-api.yml"
        "../cloudformation/data-api.yml"
    )

    for template in "${required_templates[@]}"; do
        if [[ ! -f "$template" ]]; then
            log_error "Required template file not found: $template"
            return 1
        fi
    done

    # Validate environment
    if [[ ! "$ENVIRONMENT" =~ ^(staging|production)$ ]]; then
        log_error "Invalid environment. Must be 'staging' or 'production'"
        return 1
    }

    return 0
}

# Validate CloudFormation template
validate_template() {
    local template_file=$1
    log_info "Validating template: $template_file"
    
    if ! aws cloudformation validate-template \
        --template-body "file://$template_file" \
        --region "$AWS_REGION" &>/dev/null; then
        log_error "Template validation failed: $template_file"
        return 1
    fi
    return 0
}

# Wait for stack operation to complete
wait_for_stack() {
    local stack_name=$1
    local retries=0
    local stack_status

    log_info "Waiting for stack operation to complete: $stack_name"

    while [[ $retries -lt $MAX_RETRIES ]]; do
        stack_status=$(aws cloudformation describe-stacks \
            --stack-name "$stack_name" \
            --region "$AWS_REGION" \
            --query 'Stacks[0].StackStatus' \
            --output text 2>/dev/null || echo "STACK_NOT_FOUND")

        case $stack_status in
            *COMPLETE)
                log_info "Stack $stack_name: $stack_status"
                return 0
                ;;
            *FAILED|*ROLLBACK_COMPLETE)
                log_error "Stack $stack_name failed: $stack_status"
                return 1
                ;;
            *IN_PROGRESS)
                log_info "Stack $stack_name in progress: $stack_status"
                sleep $WAIT_TIME
                ;;
            STACK_NOT_FOUND)
                log_error "Stack $stack_name not found"
                return 1
                ;;
        esac
        ((retries++))
    done

    log_error "Timeout waiting for stack $stack_name"
    return 1
}

# Create or update stack
create_stack() {
    local stack_name=$1
    local template_file=$2
    local parameters=$3
    local stack_exists

    # Check if stack exists
    stack_exists=$(aws cloudformation describe-stacks \
        --stack-name "$stack_name" \
        --region "$AWS_REGION" 2>/dev/null || echo "false")

    if [[ "$stack_exists" == "false" ]]; then
        log_info "Creating stack: $stack_name"
        aws cloudformation create-stack \
            --stack-name "$stack_name" \
            --template-body "file://$template_file" \
            --parameters "$parameters" \
            --capabilities CAPABILITY_NAMED_IAM \
            --region "$AWS_REGION" \
            --tags Key=Environment,Value="$ENVIRONMENT" \
                  Key=Project,Value="EstateKit" \
            --enable-termination-protection
    else
        log_info "Updating stack: $stack_name"
        aws cloudformation update-stack \
            --stack-name "$stack_name" \
            --template-body "file://$template_file" \
            --parameters "$parameters" \
            --capabilities CAPABILITY_NAMED_IAM \
            --region "$AWS_REGION" || {
                if [[ $? -eq 255 ]] && [[ $? =~ "No updates" ]]; then
                    log_info "No updates required for stack: $stack_name"
                    return 0
                else
                    return 1
                fi
            }
    fi

    wait_for_stack "$stack_name"
    return $?
}

# Main deployment function
deploy_stacks() {
    local base_stack_name="${STACK_PREFIX}-${ENVIRONMENT}"

    # 1. Deploy Networking Stack
    log_info "Deploying networking stack..."
    local networking_params="ParameterKey=EnvironmentName,ParameterValue=$ENVIRONMENT"
    create_stack "${base_stack_name}-network" "../cloudformation/networking.yml" "$networking_params" || return 1

    # 2. Deploy Security Stack
    log_info "Deploying security stack..."
    local security_params="ParameterKey=EnvironmentName,ParameterValue=$ENVIRONMENT"
    create_stack "${base_stack_name}-security" "../cloudformation/security.yml" "$security_params" || return 1

    # 3. Deploy Database Stack
    log_info "Deploying database stack..."
    local db_password=$(aws secretsmanager get-secret-value \
        --secret-id "${base_stack_name}/database/password" \
        --query 'SecretString' \
        --output text 2>/dev/null || echo "default-password-change-me")
    
    local database_params="[
        {ParameterKey=EnvironmentName,ParameterValue=$ENVIRONMENT},
        {ParameterKey=DatabaseUser,ParameterValue=estatekit},
        {ParameterKey=DatabasePassword,ParameterValue=$db_password}
    ]"
    create_stack "${base_stack_name}-database" "../cloudformation/database.yml" "$database_params" || return 1

    # 4. Deploy Business API Stack
    log_info "Deploying business API stack..."
    local business_api_params="[
        {ParameterKey=EnvironmentName,ParameterValue=$ENVIRONMENT},
        {ParameterKey=ApiDomainName,ParameterValue=api.estatekit.com},
        {ParameterKey=CertificateArn,ParameterValue=arn:aws:acm:${AWS_REGION}:${AWS_ACCOUNT_ID}:certificate/example}
    ]"
    create_stack "${base_stack_name}-business-api" "../cloudformation/business-api.yml" "$business_api_params" || return 1

    # 5. Deploy Data API Stack
    log_info "Deploying data API stack..."
    local data_api_params="[
        {ParameterKey=Environment,ParameterValue=$ENVIRONMENT},
        {ParameterKey=ContainerImage,ParameterValue=${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/estatekit/data-api:latest},
        {ParameterKey=EncryptionServiceEndpoint,ParameterValue=https://encryption.estatekit.com}
    ]"
    create_stack "${base_stack_name}-data-api" "../cloudformation/data-api.yml" "$data_api_params" || return 1
}

# Main execution
main() {
    log_info "Starting EstateKit infrastructure deployment for environment: $ENVIRONMENT"
    
    # Check prerequisites
    check_prerequisites || exit 1

    # Deploy stacks
    if deploy_stacks; then
        log_info "Infrastructure deployment completed successfully"
        return 0
    else
        log_error "Infrastructure deployment failed"
        return 1
    fi
}

# Execute main function
main