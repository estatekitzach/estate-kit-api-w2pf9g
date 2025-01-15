using Microsoft.Extensions.DependencyInjection; // Version 9.0.0
using HotChocolate; // Version 13.0.0
using HotChocolate.Types; // Version 13.0.0
using HotChocolate.AspNetCore; // Version 13.0.0
using HotChocolate.Authorization; // Version 13.0.0
using EstateKit.Business.API.GraphQL.Types;
using EstateKit.Business.API.GraphQL.Queries;
using EstateKit.Business.API.GraphQL.Mutations;
using Microsoft.AspNetCore.Builder;
using System;

namespace EstateKit.Business.API.Configuration
{
    /// <summary>
    /// Configures GraphQL services with comprehensive security, performance optimization,
    /// and proper request handling for the Business Logic API.
    /// </summary>
    public static class GraphQLConfig
    {
        /// <summary>
        /// Adds and configures GraphQL services with security features and performance optimizations
        /// </summary>
        public static IServiceCollection AddGraphQLServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services
                .AddGraphQLServer()
                // Configure schema and types
                .AddQueryType(d => d.Name("Query"))
                    .AddTypeExtension<AssetQueries>()
                .AddMutationType(d => d.Name("Mutation"))
                    .AddTypeExtension<AssetMutations>()
                .AddType<AssetType>()
                
                // Security configurations
                .AddAuthorization()
                .AddHttpRequestInterceptor(async (context, executor, builder, ct) =>
                {
                    // Add security headers
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Add("X-Frame-Options", "DENY");
                    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                    await default;
                })
                
                // Performance optimizations
                .AddMaxExecutionDepthRule(10)
                .ModifyOptions(options =>
                {
                    options.UseXmlDocumentation = true;
                    options.SortFieldsByName = true;
                    options.RemoveUnreachableTypes = true;
                    options.StrictValidation = true;
                })
                
                // Caching configuration
                .AddCaching(options =>
                {
                    options.EnableQueryCaching = true;
                    options.CacheSize = 1000;
                    options.DefaultExpirationTimeSpan = TimeSpan.FromMinutes(5);
                })
                
                // Error handling
                .AddErrorFilter<GraphQLErrorFilter>()
                .AddDiagnosticEventListener<GraphQLDiagnosticsEventListener>()
                
                // Query validation and complexity analysis
                .AddValidationRule<MaxComplexityValidationRule>()
                .AddValidationRule<MaxTokenValidationRule>()
                .SetMaxAllowedComplexity(1000)
                
                // Request batching configuration
                .ModifyRequestOptions(options =>
                {
                    options.IncludeExceptionDetails = false;
                    options.ExecutionTimeout = TimeSpan.FromSeconds(30);
                    options.MaxRequestSize = 2 * 1024 * 1024; // 2MB
                    options.MaxQueryDepth = 15;
                })
                
                // Subscription support
                .AddInMemorySubscriptions()
                
                // Monitoring and metrics
                .AddApolloTracing()
                .AddTelemetry();

            return services;
        }

        /// <summary>
        /// Configures the GraphQL endpoint with security middleware and performance optimizations
        /// </summary>
        public static void ConfigureGraphQLEndpoint(this IApplicationBuilder app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.UseWebSockets();

            app.UseGraphQL("/graphql")
                .WithOptions(new GraphQLServerOptions
                {
                    Tool = {
                        Enable = true,
                        DisableTelemetry = true,
                        RequireLocalhost = true
                    },
                    EnableSchemaRequests = false,
                    EnableGetRequests = false,
                    EnableMultipartRequests = true,
                    AllowedMimeTypes = { "application/json" },
                })
                .WithWebSockets("/graphql")
                .WithRouting();

            // Configure CORS for GraphQL endpoint
            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("X-Request-Id"));

            // Add rate limiting middleware
            app.UseRateLimiting(options =>
            {
                options.GeneralRules = new[]
                {
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Period = "1m",
                        Limit = 1000
                    }
                };
            });
        }
    }

    /// <summary>
    /// Custom error filter for GraphQL error handling
    /// </summary>
    public class GraphQLErrorFilter : IErrorFilter
    {
        public IError OnError(IError error)
        {
            return error.WithMessage(error.Exception?.Message ?? error.Message)
                       .WithCode(error.Exception?.GetType().Name ?? "UnknownError");
        }
    }

    /// <summary>
    /// Custom diagnostics listener for GraphQL monitoring
    /// </summary>
    public class GraphQLDiagnosticsEventListener : IDiagnosticEventListener
    {
        public bool EnableMetrics => true;

        public void OnError(IQueryRequestError error)
        {
            // Log error metrics
        }

        public void OnRequest(IRequestExecutor requestExecutor)
        {
            // Log request metrics
        }

        public void OnValidationError(IRequestValidationError error)
        {
            // Log validation error metrics
        }
    }
}