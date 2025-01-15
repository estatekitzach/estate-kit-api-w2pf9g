# Provider and terraform configuration
terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.0"
    }
  }
}

# Local variables for common configurations
locals {
  common_tags = {
    Environment        = var.environment
    Project           = "estatekit"
    ManagedBy         = "terraform"
    SecurityZone      = "api"
    ComplianceLevel   = "high"
    DataClassification = "sensitive"
  }

  # Load balancer configuration
  lb_config = {
    deletion_protection = var.environment == "prod"
    idle_timeout       = 60
    http2_enabled      = true
  }

  # API service configuration
  api_config = {
    business_api = {
      name           = "estatekit-${var.environment}-business-api"
      container_port = 443
      replicas       = var.business_api_replicas
      cpu_limit      = "1000m"
      memory_limit   = "2Gi"
    }
    data_api = {
      name           = "estatekit-${var.environment}-data-api"
      container_port = 443
      replicas       = var.data_api_replicas
      cpu_limit      = "2000m"
      memory_limit   = "4Gi"
    }
  }
}

# S3 bucket for ALB access logs
resource "aws_s3_bucket" "alb_logs" {
  bucket = "estatekit-${var.environment}-alb-logs"
  
  tags = local.common_tags
}

resource "aws_s3_bucket_versioning" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "alb_logs" {
  bucket = aws_s3_bucket.alb_logs.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# Security group for ALB
resource "aws_security_group" "alb" {
  name        = "estatekit-${var.environment}-alb-sg"
  description = "Security group for API load balancer"
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
    Name = "estatekit-${var.environment}-alb-sg"
  })
}

# Application Load Balancer
resource "aws_lb" "api" {
  name               = "estatekit-${var.environment}-api-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets           = var.private_subnet_ids

  enable_deletion_protection = local.lb_config.deletion_protection
  enable_http2              = local.lb_config.http2_enabled
  idle_timeout             = local.lb_config.idle_timeout

  access_logs {
    bucket  = aws_s3_bucket.alb_logs.id
    prefix  = "api-alb"
    enabled = true
  }

  tags = local.common_tags
}

# ACM Certificate for ALB
resource "aws_acm_certificate" "api" {
  domain_name       = "api.estatekit.com"
  validation_method = "DNS"

  tags = local.common_tags

  lifecycle {
    create_before_destroy = true
  }
}

# HTTPS Listener
resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.api.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = aws_acm_certificate.api.arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.business_api.arn
  }
}

# Target Groups
resource "aws_lb_target_group" "business_api" {
  name        = "estatekit-${var.environment}-business-api"
  port        = local.api_config.business_api.container_port
  protocol    = "HTTPS"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    enabled             = true
    healthy_threshold   = 2
    interval            = 30
    matcher            = "200"
    path               = "/health"
    port               = "traffic-port"
    protocol           = "HTTPS"
    timeout            = 5
    unhealthy_threshold = 3
  }

  tags = local.common_tags
}

resource "aws_lb_target_group" "data_api" {
  name        = "estatekit-${var.environment}-data-api"
  port        = local.api_config.data_api.container_port
  protocol    = "HTTPS"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    enabled             = true
    healthy_threshold   = 2
    interval            = 30
    matcher            = "200"
    path               = "/health"
    port               = "traffic-port"
    protocol           = "HTTPS"
    timeout            = 5
    unhealthy_threshold = 3
  }

  tags = local.common_tags
}

# EKS Node Group IAM Role
resource "aws_iam_role" "node_group" {
  name = "estatekit-${var.environment}-eks-node-group"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

# EKS Node Group
resource "aws_eks_node_group" "api" {
  cluster_name    = "estatekit-${var.environment}-eks"
  node_group_name = "estatekit-${var.environment}-api"
  node_role_arn   = aws_iam_role.node_group.arn
  subnet_ids      = var.private_subnet_ids

  instance_types = ["t3.large"]

  scaling_config {
    desired_size = 3
    max_size     = 10
    min_size     = 3
  }

  tags = local.common_tags
}

# WAF Association
resource "aws_wafv2_web_acl_association" "api" {
  resource_arn = aws_lb.api.arn
  web_acl_arn  = var.waf_acl_id
}

# CloudWatch Log Groups
resource "aws_cloudwatch_log_group" "business_api" {
  name              = "/aws/estatekit/${var.environment}/business-api"
  retention_in_days = 90
  
  tags = local.common_tags
}

resource "aws_cloudwatch_log_group" "data_api" {
  name              = "/aws/estatekit/${var.environment}/data-api"
  retention_in_days = 90
  
  tags = local.common_tags
}

# Outputs
output "api_endpoint" {
  description = "ALB endpoint for API access"
  value       = aws_lb.api.dns_name
}

output "business_api_service_name" {
  description = "Kubernetes service name for Business API"
  value       = local.api_config.business_api.name
}

output "data_api_service_name" {
  description = "Kubernetes service name for Data API"
  value       = local.api_config.data_api.name
}