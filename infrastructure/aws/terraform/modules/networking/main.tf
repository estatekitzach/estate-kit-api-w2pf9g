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

# Local variables for common configurations
locals {
  common_tags = {
    Project      = "estatekit"
    ManagedBy    = "terraform"
    SecurityZone = var.environment
  }

  # Subnet CIDR configurations
  subnet_configs = {
    public = {
      prefix     = "10.0.0.0/20"
      nacl_rules = "allow_http_https"
    }
    private = {
      prefix     = "10.0.16.0/20"
      nacl_rules = "restrict_outbound"
    }
    database = {
      prefix     = "10.0.32.0/20"
      nacl_rules = "database_only"
    }
  }
}

# Query available AZs in the current region
data "aws_availability_zones" "available" {
  state = "available"
}

# Main VPC
resource "aws_vpc" "main" {
  cidr_block                           = var.vpc_cidr
  enable_dns_hostnames                 = true
  enable_dns_support                   = true
  instance_tenancy                     = "default"
  enable_network_address_usage_metrics = true

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-vpc"
      Environment  = var.environment
      SecurityZone = "all"
    }
  )
}

# Public Subnets
resource "aws_subnet" "public" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(local.subnet_configs.public.prefix, 2, count.index)
  availability_zone = var.availability_zones[count.index]

  map_public_ip_on_launch = true

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-public-${var.availability_zones[count.index]}"
      Environment  = var.environment
      Type         = "public"
      SecurityZone = "public"
    }
  )
}

# Private Subnets
resource "aws_subnet" "private" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(local.subnet_configs.private.prefix, 2, count.index)
  availability_zone = var.availability_zones[count.index]

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-private-${var.availability_zones[count.index]}"
      Environment  = var.environment
      Type         = "private"
      SecurityZone = "business"
    }
  )
}

# Database Subnets
resource "aws_subnet" "database" {
  count             = length(var.availability_zones)
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(local.subnet_configs.database.prefix, 2, count.index)
  availability_zone = var.availability_zones[count.index]

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-database-${var.availability_zones[count.index]}"
      Environment  = var.environment
      Type         = "database"
      SecurityZone = "storage"
    }
  )
}

# Internet Gateway
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-igw"
      Environment  = var.environment
      SecurityZone = "public"
    }
  )
}

# NAT Gateway with EIP
resource "aws_eip" "nat" {
  count  = length(var.availability_zones)
  domain = "vpc"

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-nat-eip-${count.index + 1}"
      Environment  = var.environment
      SecurityZone = "public"
    }
  )
}

resource "aws_nat_gateway" "main" {
  count         = length(var.availability_zones)
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-nat-${count.index + 1}"
      Environment  = var.environment
      SecurityZone = "public"
    }
  )
}

# Route Tables
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-public-rt"
      Environment  = var.environment
      SecurityZone = "public"
    }
  )
}

resource "aws_route_table" "private" {
  count  = length(var.availability_zones)
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main[count.index].id
  }

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-private-rt-${count.index + 1}"
      Environment  = var.environment
      SecurityZone = "business"
    }
  )
}

# Route Table Associations
resource "aws_route_table_association" "public" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

resource "aws_route_table_association" "database" {
  count          = length(var.availability_zones)
  subnet_id      = aws_subnet.database[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# VPC Flow Logs
resource "aws_cloudwatch_log_group" "flow_log" {
  name              = "/aws/vpc/estatekit-${var.environment}-flow-logs"
  retention_in_days = var.flow_log_retention_days

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-flow-logs"
      Environment  = var.environment
      SecurityZone = "monitoring"
    }
  )
}

resource "aws_flow_log" "main" {
  vpc_id                   = aws_vpc.main.id
  traffic_type            = "ALL"
  log_destination_type    = "cloud-watch-logs"
  log_destination         = aws_cloudwatch_log_group.flow_log.arn
  max_aggregation_interval = 60

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-vpc-flow-log"
      Environment  = var.environment
      SecurityZone = "monitoring"
    }
  )
}

# Network ACLs
resource "aws_network_acl" "public" {
  vpc_id     = aws_vpc.main.id
  subnet_ids = aws_subnet.public[*].id

  ingress {
    protocol   = -1
    rule_no    = 100
    action     = "allow"
    cidr_block = "0.0.0.0/0"
    from_port  = 0
    to_port    = 0
  }

  egress {
    protocol   = -1
    rule_no    = 100
    action     = "allow"
    cidr_block = "0.0.0.0/0"
    from_port  = 0
    to_port    = 0
  }

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-public-nacl"
      Environment  = var.environment
      SecurityZone = "public"
    }
  )
}

resource "aws_network_acl" "private" {
  vpc_id     = aws_vpc.main.id
  subnet_ids = aws_subnet.private[*].id

  ingress {
    protocol   = -1
    rule_no    = 100
    action     = "allow"
    cidr_block = var.vpc_cidr
    from_port  = 0
    to_port    = 0
  }

  egress {
    protocol   = -1
    rule_no    = 100
    action     = "allow"
    cidr_block = "0.0.0.0/0"
    from_port  = 0
    to_port    = 0
  }

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-private-nacl"
      Environment  = var.environment
      SecurityZone = "business"
    }
  )
}

resource "aws_network_acl" "database" {
  vpc_id     = aws_vpc.main.id
  subnet_ids = aws_subnet.database[*].id

  ingress {
    protocol   = "tcp"
    rule_no    = 100
    action     = "allow"
    cidr_block = local.subnet_configs.private.prefix
    from_port  = 5432
    to_port    = 5432
  }

  egress {
    protocol   = -1
    rule_no    = 100
    action     = "allow"
    cidr_block = local.subnet_configs.private.prefix
    from_port  = 0
    to_port    = 0
  }

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-database-nacl"
      Environment  = var.environment
      SecurityZone = "storage"
    }
  )
}

# VPN Gateway (if enabled)
resource "aws_vpn_gateway" "main" {
  count  = var.enable_vpn_gateway ? 1 : 0
  vpc_id = aws_vpc.main.id

  tags = merge(
    local.common_tags,
    {
      Name         = "estatekit-${var.environment}-vpn-gateway"
      Environment  = var.environment
      SecurityZone = "business"
    }
  )
}