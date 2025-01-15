using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.AspNetCore.Authorization;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Polly;
using Polly.CircuitBreaker;
using EstateKit.Core.Entities;
using EstateKit.Business.API.Services;

namespace EstateKit.Business.API.GraphQL.Subscriptions
{
    /// <summary>
    /// Provides real-time GraphQL subscriptions for document processing events with comprehensive
    /// security, monitoring, and error handling capabilities.
    /// </summary>
    [ExtendObjectType("Subscription")]
    [Authorize(Policy = "DocumentSubscriptionPolicy")]
    public class DocumentSubscriptions
    {
        private readonly ITopicEventReceiver _eventReceiver;
        private readonly ILogger<DocumentSubscriptions> _logger;
        private readonly ITelemetryClient _telemetryClient;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        private readonly IAsyncPolicy _retryPolicy;

        private const string ProcessingStatusTopic = "DocumentProcessing_{0}";
        private const string OcrCompletionTopic = "DocumentOcr_{0}";
        private const int MaxConcurrentSubscriptions = 1000;
        private const int RetryCount = 3;

        public DocumentSubscriptions(
            ITopicEventReceiver eventReceiver,
            ILogger<DocumentSubscriptions> logger,
            ITelemetryClient telemetryClient)
        {
            _eventReceiver = eventReceiver ?? throw new ArgumentNullException(nameof(eventReceiver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            // Configure resilience policies
            _circuitBreaker = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, duration) =>
                    {
                        _logger.LogError(ex, 
                            "Circuit breaker opened for document subscriptions. Duration: {Duration}s",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset for document subscriptions");
                    });

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    RetryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception,
                            "Retry {RetryCount} for subscription after {Delay}ms. Context: {@Context}",
                            retryCount,
                            timeSpan.TotalMilliseconds,
                            context);
                    });
        }

        /// <summary>
        /// Subscription for document processing status changes with comprehensive security and monitoring
        /// </summary>
        [Subscribe]
        [Topic]
        [RateLimit(MaxRequests = 100, TimeWindow = 60)]
        [Authorize(Policy = "DocumentAccessPolicy")]
        public async IAsyncEnumerable<Document> OnDocumentProcessingStatusChanged(
            Guid documentId,
            [Service] ITopicEventReceiver eventReceiver)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>(
                "DocumentProcessingSubscription");
            operation.Telemetry.Properties["DocumentId"] = documentId.ToString();

            try
            {
                _logger.LogInformation(
                    "Starting document processing subscription for document {DocumentId}",
                    documentId);

                var topic = string.Format(ProcessingStatusTopic, documentId);
                
                await foreach (var update in eventReceiver
                    .SubscribeAsync<Document>(topic)
                    .WithCancellation(operation.Telemetry.Context.Operation.Id)
                    .ConfigureAwait(false))
                {
                    if (update.Id != documentId)
                    {
                        _logger.LogWarning(
                            "Received update for incorrect document. Expected: {ExpectedId}, Actual: {ActualId}",
                            documentId,
                            update.Id);
                        continue;
                    }

                    _telemetryClient.TrackEvent(
                        "DocumentProcessingUpdate",
                        new Dictionary<string, string>
                        {
                            ["DocumentId"] = documentId.ToString(),
                            ["ProcessingStatus"] = update.ProcessingStatus
                        });

                    yield return update;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in document processing subscription for document {DocumentId}",
                    documentId);
                operation.Telemetry.Success = false;
                throw;
            }
        }

        /// <summary>
        /// Subscription for OCR completion events with enhanced monitoring and error handling
        /// </summary>
        [Subscribe]
        [Topic]
        [RateLimit(MaxRequests = 50, TimeWindow = 60)]
        [Authorize(Policy = "DocumentAccessPolicy")]
        public async IAsyncEnumerable<Document> OnDocumentOcrCompleted(
            Guid documentId,
            [Service] ITopicEventReceiver eventReceiver)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>(
                "DocumentOcrSubscription");
            operation.Telemetry.Properties["DocumentId"] = documentId.ToString();

            try
            {
                _logger.LogInformation(
                    "Starting OCR completion subscription for document {DocumentId}",
                    documentId);

                var topic = string.Format(OcrCompletionTopic, documentId);

                await foreach (var update in eventReceiver
                    .SubscribeAsync<Document>(topic)
                    .WithCancellation(operation.Telemetry.Context.Operation.Id)
                    .ConfigureAwait(false))
                {
                    if (!update.IsProcessed)
                    {
                        continue;
                    }

                    // Track OCR completion metrics
                    _telemetryClient.TrackEvent(
                        "DocumentOcrCompleted",
                        new Dictionary<string, string>
                        {
                            ["DocumentId"] = documentId.ToString(),
                            ["HasMetadata"] = (!string.IsNullOrEmpty(update.Metadata)).ToString()
                        });

                    yield return update;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in OCR completion subscription for document {DocumentId}",
                    documentId);
                operation.Telemetry.Success = false;
                throw;
            }
        }
    }
}