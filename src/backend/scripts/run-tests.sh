#!/bin/bash

# EstateKit Personal Information API Test Runner
# Version: 1.0
# Requires: .NET SDK 9.0+, bash 5.0+
# Purpose: Executes all test suites with parallel execution and comprehensive reporting

set -euo pipefail
trap cleanup EXIT

# Global configuration
readonly TEST_RESULTS_DIR="./TestResults"
readonly COVERAGE_DIR="./CodeCoverage"
readonly CONFIGURATION="Release"
readonly MIN_COVERAGE_THRESHOLD=80
readonly MAX_RETRY_ATTEMPTS=3
readonly PARALLEL_WORKERS=4

# Logging utilities
log_info() {
    echo "[INFO] $(date '+%Y-%m-%d %H:%M:%S') - $1"
}

log_error() {
    echo "[ERROR] $(date '+%Y-%m-%d %H:%M:%S') - $1" >&2
}

log_warning() {
    echo "[WARN] $(date '+%Y-%m-%d %H:%M:%S') - $1"
}

# Environment validation
validate_environment() {
    log_info "Validating environment..."
    
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK not found. Please install .NET SDK 9.0 or later."
        exit 1
    }

    # Validate .NET version
    local dotnet_version=$(dotnet --version)
    if [[ ! "$dotnet_version" =~ ^9\. ]]; then
        log_error "Required .NET SDK version 9.0 or later. Found: $dotnet_version"
        exit 1
    }

    # Check available disk space
    local available_space=$(df -h . | awk 'NR==2 {print $4}')
    log_info "Available disk space: $available_space"
    
    # Create directories with proper permissions
    mkdir -p "${TEST_RESULTS_DIR}" "${COVERAGE_DIR}"
    chmod 755 "${TEST_RESULTS_DIR}" "${COVERAGE_DIR}"
}

# Unit test execution
run_unit_tests() {
    local exit_code=0
    log_info "Starting unit test execution..."

    # Business API Tests
    log_info "Running Business API tests..."
    if ! dotnet test ../Tests/Business.API.Tests/Business.API.Tests.csproj \
        --configuration ${CONFIGURATION} \
        --results-directory "${TEST_RESULTS_DIR}/BusinessAPI" \
        --collect:"XPlat Code Coverage" \
        --settings ../Tests/coverlet.runsettings \
        --blame-hang-timeout 60s \
        --logger "console;verbosity=detailed" \
        --logger "trx;LogFileName=BusinessAPI.trx" \
        --parallel \
        --blame; then
        log_error "Business API tests failed"
        exit_code=1
    fi

    # Data API Tests
    log_info "Running Data API tests..."
    if ! dotnet test ../Tests/Data.API.Tests/Data.API.Tests.csproj \
        --configuration ${CONFIGURATION} \
        --results-directory "${TEST_RESULTS_DIR}/DataAPI" \
        --collect:"XPlat Code Coverage" \
        --settings ../Tests/coverlet.runsettings \
        --blame-hang-timeout 60s \
        --logger "console;verbosity=detailed" \
        --logger "trx;LogFileName=DataAPI.trx" \
        --parallel \
        --blame; then
        log_error "Data API tests failed"
        exit_code=1
    fi

    return $exit_code
}

# Integration test execution with retry mechanism
run_integration_tests() {
    local attempt=1
    local exit_code=1
    
    log_info "Starting integration test execution..."

    while [ $attempt -le $MAX_RETRY_ATTEMPTS ] && [ $exit_code -ne 0 ]; do
        log_info "Integration test attempt $attempt of $MAX_RETRY_ATTEMPTS"
        
        if dotnet test ../Tests/Integration.Tests/Integration.Tests.csproj \
            --configuration ${CONFIGURATION} \
            --results-directory "${TEST_RESULTS_DIR}/Integration" \
            --collect:"XPlat Code Coverage" \
            --settings ../Tests/coverlet.runsettings \
            --blame-hang-timeout 120s \
            --logger "console;verbosity=detailed" \
            --logger "trx;LogFileName=Integration.trx"; then
            exit_code=0
            break
        else
            log_warning "Integration tests failed on attempt $attempt"
            sleep 5
        fi
        
        ((attempt++))
    done

    if [ $exit_code -ne 0 ]; then
        log_error "Integration tests failed after $MAX_RETRY_ATTEMPTS attempts"
    fi

    return $exit_code
}

# Coverage report generation
generate_coverage_report() {
    log_info "Generating coverage reports..."
    
    # Merge coverage reports
    local coverage_files=("${TEST_RESULTS_DIR}"/**/*.cobertura.xml)
    
    if [ ${#coverage_files[@]} -eq 0 ]; then
        log_error "No coverage files found"
        return 1
    }

    # Generate HTML report
    dotnet reportgenerator \
        -reports:"${coverage_files[*]}" \
        -targetdir:"${COVERAGE_DIR}/report" \
        -reporttypes:"Html;Cobertura;JsonSummary" \
        -title:"EstateKit API Test Coverage" \
        -verbosity:"Warning"

    # Validate coverage threshold
    local coverage_percentage=$(jq '.summary.lineCoverage' "${COVERAGE_DIR}/report/Summary.json")
    
    if (( $(echo "$coverage_percentage < $MIN_COVERAGE_THRESHOLD" | bc -l) )); then
        log_error "Coverage ${coverage_percentage}% below minimum threshold of ${MIN_COVERAGE_THRESHOLD}%"
        return 1
    fi
    
    log_info "Coverage report generated successfully. Total coverage: ${coverage_percentage}%"
    return 0
}

# Resource cleanup
cleanup() {
    log_info "Performing cleanup..."
    
    # Clean up old test results (keep last 7 days)
    find "${TEST_RESULTS_DIR}" -type f -mtime +7 -delete 2>/dev/null || true
    
    # Compress logs
    if [ -d "${TEST_RESULTS_DIR}" ]; then
        local timestamp=$(date +%Y%m%d_%H%M%S)
        tar -czf "${TEST_RESULTS_DIR}/logs_${timestamp}.tar.gz" \
            "${TEST_RESULTS_DIR}"/*.trx \
            "${TEST_RESULTS_DIR}"/*.log 2>/dev/null || true
    fi
    
    # Remove temporary files
    find . -type f -name "*.tmp" -delete 2>/dev/null || true
    
    log_info "Cleanup completed"
}

# Main execution
main() {
    local exit_code=0
    
    log_info "Starting test execution..."
    
    validate_environment
    
    # Run unit tests
    if ! run_unit_tests; then
        log_error "Unit tests failed"
        exit_code=1
    fi
    
    # Run integration tests
    if ! run_integration_tests; then
        log_error "Integration tests failed"
        exit_code=1
    fi
    
    # Generate coverage report
    if ! generate_coverage_report; then
        log_error "Coverage report generation failed"
        exit_code=1
    fi
    
    if [ $exit_code -eq 0 ]; then
        log_info "All tests completed successfully"
    else
        log_error "Test execution failed"
    fi
    
    return $exit_code
}

# Execute main function
main "$@"