# Provider and environment configuration
variable "environment" {
  type        = string
  description = "Environment name for resource tagging and identification"
  default     = "prod"

  validation {
    condition     = var.environment == "prod"
    error_message = "Environment must be prod for production configuration"
  }
}

variable "aws_region" {
  type        = string
  description = "AWS region for production deployment"
  default     = "us-east-1"

  validation {
    condition     = contains(["us-east-1", "us-west-2"], var.aws_region)
    error_message = "Region must be us-east-1 (primary) or us-west-2 (DR)"
  }
}

# Networking configuration
variable "vpc_cidr" {
  type        = string
  description = "CIDR block for production VPC with sufficient IP space for all components"
  default     = "10.0.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "VPC CIDR must be a valid IPv4 CIDR block"
  }
}

variable "availability_zones" {
  type        = list(string)
  description = "List of availability zones for multi-AZ deployment ensuring high availability"
  default     = ["us-east-1a", "us-east-1b", "us-east-1c"]

  validation {
    condition     = length(var.availability_zones) >= 3
    error_message = "Production requires at least 3 availability zones"
  }
}

# Database configuration
variable "db_instance_class" {
  type        = string
  description = "RDS instance class for production database with sufficient CPU and memory"
  default     = "db.r6g.xlarge"

  validation {
    condition     = can(regex("^db\\.(r6g|r6i|r5)\\..*", var.db_instance_class))
    error_message = "Must use r6g, r6i, or r5 instance family for production"
  }
}

# EKS configuration
variable "eks_node_instance_type" {
  type        = string
  description = "EC2 instance type for EKS nodes with balanced CPU and memory"
  default     = "t3.large"

  validation {
    condition     = can(regex("^t3\\.(large|xlarge)|m5\\.(large|xlarge)", var.eks_node_instance_type))
    error_message = "Must use t3 or m5 instance family for EKS nodes"
  }
}

variable "eks_min_nodes" {
  type        = number
  description = "Minimum number of nodes in EKS cluster for high availability"
  default     = 3

  validation {
    condition     = var.eks_min_nodes >= 3
    error_message = "Production requires at least 3 nodes for high availability"
  }
}

# Redis configuration
variable "redis_node_type" {
  type        = string
  description = "ElastiCache Redis node type for caching layer"
  default     = "cache.r6g.large"

  validation {
    condition     = can(regex("^cache\\.(r6g|r6|r5)\\.(large|xlarge)", var.redis_node_type))
    error_message = "Must use r6g, r6, or r5 instance family for Redis"
  }
}

# Backup configuration
variable "backup_retention_days" {
  type        = number
  description = "Number of days to retain backups for disaster recovery"
  default     = 7

  validation {
    condition     = var.backup_retention_days >= 7
    error_message = "Production requires minimum 7 days backup retention"
  }
}

# Security configuration
variable "waf_rules" {
  type        = map(string)
  description = "WAF rules configuration for API protection"
  default = {
    rate_limit     = "2000"
    ip_rate_limit  = "100"
    geo_match      = "true"
  }
}

# Monitoring configuration
variable "monitoring_config" {
  type        = map(string)
  description = "CloudWatch monitoring configuration"
  default = {
    metric_resolution       = "1"
    log_retention_days     = "30"
    alarm_evaluation_periods = "3"
  }
}

# Resource tagging
variable "tags" {
  type        = map(string)
  description = "Common tags for all resources including compliance and security markers"
  default = {
    Environment       = "prod"
    Project          = "estatekit"
    ManagedBy        = "terraform"
    SecurityZone     = "restricted"
    ComplianceLevel  = "high"
    DataClassification = "sensitive"
  }
}