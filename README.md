# EstateKit Personal Information API

Enterprise-grade personal information management system for secure estate planning, featuring a GraphQL Business Logic API and REST Data Access API architecture.

## Overview

EstateKit Personal Information API is a comprehensive system designed to securely manage and process sensitive personal information for estate planning purposes. The system implements field-level encryption, OAuth2 authentication, and complete audit logging while maintaining strict security protocols and regulatory compliance.

### Key Features

- **Dual API Architecture**
  - GraphQL Business Logic API (.NET Core 9, Hot Chocolate 13)
  - REST Data Access API (.NET Core 9, Entity Framework Core 10)

- **Enterprise Security**
  - Field-level encryption with EstateKit Encryption Service
  - OAuth 2.0 authentication with AWS Cognito
  - Comprehensive audit logging
  - TLS 1.3 with perfect forward secrecy

- **Document Management**
  - Secure document storage with AWS S3
  - OCR processing via AWS Textract
  - Version control and audit trails
  - Encrypted document storage

- **High Availability**
  - Multi-AZ deployment on AWS
  - Kubernetes orchestration with EKS
  - Automated failover and disaster recovery
  - Cross-region replication

## Architecture

The system follows a distributed architecture pattern with two primary components:

### Business Logic API (GraphQL)
- Handles all client interactions
- Processes document uploads and OCR
- Manages business rules and validation
- Integrates with AWS services

### Data Access API (REST)
- Provides exclusive database access
- Manages data encryption/decryption
- Enforces security protocols
- Handles data persistence

## Prerequisites

- .NET Core SDK 9.0
- Docker Desktop
- AWS Account with required services:
  - EKS (Kubernetes)
  - RDS (PostgreSQL 15)
  - S3
  - Cognito
  - CloudWatch

## Quick Start

1. **Clone the Repository**
```bash
git clone https://github.com/your-org/estatekit-api.git
cd estatekit-api
```

2. **Configure Environment**
```bash
cp .env.example .env
# Edit .env with your configuration
```

3. **Build and Run with Docker**
```bash
docker-compose up --build
```

4. **Access APIs**
- GraphQL API: https://localhost:5001/graphql
- REST API: https://localhost:5002/api/v1

## Development Setup

1. **Install Dependencies**
```bash
dotnet restore
```

2. **Setup Database**
```bash
dotnet ef database update
```

3. **Run Locally**
```bash
dotnet run --project src/EstateKit.BusinessApi
dotnet run --project src/EstateKit.DataApi
```

## Deployment

The system uses a GitOps approach with Azure DevOps pipelines:

1. **Build Pipeline**
- Runs unit tests
- Performs security scans
- Builds Docker images
- Pushes to container registry

2. **Deployment Pipeline**
- Uses Helm charts
- Supports multiple environments
- Implements canary deployments
- Includes rollback capabilities

## Security

### Authentication
- OAuth 2.0 with JWT tokens
- AWS Cognito integration
- MFA support
- Token-based session management

### Data Protection
- Field-level encryption
- TLS 1.3 for transport
- AWS KMS for key management
- Database encryption at rest

### Compliance
- GDPR compliant
- SOC 2 certified
- HIPAA ready
- PCI DSS compatible

## Documentation

- [API Documentation](docs/api/README.md)
- [Security Guide](docs/security/README.md)
- [Deployment Guide](docs/deployment/README.md)
- [Development Guide](docs/development/README.md)

## Monitoring

- CloudWatch integration
- Custom metrics
- Real-time alerting
- Performance monitoring
- Security event tracking

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

Copyright © 2024 EstateKit. All rights reserved.

## Support

For support and inquiries:
- Email: support@estatekit.com
- Documentation: https://docs.estatekit.com
- Issue Tracker: https://github.com/your-org/estatekit-api/issues