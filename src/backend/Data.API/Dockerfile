# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first to optimize layer caching
COPY ["Data.API/Data.API.csproj", "Data.API/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["EstateKit.snk", "."]

# Restore packages with security scanning
RUN dotnet restore "Data.API/Data.API.csproj" \
    --disable-parallel \
    /p:RestoreUseSkipNonexistentTargets=false \
    /p:Configuration=Release \
    && dotnet tool install --global security-scan

# Copy remaining source code
COPY . .

# Build with security flags
RUN dotnet build "Data.API/Data.API.csproj" \
    -c Release \
    -o /app/build \
    /p:UseSharedCompilation=false \
    /p:TreatWarningsAsErrors=true \
    /p:EnableNETAnalyzers=true \
    /p:AnalysisLevel=latest \
    /p:AnalysisMode=All

# Run security scanning
RUN security-scan /app/build

# Publish
RUN dotnet publish "Data.API/Data.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true \
    /p:PublishSingleFile=true \
    /p:DebugType=None \
    /p:DebugSymbols=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user
RUN addgroup --system --gid 65532 nonroot && \
    adduser --system --uid 65532 --ingroup nonroot nonroot

# Set up readonly root filesystem and security configurations
RUN chmod 755 /app && \
    chown nonroot:nonroot /app

# Copy published files from build stage
COPY --from=build --chown=nonroot:nonroot /app/publish .

# Create volume mount points
RUN mkdir -p /tmp /data && \
    chown -R nonroot:nonroot /tmp /data && \
    chmod 755 /tmp /data

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:80 \
    TZ=UTC \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Configure health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

# Set container metadata
LABEL maintainer="EstateKit Team" \
      description="Data Access API container for EstateKit Personal Information System" \
      version="1.0.0" \
      security.hardened="true" \
      com.estatekit.component="data-api"

# Expose API port
EXPOSE 80

# Set up volume mounts
VOLUME ["/tmp", "/data"]

# Switch to non-root user
USER nonroot:nonroot

# Set security options
RUN setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet

# Set entry point
ENTRYPOINT ["dotnet", "EstateKit.Data.API.dll"]

# Apply security options
SECURITY_OPT ["no-new-privileges:true", "seccomp=unconfined"]