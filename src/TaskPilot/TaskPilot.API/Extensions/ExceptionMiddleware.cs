// TaskPilot.API/Extensions/ExceptionMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;

namespace TaskPilot.API.Extensions
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
                _logger.LogError(ex, "Um erro inesperado ocorreu. TraceId: {TraceId}", traceId);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500

            // Estrutura básica de erro
            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "Ocorreu um erro interno do servidor. Tente novamente mais tarde."
                // Detalhes da exceção (apenas em ambiente de desenvolvimento)
                // Detail = exception.Message 
            };

            // Serializa o objeto anônimo para uma string JSON (compatível com .NET Core 3.1)
            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(jsonResponse);
        }
    }
}