using EstateKit.Data.Api.Extensions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// The following line enables Application Insights telemetry collection.
builder.Services.AddApplicationInsightsTelemetry();

// Configure structured logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("EstateKit.PersonalInformationApi", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddAWSInstrumentation())
    // TODO.AddRedisInstrumentation()
    // todod: .AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
// todo:.AddProcessInstrumentation());*/


builder.Services.AddEndpointsApiExplorer();
// Add services to the container.
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContexts();
builder.Services.AddEstateKitDataApiServices(builder.Configuration);

// Add CORS
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>();
if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    throw new InvalidOperationException("Allowed origins are not configured.");
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("EstateKitPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID")
            .WithMethods("GET", "POST")
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowCredentials();
    });
});

// Configure rate limiting for 1000+ concurrent requests
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter("GlobalLimiter",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 1000,
                AutoReplenishment = true
            }));
});

// Configure JWT authentication with enhanced validation
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AWS:Cognito:Authority"];
        options.Audience = builder.Configuration["AWS:Cognito:Audience"];
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
    });


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => // UseSwaggerUI is called only in Development.
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
