using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TaskPilot.API.Observability
{
    internal static class OpenTelemetryServiceCollectionExtensions
    {
        public static IServiceCollection AddTaskPilotOpenTelemetry(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var serviceName = configuration["OTEL_SERVICE_NAME"] ?? "TaskPilot.API";
            var otlpEnabled = configuration.GetValue<bool>("Observability:Otlp:Enabled");

            var openTelemetry = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName));

            openTelemetry.WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("System.Net.Http");

                if (otlpEnabled)
                    metrics.AddOtlpExporter();
            });

            openTelemetry.WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddProcessor(new DatabaseQueryTextRedactingProcessor());

                if (otlpEnabled)
                    tracing.AddOtlpExporter();
            });

            return services;
        }

        private sealed class DatabaseQueryTextRedactingProcessor : BaseProcessor<Activity>
        {
            public override void OnEnd(Activity activity)
            {
                var sensitiveTags = activity.TagObjects
                    .Where(tag =>
                        tag.Key.StartsWith("db.query.", StringComparison.Ordinal) ||
                        tag.Key == "db.statement" ||
                        tag.Key == "db.stored_procedure.name")
                    .Select(tag => tag.Key)
                    .ToArray();

                foreach (var tag in sensitiveTags)
                    activity.SetTag(tag, null);
            }
        }
    }
}
