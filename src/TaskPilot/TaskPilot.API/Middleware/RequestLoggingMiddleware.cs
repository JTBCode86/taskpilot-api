using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace TaskPilot.API.Middleware
{
    public sealed class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var statusCode = StatusCodes.Status200OK;

            try
            {
                await _next(context);
                statusCode = context.Response.StatusCode;
            }
            catch (Exception)
            {
                statusCode = StatusCodes.Status500InternalServerError;
                throw;
            }
            finally
            {
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";

                _logger.LogInformation(
                    "HTTP request {HttpMethod} {Route} responded {StatusCode} in {ElapsedMilliseconds} ms. TraceId: {TraceId}",
                    context.Request.Method,
                    route,
                    statusCode,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    traceId);
            }
        }
    }
}
