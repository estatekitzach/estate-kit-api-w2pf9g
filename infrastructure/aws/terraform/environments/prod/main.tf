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
      version = "~> 2.23"
    }
  }

  backend "s3" {
    bucket         = "estatekit-terraform-state"
    key            = "prod/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "estatekit-terraform-locks"
    kms_key_id     = "alias/terraform-state"
  }
}

# Local variables
locals {
  environment = "prod"
  region     = "us-east-1"
  availability_zones = ["us-east-1a", "us-east-1b", "us-east-1c"]
  
  common_tags = {
    Environment        = "prod"
    Project           = "estatekit"
    ManagedBy         = "terraform"
    BusinessUnit      = "platform"
    CostCenter        = "platform-prod"
    DataClassification = "confidential"
  }
}

# Core networking infrastructure
module "networking" {
  source = "../modules/networking"

  environment         = local.environment
  region             = local.region
  vpc_cidr           = "10.0.0.0/16"
  availability_zones = local.availability_zones
  
  enable_flow_logs      = true
  enable_vpc_endpoints  = true
  nat_gateway_redundancy = true

  tags = local.common_tags
}

# Security infrastructure
module "security" {
  source = "../modules/security"

  environment = local.environment
  project     = "estatekit"
  region      = local.region
  vpc_id      = module.networking.vpc_id

  enable_waf       = true
  enable_shield    = true
  enable_guardduty = true
  key_rotation_period = 90

  tags = local.common_tags
}

# EKS cluster
module "eks" {
  source = "../modules/eks"

  cluster_name = "estatekit-prod"
  environment  = local.environment
  vpc_id       = module.networking.vpc_id
  subnet_ids   = module.networking.private_subnet_ids

  min_nodes = 3
  max_nodes = 10
  node_type = "t3.large"

  kms_key_arn = module.security.kms_key_id

  enable_cluster_autoscaler = true
  enable_metrics_server     = true
  enable_container_insights = true

  tags = local.common_tags
}

# Database infrastructure
module "database" {
  source = "../modules/database"

  environment = local.environment
  vpc_id      = module.networking.vpc_id
  subnet_ids  = module.networking.private_subnet_ids

  instance_class = "db.r6g.xlarge"
  multi_az      = true

  backup_retention_period = 35
  kms_key_id             = module.security.kms_key_id

  performance_insights_enabled = true
  deletion_protection         = true
  auto_minor_version_upgrade  = true

  tags = local.common_tags
}

# API infrastructure
module "api" {
  source = "../modules/api"

  environment = local.environment
  vpc_id      = module.networking.vpc_id
  
  private_subnet_ids = module.networking.private_subnet_ids
  public_subnet_ids  = module.networking.public_subnet_ids
  
  alb_security_group_id = module.security.alb_security_group_id
  waf_acl_id           = module.security.waf_acl_id

  ssl_policy = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  enable_access_logs = true
  enable_deletion_protection = true

  tags = local.common_tags
}

# Outputs
output "vpc_id" {
  description = "ID of the production VPC"
  value       = module.networking.vpc_id
}

output "eks_cluster_endpoint" {
  description = "EKS cluster endpoint URL"
  value       = module.eks.cluster_endpoint
}

output "rds_endpoint" {
  description = "RDS cluster endpoint"
  value       = module.database.rds_cluster_endpoint
}

output "business_api_url" {
  description = "Business API endpoint URL"
  value       = module.api.business_api_endpoint
}

output "data_api_url" {
  description = "Data API endpoint URL"
  value       = module.api.data_api_endpoint
}