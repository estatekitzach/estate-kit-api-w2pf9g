# Provider and terraform configuration
terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# Variables
variable "environment" {
  type        = string
  description = "Deployment environment identifier (staging/prod)"
  validation {
    condition     = contains(["staging", "prod"], var.environment)
    error_message = "Environment must be staging or prod"
  }
}

variable "project" {
  type        = string
  description = "Project identifier for resource tagging and naming"
  default     = "estatekit"
}

variable "vpc_id" {
  type        = string
  description = "ID of the VPC for RDS deployment"
}

variable "database_subnet_ids" {
  type        = string
  description = "List of subnet IDs for RDS multi-AZ deployment"
}

variable "instance_class" {
  type        = string
  description = "RDS instance class for primary and replicas"
  default     = "db.r6g.xlarge"
}

variable "multi_az" {
  type        = bool
  description = "Enable multi-AZ deployment for high availability"
  default     = true
}

variable "backup_retention_period" {
  type        = number
  description = "Number of days to retain automated backups"
  default     = 7
}

# Local variables
locals {
  common_tags = {
    Environment   = var.environment
    Project      = var.project
    ManagedBy    = "terraform"
    Service      = "database"
    SecurityLevel = "critical"
  }
}

# Data sources
data "aws_kms_key" "database" {
  key_id = "alias/${var.project}-${var.environment}-db-key"
}

# IAM role for enhanced monitoring
resource "aws_iam_role" "rds_monitoring" {
  name = "${var.project}-${var.environment}-rds-monitoring"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "monitoring.rds.amazonaws.com"
        }
      }
    ]
  })

  managed_policy_arns = ["arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"]
}

# DB subnet group
resource "aws_db_subnet_group" "main" {
  name       = "${var.project}-${var.environment}-db-subnet-group"
  subnet_ids = var.database_subnet_ids
  tags       = local.common_tags
}

# Parameter group
resource "aws_db_parameter_group" "main" {
  family = "postgres15"
  name   = "${var.project}-${var.environment}-pg-params"

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements"
  }

  parameter {
    name  = "log_statement"
    value = "all"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = "1000"
  }

  parameter {
    name  = "rds.force_ssl"
    value = "1"
  }

  tags = local.common_tags
}

# Primary RDS instance
resource "aws_db_instance" "main" {
  identifier     = "${var.project}-${var.environment}-db"
  engine         = "postgres"
  engine_version = "15.0"
  instance_class = var.instance_class

  allocated_storage     = 100
  max_allocated_storage = 1000
  storage_encrypted     = true
  kms_key_id           = data.aws_kms_key.database.arn

  db_name = "estatekit"
  multi_az = var.multi_az

  db_subnet_group_name   = aws_db_subnet_group.main.name
  parameter_group_name   = aws_db_parameter_group.main.name
  backup_retention_period = var.backup_retention_period

  performance_insights_enabled          = true
  performance_insights_retention_period = 7
  monitoring_interval                   = 60
  monitoring_role_arn                  = aws_iam_role.rds_monitoring.arn

  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]
  
  deletion_protection = true
  skip_final_snapshot = false
  final_snapshot_identifier = "${var.project}-${var.environment}-final-snapshot"

  tags = local.common_tags
}

# Read replicas
resource "aws_db_instance" "replica" {
  count = 2

  identifier     = "${var.project}-${var.environment}-db-replica-${count.index}"
  instance_class = var.instance_class
  
  replicate_source_db = aws_db_instance.main.id
  parameter_group_name = aws_db_parameter_group.main.name

  performance_insights_enabled          = true
  performance_insights_retention_period = 7
  monitoring_interval                   = 60
  monitoring_role_arn                  = aws_iam_role.rds_monitoring.arn

  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  tags = local.common_tags
}

# Outputs
output "db_instance_endpoint" {
  description = "Connection endpoint for the primary RDS instance"
  value       = aws_db_instance.main.endpoint
}

output "db_replica_endpoints" {
  description = "Connection endpoints for read replica instances"
  value       = aws_db_instance.replica[*].endpoint
}

output "db_instance_id" {
  description = "Identifier of the primary RDS instance"
  value       = aws_db_instance.main.id
}

output "parameter_group_id" {
  description = "Identifier of the database parameter group"
  value       = aws_db_parameter_group.main.id
}