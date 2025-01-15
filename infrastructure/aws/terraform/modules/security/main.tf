terraform {
  required_version = ">= 1.0.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# Local variables
locals {
  name_prefix = "estatekit-${var.environment}"
  common_tags = {
    Environment = var.environment
    Service     = "estatekit"
    ManagedBy   = "terraform"
  }
  
  # Load WAF rules from JSON file
  waf_rules = jsondecode(file("${path.module}/../../security/waf/api-rules.json"))
}

# KMS key for field-level encryption
resource "aws_kms_key" "field_encryption" {
  description              = "KMS key for field-level encryption in EstateKit"
  deletion_window_in_days  = 30
  enable_key_rotation     = true
  multi_region            = true
  policy                  = data.template_file.kms_key_policy.rendered
  
  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-field-encryption"
  })
}

resource "aws_kms_alias" "field_encryption" {
  name          = "alias/${local.name_prefix}-field-encryption"
  target_key_id = aws_kms_key.field_encryption.key_id
}

# WAF Web ACL for API protection
resource "aws_wafv2_web_acl" "api_acl" {
  name        = "${local.name_prefix}-api-protection"
  description = "WAF Web ACL for EstateKit API protection"
  scope       = "REGIONAL"

  default_action {
    allow {}
  }

  dynamic "rule" {
    for_each = local.waf_rules.webACLConfig.rules
    content {
      name     = rule.value.name
      priority = rule.value.priority

      override_action {
        dynamic "none" {
          for_each = rule.value.overrideAction == "none" ? [1] : []
          content {}
        }
      }

      statement {
        dynamic "rate_based_statement" {
          for_each = try(rule.value.statement.rateBasedStatement, null) != null ? [rule.value.statement.rateBasedStatement] : []
          content {
            limit              = rate_based_statement.value.limit
            aggregate_key_type = rate_based_statement.value.aggregateKeyType
          }
        }

        dynamic "managed_rule_group_statement" {
          for_each = try(rule.value.statement.managedRuleGroupStatement, null) != null ? [rule.value.statement.managedRuleGroupStatement] : []
          content {
            name        = managed_rule_group_statement.value.name
            vendor_name = managed_rule_group_statement.value.vendorName
          }
        }
      }

      visibility_config {
        cloudwatch_metrics_enabled = rule.value.visibilityConfig.cloudWatchMetricsEnabled
        metric_name               = rule.value.visibilityConfig.metricName
        sampled_requests_enabled  = rule.value.visibilityConfig.sampledRequestsEnabled
      }
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name               = "${local.name_prefix}-waf-metrics"
    sampled_requests_enabled  = true
  }

  tags = local.common_tags
}

# Security Groups
resource "aws_security_group" "api" {
  name        = "${local.name_prefix}-api-sg"
  description = "Security group for EstateKit APIs"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-api-sg"
  })
}

resource "aws_security_group" "database" {
  name        = "${local.name_prefix}-database-sg"
  description = "Security group for EstateKit database"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.api.id]
  }

  tags = merge(local.common_tags, {
    Name = "${local.name_prefix}-database-sg"
  })
}

# CloudWatch Log Groups for WAF
resource "aws_cloudwatch_log_group" "waf_logs" {
  name              = "/aws/waf/${local.name_prefix}"
  retention_in_days = 90
  
  tags = local.common_tags
}

# WAF Logging Configuration
resource "aws_wafv2_web_acl_logging_configuration" "api_acl" {
  log_destination_configs = [aws_cloudwatch_log_group.waf_logs.arn]
  resource_arn           = aws_wafv2_web_acl.api_acl.arn
  
  redacted_fields {
    single_header {
      name = "authorization"
    }
    single_header {
      name = "x-api-key"
    }
  }
}

# Outputs
output "kms_key_arn" {
  description = "ARN of the KMS key for field-level encryption"
  value       = aws_kms_key.field_encryption.arn
}

output "waf_web_acl_arn" {
  description = "ARN of the WAF Web ACL"
  value       = aws_wafv2_web_acl.api_acl.arn
}

output "security_group_ids" {
  description = "Map of security group IDs"
  value = {
    api      = aws_security_group.api.id
    database = aws_security_group.database.id
  }
}

# Variables
variable "aws_region" {
  description = "AWS region for resource deployment"
  type        = string
}

variable "environment" {
  description = "Deployment environment (staging/production)"
  type        = string
}

variable "vpc_id" {
  description = "VPC ID for security group creation"
  type        = string
}