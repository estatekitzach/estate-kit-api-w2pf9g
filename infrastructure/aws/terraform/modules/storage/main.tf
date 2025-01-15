terraform {
  required_version = ">= 1.5.0"
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

# Provider for disaster recovery region
provider "aws" {
  alias  = "dr"
  region = var.dr_region
}

# Document storage bucket
resource "aws_s3_bucket" "documents" {
  bucket = "${var.project}-documents-${var.environment}"
  
  # Prevent accidental deletion
  force_destroy = false
  
  # Enable object lock for compliance
  object_lock_enabled = true
  
  tags = {
    Environment        = var.environment
    Project           = var.project
    ManagedBy         = "terraform"
    SecurityZone      = "restricted"
    DataClassification = "sensitive"
    CostCenter        = "storage"
  }
}

# Versioning configuration
resource "aws_s3_bucket_versioning" "documents" {
  bucket = aws_s3_bucket.documents.id
  
  versioning_configuration {
    status     = "Enabled"
    mfa_delete = "Enabled"
  }
}

# Server-side encryption configuration
resource "aws_s3_bucket_server_side_encryption_configuration" "documents" {
  bucket = aws_s3_bucket.documents.id

  rule {
    apply_server_side_encryption_by_default {
      kms_master_key_id = var.enable_encryption ? data.terraform_remote_state.security.outputs.kms_key_arn : null
      sse_algorithm     = var.enable_encryption ? "aws:kms" : "AES256"
    }
    bucket_key_enabled = true
  }
}

# Intelligent tiering configuration
resource "aws_s3_bucket_intelligent_tiering_configuration" "documents" {
  count  = var.enable_intelligent_tiering ? 1 : 0
  bucket = aws_s3_bucket.documents.id
  name   = "DocumentTiering"

  tiering {
    access_tier = "DEEP_ARCHIVE_ACCESS"
    days        = 180
  }

  tiering {
    access_tier = "ARCHIVE_ACCESS"
    days        = 90
  }
}

# Lifecycle rules
resource "aws_s3_bucket_lifecycle_configuration" "documents" {
  bucket = aws_s3_bucket.documents.id

  rule {
    id     = "archive_old_versions"
    status = "Enabled"

    transition {
      days          = var.backup_retention_days
      storage_class = "GLACIER"
    }

    noncurrent_version_transition {
      noncurrent_days = 30
      storage_class   = "GLACIER"
    }
  }

  rule {
    id     = "delete_old_versions"
    status = "Enabled"

    noncurrent_version_expiration {
      noncurrent_days = 365
    }
  }
}

# Replication configuration for disaster recovery
resource "aws_s3_bucket" "documents_replica" {
  provider = aws.dr
  bucket   = "${var.project}-documents-${var.environment}-replica"
  
  tags = {
    Environment        = var.environment
    Project           = var.project
    ManagedBy         = "terraform"
    SecurityZone      = "restricted"
    DataClassification = "sensitive"
    CostCenter        = "storage"
    ReplicaOf         = aws_s3_bucket.documents.id
  }
}

resource "aws_s3_bucket_replication_configuration" "documents" {
  bucket = aws_s3_bucket.documents.id
  role   = aws_iam_role.replication.arn

  rule {
    id     = "document_replication"
    status = "Enabled"

    destination {
      bucket        = aws_s3_bucket.documents_replica.arn
      storage_class = "STANDARD_IA"
    }

    source_selection_criteria {
      sse_kms_encrypted_objects {
        status = "Enabled"
      }
    }
  }
}

# Block public access
resource "aws_s3_bucket_public_access_block" "documents" {
  bucket = aws_s3_bucket.documents.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Apply bucket policy from storage-policy.json
resource "aws_s3_bucket_policy" "documents" {
  bucket = aws_s3_bucket.documents.id
  policy = templatefile("${path.module}/../../security/iam/storage-policy.json", {
    EnvironmentName = var.environment
    KMSKeyArn      = data.terraform_remote_state.security.outputs.kms_key_arn
  })
}

# Enable access logging
resource "aws_s3_bucket_logging" "documents" {
  bucket = aws_s3_bucket.documents.id

  target_bucket = aws_s3_bucket.documents_logs.id
  target_prefix = "document-access-logs/"
}

# Access logs bucket
resource "aws_s3_bucket" "documents_logs" {
  bucket = "${var.project}-documents-logs-${var.environment}"
  
  tags = {
    Environment        = var.environment
    Project           = var.project
    ManagedBy         = "terraform"
    SecurityZone      = "restricted"
    DataClassification = "sensitive"
    CostCenter        = "storage"
  }
}

# CORS configuration
resource "aws_s3_bucket_cors_configuration" "documents" {
  bucket = aws_s3_bucket.documents.id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "PUT", "POST", "DELETE"]
    allowed_origins = ["https://*.estatekit.com"]
    expose_headers  = ["ETag"]
    max_age_seconds = 3000
  }
}

# Outputs
output "document_bucket_id" {
  value       = aws_s3_bucket.documents.id
  description = "ID of the document storage bucket"
}

output "document_bucket_arn" {
  value       = aws_s3_bucket.documents.arn
  description = "ARN of the document storage bucket"
}

output "replica_bucket_arn" {
  value       = aws_s3_bucket.documents_replica.arn
  description = "ARN of the replica document storage bucket"
}