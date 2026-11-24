#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskPilot.API;

namespace TaskPilot.Integration.Tests.Observability
{
    [TestClass]
    public sealed class ObservabilityTests
    {
        [TestMethod]
        public async Task HealthEndpoints_SeparateLivenessFromReadinessAndKeepLegacyEndpoint()
        {
            using var loggerProvider = new CapturingLoggerProvider();
            await using var app = CreateApplication(loggerProvider);
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");

            using var liveness = await client.GetAsync("/health/live");
            using var readiness = await client.GetAsync("/health/ready");
            using var legacyHealth = await client.GetAsync("/health");

            Assert.AreEqual(HttpStatusCode.OK, liveness.StatusCode);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, legacyHealth.StatusCode);
        }

        [TestMethod]
        public async Task RequestLogging_RecordsSafeStructuredRequestFields()
        {
            using var loggerProvider = new CapturingLoggerProvider();
            await using var app = CreateApplication(loggerProvider);
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");

            using var response = await client.GetAsync("/health/live?access_token=must-not-be-logged");

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var requestLog = loggerProvider.Entries.Single(entry =>
                entry.Category == typeof(TaskPilot.API.Middleware.RequestLoggingMiddleware).FullName);

            Assert.AreEqual("GET", requestLog.Properties["HttpMethod"]);
            Assert.AreEqual("/health/live", requestLog.Properties["Route"]);
            Assert.AreEqual(200, requestLog.Properties["StatusCode"]);
            Assert.IsTrue(Convert.ToDouble(requestLog.Properties["ElapsedMilliseconds"]) >= 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(requestLog.Properties["TraceId"]?.ToString()));
            Assert.IsFalse(requestLog.Message.Contains("must-not-be-logged", StringComparison.Ordinal));
            Assert.IsFalse(requestLog.Message.Contains("access_token", StringComparison.Ordinal));
        }

        private static WebApplication CreateApplication(CapturingLoggerProvider loggerProvider)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "Observability_Test_Key_0123456789_ABCDEF",
                ["ConnectionStrings:TaskPilotDBConnection"] =
                    "Server=localhost;Database=TaskPilotObservabilityTests;Integrated Security=true;TrustServerCertificate=true"
            };

            var builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = "Testing";
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(loggerProvider);

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);
            builder.Services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new HealthCheckRegistration(
                    "database",
                    _ => new FixedHealthCheck(HealthCheckResult.Unhealthy("database unavailable")),
                    HealthStatus.Unhealthy,
                    new[] { "ready" }));
            });
            builder.Services.AddControllers().AddApplicationPart(typeof(Startup).Assembly);

            var app = builder.Build();
            startup.Configure(app, app.Environment);
            return app;
        }

        private sealed class FixedHealthCheck : IHealthCheck
        {
            private readonly HealthCheckResult _result;

            public FixedHealthCheck(HealthCheckResult result)
            {
                _result = result;
            }

            public Task<HealthCheckResult> CheckHealthAsync(
                HealthCheckContext context,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_result);
            }
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<CapturedLog> _entries = new ConcurrentQueue<CapturedLog>();

            public IReadOnlyCollection<CapturedLog> Entries => _entries.ToArray();

            public ILogger CreateLogger(string categoryName)
            {
                return new CapturingLogger(categoryName, _entries);
            }

            public void Dispose()
            {
            }

            private sealed class CapturingLogger : ILogger
            {
                private readonly string _category;
                private readonly ConcurrentQueue<CapturedLog> _entries;

                public CapturingLogger(string category, ConcurrentQueue<CapturedLog> entries)
                {
                    _category = category;
                    _entries = entries;
                }

                public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                {
                    return null;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return logLevel >= LogLevel.Information;
                }

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception? exception,
                    Func<TState, Exception?, string> formatter)
                {
                    if (!IsEnabled(logLevel))
                        return;

                    var properties = new Dictionary<string, object?>();
                    if (state is IEnumerable<KeyValuePair<string, object?>> values)
                    {
                        foreach (var value in values)
                            properties[value.Key] = value.Value;
                    }

                    _entries.Enqueue(new CapturedLog(
                        _category,
                        properties,
                        formatter(state, exception)));
                }
            }
        }

        private sealed record CapturedLog(
            string Category,
            IReadOnlyDictionary<string, object?> Properties,
            string Message);
    }
}
