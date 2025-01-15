using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit; // v2.5.0
using FluentAssertions; // v6.12.0
using Microsoft.AspNetCore.TestHost; // v9.0.0
using Microsoft.Extensions.DependencyInjection; // v9.0.0
using NBomber.Contracts; // v5.0.0
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using EstateKit.Core.Entities;
using EstateKit.Core.Enums;
using EstateKit.Business.API.GraphQL.Queries;
using EstateKit.Business.API.GraphQL.Mutations;

namespace EstateKit.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration test suite for validating end-to-end API functionality,
    /// security, and performance requirements between Business.API and Data.API
    /// </summary>
    [Collection("IntegrationTests")]
    public class ApiIntegrationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly TestServer _businessApiServer;
        private readonly TestServer _dataApiServer;
        private readonly HttpClient _businessApiClient;
        private readonly HttpClient _dataApiClient;
        private readonly ITestUserGenerator _testUserGenerator;
        private readonly IPerformanceValidator _performanceValidator;
        private readonly string _testAuthToken;
        private readonly Guid _testUserId;

        public ApiIntegrationTests(
            ITestOutputHelper output,
            ITestUserGenerator testUserGenerator,
            IPerformanceValidator performanceValidator)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _testUserGenerator = testUserGenerator ?? throw new ArgumentNullException(nameof(testUserGenerator));
            _performanceValidator = performanceValidator ?? throw new ArgumentNullException(nameof(performanceValidator));
            
            // Initialize test servers
            _businessApiServer = new TestServer(new WebHostBuilder()
                .UseStartup<Business.API.Startup>()
                .ConfigureTestServices(services =>
                {
                    services.AddTestAuthentication();
                    services.AddTestEncryption();
                }));

            _dataApiServer = new TestServer(new WebHostBuilder()
                .UseStartup<Data.API.Startup>()
                .ConfigureTestServices(services =>
                {
                    services.AddTestAuthentication();
                    services.AddTestEncryption();
                }));

            _businessApiClient = _businessApiServer.CreateClient();
            _dataApiClient = _dataApiServer.CreateClient();
            _testAuthToken = "test-jwt-token";
            _testUserId = Guid.NewGuid();

            // Configure authentication headers
            _businessApiClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _testAuthToken);
            _dataApiClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _testAuthToken);
        }

        public async Task InitializeAsync()
        {
            // Set up test data
            await _testUserGenerator.CreateTestUserAsync(_testUserId);
        }

        public async Task DisposeAsync()
        {
            // Clean up test data
            await _testUserGenerator.CleanupTestUserAsync(_testUserId);
            _businessApiClient.Dispose();
            _dataApiClient.Dispose();
            _businessApiServer.Dispose();
            _dataApiServer.Dispose();
        }

        [Fact]
        public async Task TestUserQueryEndToEndAsync()
        {
            // Arrange
            var performanceTimer = new PerformanceTimer();
            var testUser = await _testUserGenerator.GenerateTestUserAsync();
            var graphQLQuery = @"
                query GetUser($id: ID!) {
                    user(id: $id) {
                        id
                        contact {
                            firstName
                            lastName
                            contactMethods {
                                type
                                value
                            }
                        }
                        documents {
                            type
                            isProcessed
                        }
                        identifiers {
                            type
                            value
                            issuingAuthority
                        }
                    }
                }";

            // Act
            performanceTimer.Start();
            
            // Create user through Data API
            var createResponse = await _dataApiClient.PostAsJsonAsync(
                "api/v1/users",
                testUser);
            createResponse.EnsureSuccessStatusCode();
            
            // Query user through Business API GraphQL
            var queryResponse = await _businessApiClient.PostAsJsonAsync(
                "graphql",
                new { query = graphQLQuery, variables = new { id = testUser.Id } });
            queryResponse.EnsureSuccessStatusCode();
            
            var elapsedMs = performanceTimer.Stop();

            // Assert
            // Verify response time meets performance requirements
            elapsedMs.Should().BeLessThan(3000, "API response time should be under 3 seconds");

            var queryResult = await queryResponse.Content.ReadFromJsonAsync<GraphQLResponse<UserQueryResult>>();
            queryResult.Should().NotBeNull();
            queryResult.Errors.Should().BeNull();
            
            // Verify data consistency
            var returnedUser = queryResult.Data.User;
            returnedUser.Id.Should().Be(testUser.Id);
            returnedUser.Contact.Should().NotBeNull();
            returnedUser.Contact.FirstName.Should().Be(testUser.Contact.FirstName);
            returnedUser.Contact.LastName.Should().Be(testUser.Contact.LastName);

            // Verify field-level encryption
            returnedUser.Identifiers.Should().AllSatisfy(identifier =>
            {
                identifier.Value.Should().NotBeNullOrEmpty("Identifier values should be decrypted");
                identifier.Value.Should().NotContain("ENCRYPTED:", "Values should be properly decrypted");
            });

            _output.WriteLine($"End-to-end test completed in {elapsedMs}ms");
        }

        [Fact]
        public async Task TestConcurrentUsersAsync()
        {
            // Arrange
            var scenario = Scenario.Create("concurrent_users_test", async context =>
            {
                var response = await _businessApiClient.GetAsync("api/v1/health");
                return response.IsSuccessStatusCode
                    ? Response.Ok()
                    : Response.Fail();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: 1000,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromMinutes(1)
                )
            );

            // Act
            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithTestName("API Concurrent Users Test")
                .WithReportFileName("concurrent_users_report")
                .Run();

            // Assert
            stats.AllOkCount.Should().BeGreaterOrEqual(1000, 
                "System should handle 1000 concurrent users");
            
            stats.ScenarioStats[0].Ok.RPS.Should().BeGreaterOrEqual(1000,
                "System should process at least 1000 requests per second");
            
            stats.ScenarioStats[0].Ok.Latency.Percent99.Should().BeLessThan(3000,
                "99th percentile latency should be under 3 seconds");

            _output.WriteLine($"Concurrent users test completed. RPS: {stats.ScenarioStats[0].Ok.RPS}");
        }

        [Fact]
        public async Task TestUserMutationEndToEndAsync()
        {
            // Arrange
            var performanceTimer = new PerformanceTimer();
            var createUserInput = new CreateUserInput
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = "1980-01-01",
                MaritalStatus = "Single",
                AuditLogger = _testUserGenerator.GetAuditLogger(),
                SecurityValidator = _testUserGenerator.GetSecurityValidator()
            };

            var graphQLMutation = @"
                mutation CreateUser($input: CreateUserInput!) {
                    createUser(input: $input) {
                        id
                        contact {
                            firstName
                            lastName
                        }
                        dateOfBirth
                        maritalStatus
                    }
                }";

            // Act
            performanceTimer.Start();
            
            // Create user through Business API GraphQL
            var mutationResponse = await _businessApiClient.PostAsJsonAsync(
                "graphql",
                new { query = graphQLMutation, variables = new { input = createUserInput } });
            mutationResponse.EnsureSuccessStatusCode();
            
            // Verify user in Data API
            var createdUser = await mutationResponse.Content.ReadFromJsonAsync<GraphQLResponse<UserMutationResult>>();
            var getUserResponse = await _dataApiClient.GetAsync($"api/v1/users/{createdUser.Data.CreateUser.Id}");
            
            var elapsedMs = performanceTimer.Stop();

            // Assert
            elapsedMs.Should().BeLessThan(3000, "API response time should be under 3 seconds");
            
            getUserResponse.EnsureSuccessStatusCode();
            var userData = await getUserResponse.Content.ReadFromJsonAsync<User>();
            
            userData.Should().NotBeNull();
            userData.Contact.FirstName.Should().Be(createUserInput.FirstName);
            userData.Contact.LastName.Should().Be(createUserInput.LastName);
            userData.DateOfBirth.Should().Be(createUserInput.DateOfBirth);
            userData.MaritalStatus.Should().Be(createUserInput.MaritalStatus);

            _output.WriteLine($"Mutation test completed in {elapsedMs}ms");
        }

        private class GraphQLResponse<T>
        {
            public T Data { get; set; }
            public List<GraphQLError> Errors { get; set; }
        }

        private class GraphQLError
        {
            public string Message { get; set; }
        }

        private class UserQueryResult
        {
            public User User { get; set; }
        }

        private class UserMutationResult
        {
            public User CreateUser { get; set; }
        }

        private class PerformanceTimer
        {
            private DateTime _startTime;

            public void Start() => _startTime = DateTime.UtcNow;

            public double Stop() => (DateTime.UtcNow - _startTime).TotalMilliseconds;
        }
    }
}