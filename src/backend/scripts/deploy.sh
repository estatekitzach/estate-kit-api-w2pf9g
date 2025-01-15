#!/bin/bash

# EstateKit Deployment Script v1.0
# Requires: 
# - kubectl v1.27 or higher
# - aws-cli v2.0 or higher
# - helm v3.0 or higher

set -euo pipefail

# Global variables
NAMESPACE=${NAMESPACE:-estatekit}
ENVIRONMENT=${ENVIRONMENT:-production}
AWS_REGION=${AWS_REGION:-us-east-1}
DEPLOY_TIMEOUT=${DEPLOY_TIMEOUT:-300s}
LOG_LEVEL=${LOG_LEVEL:-INFO}
HEALTH_CHECK_RETRIES=${HEALTH_CHECK_RETRIES:-5}
ROLLBACK_TIMEOUT=${ROLLBACK_TIMEOUT:-180s}

# Logging function
log() {
    local level=$1
    shift
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] [$level] $*"
}

# Check all prerequisites before deployment
check_prerequisites() {
    log "INFO" "Checking deployment prerequisites..."

    # Check required tools
    command -v kubectl >/dev/null 2>&1 || { log "ERROR" "kubectl is required but not installed"; exit 1; }
    command -v aws >/dev/null 2>&1 || { log "ERROR" "aws-cli is required but not installed"; exit 1; }
    command -v helm >/dev/null 2>&1 || { log "ERROR" "helm is required but not installed"; exit 1; }

    # Validate kubectl version
    kubectl_version=$(kubectl version --client -o json | jq -r '.clientVersion.gitVersion')
    if [[ ! $kubectl_version =~ v1\.2[7-9]\. ]]; then
        log "ERROR" "kubectl version 1.27 or higher is required"
        exit 1
    fi

    # Check AWS credentials and region
    aws sts get-caller-identity >/dev/null 2>&1 || { log "ERROR" "Invalid AWS credentials"; exit 1; }
    aws configure get region >/dev/null 2>&1 || { log "ERROR" "AWS region not configured"; exit 1; }

    # Verify cluster connectivity
    kubectl cluster-info >/dev/null 2>&1 || { log "ERROR" "Cannot connect to Kubernetes cluster"; exit 1; }

    # Check namespace existence
    kubectl get namespace "$NAMESPACE" >/dev/null 2>&1 || {
        log "INFO" "Creating namespace $NAMESPACE"
        kubectl create namespace "$NAMESPACE"
    }

    # Verify required manifests
    local required_files=(
        "../kubernetes/business-api-deployment.yaml"
        "../kubernetes/data-api-deployment.yaml"
        "../kubernetes/redis-deployment.yaml"
        "../kubernetes/configmap.yaml"
        "../kubernetes/secrets.yaml"
    )
    
    for file in "${required_files[@]}"; do
        if [[ ! -f $file ]]; then
            log "ERROR" "Required manifest $file not found"
            exit 1
        fi
    done

    log "INFO" "Prerequisites check completed successfully"
}

# Deploy Redis cache cluster
deploy_redis() {
    log "INFO" "Starting Redis deployment..."

    # Apply Redis ConfigMap and Secrets
    kubectl apply -f "../kubernetes/configmap.yaml" -n "$NAMESPACE"
    kubectl apply -f "../kubernetes/secrets.yaml" -n "$NAMESPACE"
    
    # Deploy Redis
    kubectl apply -f "../kubernetes/redis-deployment.yaml" -n "$NAMESPACE"
    
    # Wait for deployment
    if ! kubectl rollout status deployment/redis-cache -n "$NAMESPACE" --timeout="$DEPLOY_TIMEOUT"; then
        log "ERROR" "Redis deployment failed"
        rollback "redis" "previous"
        exit 1
    fi

    # Validate Redis health
    local retry=0
    while [[ $retry -lt $HEALTH_CHECK_RETRIES ]]; do
        if kubectl exec -n "$NAMESPACE" -it "$(kubectl get pod -l app=estatekit,component=cache -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')" -- redis-cli ping | grep -q "PONG"; then
            log "INFO" "Redis health check passed"
            return 0
        fi
        ((retry++))
        sleep 5
    done

    log "ERROR" "Redis health check failed after $HEALTH_CHECK_RETRIES attempts"
    rollback "redis" "previous"
    exit 1
}

