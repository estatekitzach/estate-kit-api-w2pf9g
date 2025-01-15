# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution files and Directory.Build.props
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Business.API/Business.API.csproj", "Business.API/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]

# Restore packages with security scanning
RUN dotnet restore "Business.API/Business.API.csproj" \
    --disable-parallel \
    && dotnet tool install --global security-scan \
    && security-scan "Business.API/Business.API.csproj"

# Copy remaining source code
COPY . .

# Build and test
RUN dotnet build "Business.API/Business.API.csproj" -c Release --no-restore \
    && dotnet test "Business.API/Business.API.csproj" -c Release --no-build \
    && dotnet publish "Business.API/Business.API.csproj" \
        -c Release \
        --no-build \
        -o /app/publish \
        /p:UseAppHost=false \
        /p:DebugType=None \
        /p:DebugSymbols=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Set security labels
LABEL maintainer="EstateKit Development Team" \
      description="GraphQL Business Logic API for EstateKit Personal Information System" \
      version="1.0.0" \
      security.scan-date="{{BUILD_DATE}}" \
      vendor="EstateKit" \
      com.estatekit.component="business-api" \
      com.estatekit.version="{{VERSION}}"

# Create non-root user
RUN addgroup --system --gid 1000 app \
    && adduser --system --uid 1000 --ingroup app app

# Set working directory and copy published files
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .

# Configure environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:80 \
    AWS_REGION=us-east-1 \
    TZ=UTC \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    COMPlus_EnableDiagnostics=0

# Set up health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

# Configure security
RUN chmod -R 500 /app \
    && chown -R app:app /app \
    && mkdir -p /tmp/cache /var/log \
    && chown -R app:app /tmp/cache /var/log

# Set up read-only filesystem and temporary storage
VOLUME ["/tmp", "/var/log"]

# Expose port
EXPOSE 80

# Switch to non-root user
USER app:app

# Set security options
SECURITY_OPTS --security-opt=no-new-privileges:true \
              --security-opt=seccomp=unconfined \
              --cap-drop=ALL

# Start application
ENTRYPOINT ["dotnet", "EstateKit.Business.API.dll"]