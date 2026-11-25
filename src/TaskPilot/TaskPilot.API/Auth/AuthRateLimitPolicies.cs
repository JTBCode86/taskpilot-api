using System;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace TaskPilot.API.Auth
{
    internal static class AuthRateLimitPolicies
    {
        public const string Login = "auth-login";
        public const string Register = "auth-register";
        public const string VerifyEmail = "auth-verify-email";
        public const string ResendVerification = "auth-resend-verification";

        public static void Configure(RateLimiterOptions options, IConfiguration configuration)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            var loginPermitLimit = GetPermitLimit(configuration, "LoginPermitLimit");
            var registerPermitLimit = GetPermitLimit(configuration, "RegisterPermitLimit");
            var verifyEmailPermitLimit = GetPermitLimit(configuration, "VerifyEmailPermitLimit");
            var resendVerificationPermitLimit = GetPermitLimit(configuration, "ResendVerificationPermitLimit");

            options.AddPolicy(Login, context => CreateClientPolicy(context, loginPermitLimit));
            options.AddPolicy(Register, context => CreateClientPolicy(context, registerPermitLimit));
            options.AddPolicy(VerifyEmail, context => CreateClientPolicy(context, verifyEmailPermitLimit));
            options.AddPolicy(ResendVerification, context => CreateClientPolicy(context, resendVerificationPermitLimit));
        }

        private static int GetPermitLimit(IConfiguration configuration, string name)
        {
            var key = $"RateLimiting:Auth:{name}";
            if (!int.TryParse(configuration[key], NumberStyles.None, CultureInfo.InvariantCulture, out var limit)
                || limit <= 0)
            {
                throw new InvalidOperationException($"{key} deve ser um número inteiro positivo.");
            }

            return limit;
        }

        private static RateLimitPartition<string> CreateClientPolicy(HttpContext context, int permitLimit)
        {
            var clientAddress = context.Connection.RemoteIpAddress;
            if (clientAddress?.IsIPv4MappedToIPv6 == true)
                clientAddress = clientAddress.MapToIPv4();

            var partitionKey = clientAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
        }
    }
}
