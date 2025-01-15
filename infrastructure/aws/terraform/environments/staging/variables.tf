# AWS region configuration with strict validation for staging environment
variable "aws_region" {
  type        = string
  description = "AWS region for staging environment deployment with strict validation"
  default     = "us-east-1"

  validation {
    condition     = can(regex("^us-east-1$", var.aws_region))
    error_message = "Staging environment must be deployed in us-east-1 as per technical specifications"
  }
}

# Environment name configuration with validation
variable "environment" {
  type        = string
  description = "Environment name for resource tagging with validation"
  default     = "staging"

  validation {
    condition     = can(regex("^staging$", var.environment))
    error_message = "Environment must be staging for this configuration"
  }
}

# VPC configuration
variable "vpc_cidr" {
  type        = string
  description = "CIDR block for staging VPC with isolated network range"
  default     = "10.1.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "VPC CIDR must be a valid IPv4 CIDR block"
  }
}

# Availability zones configuration
variable "availability_zones" {
  type        = list(string)
  description = "Single availability zone for staging environment as per specifications"
  default     = ["us-east-1a"]

  validation {
    condition     = length(var.availability_zones) == 1
    error_message = "Staging environment must use single AZ deployment"
  }
}

# EKS cluster configuration
variable "eks_cluster_name" {
  type        = string
  description = "Name of the EKS cluster for staging environment"
  default     = "estatekit-staging"
}

variable "eks_node_instance_types" {
  type        = list(string)
  description = "Instance types for EKS worker nodes optimized for staging workloads"
  default     = ["t3.large"]
}

variable "eks_min_nodes" {
  type        = number
  description = "Minimum number of EKS worker nodes for staging"
  default     = 2
}

variable "eks_max_nodes" {
  type        = number
  description = "Maximum number of EKS worker nodes for staging"
  default     = 5
}

# RDS configuration
variable "db_instance_class" {
  type        = string
  description = "RDS instance class for staging environment"
  default     = "db.r6g.large"
}

variable "db_multi_az" {
  type        = bool
  description = "Disable multi-AZ for RDS in staging for cost optimization"
  default     = false
}

variable "db_backup_retention_days" {
  type        = number
  description = "Number of days to retain database backups in staging"
  default     = 7

  validation {
    condition     = var.db_backup_retention_days >= 7
    error_message = "Backup retention must be at least 7 days"
  }
}

# Security configuration
variable "enable_waf" {
  type        = bool
  description = "Enable WAF for API protection in staging environment"
  default     = true
}

variable "enable_ssl" {
  type        = bool
  description = "Enable SSL/TLS for API endpoints (required for security)"
  default     = true

  validation {
    condition     = var.enable_ssl == true
    error_message = "SSL must be enabled for all environments including staging"
  }
}

# Resource tagging
variable "tags" {
  type        = map(string)
  description = "Common tags to be applied to all resources in staging"
  default = {
    Environment = "staging"
    Project     = "estatekit"
    ManagedBy   = "terraform"
  }
}