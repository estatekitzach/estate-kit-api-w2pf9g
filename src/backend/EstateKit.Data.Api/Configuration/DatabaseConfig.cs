namespace EstateKit.Data.API.Configuration
{
    /// <summary>
    /// Configures comprehensive database settings including connection options, security,
    /// performance, and high availability features for the Data Access API
    /// </summary>
    public class DatabaseConfig
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseOptions _options;

        /// <summary>
        /// Initializes database configuration with comprehensive validation and security checks
        /// </summary>
        /// <param name="configuration">Application configuration instance</param>
        public DatabaseConfig(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = GetDatabaseOptions();
            ValidateConfiguration();
        }

        /// <summary>
        /// Configures Entity Framework database context with comprehensive connection settings
        /// and advanced features for production deployment
        /// </summary>
        public IServiceCollection ConfigureDbContext(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseNpgsql(_options.ConnectionString, npgsqlOptions =>
                {
                    // Configure connection resiliency
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);

                    // Configure connection pooling
                    npgsqlOptions.MinPoolSize(10);
                    npgsqlOptions.MaxPoolSize(1000);

                    // Configure migrations
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");

                    // Configure query splitting for optimal performance
                    npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

                    // Configure SSL mode for secure connections
                    npgsqlOptions.SslMode(SslMode.VerifyFull);
                    npgsqlOptions.TrustServerCertificate(false);

                    // Configure read replica support
                    if (_options.EnableReadReplicas)
                    {
                        npgsqlOptions.UseReplication(
                            mainNodeConnectionString: _options.ConnectionString,
                            replicaNodeConnectionStrings: _options.ReplicaConnectionStrings);
                    }
                });

                // Configure command timeout
                options.CommandTimeout(_options.CommandTimeoutSeconds);

                // Configure detailed errors for development
                if (_configuration.GetValue<bool>("EnableDetailedErrors"))
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }

                // Configure query tracking behavior
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

                // Configure database facets
                options.UseEncryption();
                options.UseAuditing();
            });

            // Configure health checks
            services.AddHealthChecks()
                .AddNpgSql(
                    _options.ConnectionString,
                    name: "postgres",
                    tags: new[] { "db", "sql", "postgres" });

            return services;
        }

        private DatabaseOptions GetDatabaseOptions()
        {
            var options = new DatabaseOptions();
            var section = _configuration.GetSection("Database");
            
            if (!section.Exists())
                throw new InvalidOperationException("Database configuration section is missing");

            section.Bind(options);
            return options;
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(_options.ConnectionString))
                throw new InvalidOperationException("Database connection string is required");

            if (_options.CommandTimeoutSeconds <= 0)
                throw new InvalidOperationException("Command timeout must be greater than 0 seconds");

            if (_options.EnableReadReplicas && 
                (_options.ReplicaConnectionStrings == null || !_options.ReplicaConnectionStrings.Any()))
                throw new InvalidOperationException("Replica connection strings are required when read replicas are enabled");
        }

        private class DatabaseOptions
        {
            public string ConnectionString { get; set; }
            public int CommandTimeoutSeconds { get; set; } = 30;
            public bool EnableReadReplicas { get; set; }
            public string[] ReplicaConnectionStrings { get; set; }
            public bool EnableDetailedErrors { get; set; }
        }
    }
}