# Deploy Data Access API
deploy_data_api() {
    log "INFO" "Starting Data API deployment..."

    # Verify Redis is running
    if ! kubectl get deployment redis-cache -n "$NAMESPACE" >/dev/null 2>&1; then
        log "ERROR" "Redis deployment not found"
        exit 1
    }

    # Deploy Data API
    kubectl apply -f "../kubernetes/data-api-deployment.yaml" -n "$NAMESPACE"

    # Wait for deployment
    if ! kubectl rollout status deployment/data-api -n "$NAMESPACE" --timeout="$DEPLOY_TIMEOUT"; then
        log "ERROR" "Data API deployment failed"
        rollback "data-api" "previous"
        exit 1
    }

    # Health check
    local retry=0
    while [[ $retry -lt $HEALTH_CHECK_RETRIES ]]; do
        if kubectl exec -n "$NAMESPACE" -it "$(kubectl get pod -l app=estatekit,component=data-api -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')" -- curl -s http://localhost/health | grep -q "Healthy"; then
            log "INFO" "Data API health check passed"
            return 0
        fi
        ((retry++))
        sleep 5
    done

    log "ERROR" "Data API health check failed after $HEALTH_CHECK_RETRIES attempts"
    rollback "data-api" "previous"
    exit 1
}

# Deploy Business Logic API
deploy_business_api() {
    log "INFO" "Starting Business API deployment..."

    # Verify Data API is running
    if ! kubectl get deployment data-api -n "$NAMESPACE" >/dev/null 2>&1; then
        log "ERROR" "Data API deployment not found"
        exit 1
    }

    # Deploy Business API
    kubectl apply -f "../kubernetes/business-api-deployment.yaml" -n "$NAMESPACE"

    # Wait for deployment
    if ! kubectl rollout status deployment/business-api -n "$NAMESPACE" --timeout="$DEPLOY_TIMEOUT"; then
        log "ERROR" "Business API deployment failed"
        rollback "business-api" "previous"
        exit 1
    }

    # Health check
    local retry=0
    while [[ $retry -lt $HEALTH_CHECK_RETRIES ]]; do
        if kubectl exec -n "$NAMESPACE" -it "$(kubectl get pod -l app=business-api -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')" -- curl -s http://localhost/health | grep -q "Healthy"; then
            log "INFO" "Business API health check passed"
            return 0
        fi
        ((retry++))
        sleep 5
    done

    log "ERROR" "Business API health check failed after $HEALTH_CHECK_RETRIES attempts"
    rollback "business-api" "previous"
    exit 1
}

# Rollback function for failed deployments
rollback() {
    local component=$1
    local version=$2
    
    log "WARN" "Initiating rollback for $component to version $version"
    
    case $component in
        "redis")
            kubectl rollout undo deployment/redis-cache -n "$NAMESPACE"
            ;;
        "data-api")
            kubectl rollout undo deployment/data-api -n "$NAMESPACE"
            ;;
        "business-api")
            kubectl rollout undo deployment/business-api -n "$NAMESPACE"
            ;;
        *)
            log "ERROR" "Unknown component: $component"
            return 1
            ;;
    esac

    # Wait for rollback
    if ! kubectl rollout status deployment/"$component" -n "$NAMESPACE" --timeout="$ROLLBACK_TIMEOUT"; then
        log "ERROR" "Rollback failed for $component"
        return 1
    fi

    log "INFO" "Rollback completed successfully for $component"
    return 0
}

# Main deployment function
main() {
    log "INFO" "Starting EstateKit deployment in $ENVIRONMENT environment"

    # Check prerequisites
    check_prerequisites

    # Login to AWS ECR
    aws ecr get-login-password --region "$AWS_REGION" | docker login --username AWS --password-stdin "$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com"

    # Deploy components in sequence
    deploy_redis || { log "ERROR" "Redis deployment failed"; exit 1; }
    deploy_data_api || { log "ERROR" "Data API deployment failed"; exit 1; }
    deploy_business_api || { log "ERROR" "Business API deployment failed"; exit 1; }

    log "INFO" "EstateKit deployment completed successfully"
}

# Execute main function
main "$@"