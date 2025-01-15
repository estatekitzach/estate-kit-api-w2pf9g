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
    bucket         = "estatekit-terraform-state-staging"
    key            = "staging/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "terraform-state-lock"
  }
}

provider "aws" {
  region = var.aws_region
}

provider "kubernetes" {
  host                   = module.eks.cluster_endpoint
  cluster_ca_certificate = base64decode(data.aws_eks_cluster.main.certificate_authority[0].data)
  exec {
    api_version = "client.authentication.k8s.io/v1beta1"
    command     = "aws"
    args = [
      "eks",
      "get-token",
      "--cluster-name",
      module.eks.cluster_name
    ]
  }
}

# Local variables
locals {
  common_tags = {
    Environment = var.environment
    Project     = var.project
    ManagedBy   = "terraform"
  }
}

# Data sources
data "aws_eks_cluster" "main" {
  name = module.eks.cluster_name
}

# Networking module
module "networking" {
  source = "../modules/networking"

  environment              = var.environment
  vpc_cidr                = "10.1.0.0/16"
  availability_zones      = ["us-east-1a"]
  enable_vpn_gateway      = false
  enable_nat_gateway      = true
  single_nat_gateway      = true
  enable_flow_logs        = true
  flow_log_retention_days = 7

  tags = local.common_tags
}

# EKS module
module "eks" {
  source = "../modules/eks"

  cluster_name              = "estatekit-staging"
  environment              = var.environment
  vpc_id                   = module.networking.vpc_id
  subnet_ids               = module.networking.private_subnet_ids
  min_nodes                = 2
  max_nodes                = 5
  instance_types           = ["t3.large"]
  disk_size                = 50
  enable_cluster_autoscaler = true
  enable_metrics_server     = true
  enable_container_insights = true
  log_retention_days       = 30
  kms_key_arn             = module.security.kms_key_arn

  depends_on = [module.networking]
}

# Database module
module "database" {
  source = "../modules/database"

  environment             = var.environment
  vpc_id                  = module.networking.vpc_id
  database_subnet_ids     = module.networking.database_subnet_ids
  instance_class          = "db.r6g.large"
  multi_az                = false
  backup_retention_period = 7
  backup_window           = "03:00-04:00"
  maintenance_window      = "Mon:04:00-Mon:05:00"
  monitoring_interval     = 60
  deletion_protection     = true
  performance_insights_enabled = true

  depends_on = [module.networking]
}

# API module
module "api" {
  source = "../modules/api"

  environment = var.environment
  vpc_id      = module.networking.vpc_id
  subnet_ids  = module.networking.private_subnet_ids

  enable_waf = true
  enable_ssl = true
  ssl_certificate_arn = "arn:aws:acm:us-east-1:*:certificate/staging-cert"
  
  waf_rules = {
    ip_rate_limit = 2000
    geo_match     = ["US", "CA"]
    sql_injection_protection = true
    xss_protection = true
  }

  api_gateway_stage    = "staging"
  log_retention_days   = 30
  enable_xray         = true

  depends_on = [
    module.eks,
    module.database
  ]
}

# Outputs
output "business_api_url" {
  description = "Business API endpoint URL"
  value       = module.api.business_api_endpoint
}

output "data_api_url" {
  description = "Data API endpoint URL"
  value       = module.api.data_api_endpoint
}

output "database_endpoint" {
  description = "Database endpoint"
  value       = module.database.db_instance_endpoint
  sensitive   = true
}

output "eks_cluster_endpoint" {
  description = "EKS cluster endpoint"
  value       = module.eks.cluster_endpoint
  sensitive   = true
}