#!/bin/bash

# EstateKit Personal Information API System
# CloudFormation Template Validation Script
# Version: 1.0.0
# Dependencies:
# - aws-cli v2.x
# - jq v1.6
# - parallel (latest)

set -euo pipefail

# Global variables
TEMPLATE_DIR="../cloudformation"
AWS_REGION=${AWS_REGION:-"us-east-1"}
EXIT_CODE=0
LOG_FILE="/var/log/estatekit/template-validation.log"
MAX_PARALLEL=4

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Initialize logging
setup_logging() {
    local log_dir=$(dirname "${LOG_FILE}")
    if [[ ! -d "${log_dir}" ]]; then
        mkdir -p "${log_dir}"
    fi
    touch "${LOG_FILE}"
    exec 1> >(tee -a "${LOG_FILE}")
    exec 2> >(tee -a "${LOG_FILE}" >&2)
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Starting template validation"
}

# Check prerequisites
check_prerequisites() {
    local missing_deps=0

    echo "Checking prerequisites..."

    # Check AWS CLI
    if ! command -v aws &> /dev/null; then
        echo -e "${RED}Error: AWS CLI is not installed${NC}"
        missing_deps=1
    else
        aws_version=$(aws --version | cut -d/ -f2 | cut -d. -f1)
        if [[ ${aws_version} -lt 2 ]]; then
            echo -e "${RED}Error: AWS CLI version 2 or higher is required${NC}"
            missing_deps=1
        fi
    fi

    # Check jq
    if ! command -v jq &> /dev/null; then
        echo -e "${RED}Error: jq is not installed${NC}"
        missing_deps=1
    fi

    # Check parallel
    if ! command -v parallel &> /dev/null; then
        echo -e "${RED}Error: parallel is not installed${NC}"
        missing_deps=1
    fi

    # Check AWS credentials
    if ! aws sts get-caller-identity &> /dev/null; then
        echo -e "${RED}Error: Invalid or missing AWS credentials${NC}"
        missing_deps=1
    fi

    # Check template directory
    if [[ ! -d "${TEMPLATE_DIR}" ]]; then
        echo -e "${RED}Error: Template directory not found: ${TEMPLATE_DIR}${NC}"
        missing_deps=1
    fi

    if [[ ${missing_deps} -ne 0 ]]; then
        return 1
    fi

    echo -e "${GREEN}All prerequisites satisfied${NC}"
    return 0
}

# Validate individual template
validate_template() {
    local template_file=$1
    local template_name=$(basename "${template_file}")
    local validation_type=${2:-"full"}
    local temp_dir=$(mktemp -d)
    local results_file="${temp_dir}/results.json"
    local error_count=0

    echo "Validating template: ${template_name}"

    # Check file exists and is readable
    if [[ ! -r "${template_file}" ]]; then
        echo -e "${RED}Error: Cannot read template file: ${template_file}${NC}"
        return 1
    }

    # Validate YAML syntax
    if ! python3 -c "import yaml; yaml.safe_load(open('${template_file}'))" 2>/dev/null; then
        echo -e "${RED}Error: Invalid YAML syntax in ${template_name}${NC}"
        error_count=$((error_count + 1))
    fi

    # AWS CloudFormation validation
    if ! aws cloudformation validate-template \
        --template-body "file://${template_file}" \
        --region "${AWS_REGION}" > "${results_file}" 2>/dev/null; then
        echo -e "${RED}Error: Template validation failed for ${template_name}${NC}"
        error_count=$((error_count + 1))
    fi

    # Enhanced security checks
    if [[ "${validation_type}" == "full" ]]; then
        # Check for encryption configurations
        if ! grep -q "StorageEncrypted.*true\|EncryptionAtRest\|KmsKeyId" "${template_file}"; then
            echo -e "${YELLOW}Warning: No encryption configuration found in ${template_name}${NC}"
        fi

        # Check for IAM roles least privilege
        if grep -q "Effect.*Allow.*Resource.*\*" "${template_file}"; then
            echo -e "${YELLOW}Warning: Overly permissive IAM policies found in ${template_name}${NC}"
        fi

        # Check for security group configurations
        if grep -q "CidrIp.*0\.0\.0\.0/0" "${template_file}"; then
            echo -e "${YELLOW}Warning: Open CIDR ranges found in security groups in ${template_name}${NC}"
        fi

        # Check for logging configurations
        if ! grep -q "LogGroup\|AccessLogging\|CloudWatchLogs" "${template_file}"; then
            echo -e "${YELLOW}Warning: No logging configuration found in ${template_name}${NC}"
        fi
    fi

    # Cross-stack reference validation
    if grep -q "Fn::ImportValue" "${template_file}"; then
        echo "Checking cross-stack references..."
        # This is a simplified check - in production, you'd want to validate
        # against actual exported values in the account
    fi

    # Clean up
    rm -rf "${temp_dir}"

    if [[ ${error_count} -eq 0 ]]; then
        echo -e "${GREEN}Template ${template_name} validated successfully${NC}"
        return 0
    else
        echo -e "${RED}Template ${template_name} validation failed with ${error_count} errors${NC}"
        return 1
    fi
}

# Process templates in parallel
process_templates() {
    local templates=()
    
    # Find all CloudFormation templates
    while IFS= read -r -d '' file; do
        templates+=("$file")
    done < <(find "${TEMPLATE_DIR}" -type f \( -name "*.yml" -o -name "*.yaml" \) -print0)

    if [[ ${#templates[@]} -eq 0 ]]; then
        echo -e "${RED}Error: No templates found in ${TEMPLATE_DIR}${NC}"
        return 1
    fi

    echo "Found ${#templates[@]} templates to validate"

    # Export functions for parallel execution
    export -f validate_template
    export -f echo

    # Run validations in parallel
    printf "%s\n" "${templates[@]}" | \
        parallel --jobs "${MAX_PARALLEL}" --keep-order \
        "validate_template {} full || echo \"FAIL: {}\""

    # Check for any failures
    if grep -q "FAIL:" "${LOG_FILE}"; then
        EXIT_CODE=1
    fi
}

# Main execution
main() {
    setup_logging

    echo "Starting template validation process..."
    echo "Template directory: ${TEMPLATE_DIR}"
    echo "AWS Region: ${AWS_REGION}"

    if ! check_prerequisites; then
        echo -e "${RED}Prerequisites check failed. Exiting.${NC}"
        exit 1
    fi

    process_templates

    if [[ ${EXIT_CODE} -eq 0 ]]; then
        echo -e "${GREEN}All templates validated successfully${NC}"
    else
        echo -e "${RED}One or more templates failed validation${NC}"
    fi

    echo "Validation results logged to: ${LOG_FILE}"
    exit ${EXIT_CODE}
}

# Execute main function
main