using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskPilot.API;

namespace TaskPilot.Integration.Tests.Controllers
{
    [TestClass]
    public sealed class AuthRateLimitTests
    {
        [TestMethod]
        public async Task LoginAndRegister_EnforceSeparatePerClientLimits()
        {
            await using var app = CreateApplication();
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.BaseAddress = new System.Uri("https://localhost");

            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var response = await PostInvalidRequest(client, "/api/v1/auth/login", "192.0.2.1");
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }

            using (var limitedLogin = await PostInvalidRequest(client, "/api/v1/auth/login", "192.0.2.1"))
            {
                Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedLogin.StatusCode);
                Assert.IsTrue(int.TryParse(limitedLogin.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(), out var retryAfter));
                Assert.IsTrue(retryAfter > 0);
            }

            using (var otherClientLogin = await PostInvalidRequest(client, "/api/v1/auth/login", "192.0.2.2"))
                Assert.AreEqual(HttpStatusCode.BadRequest, otherClientLogin.StatusCode);

            using (var registration = await PostInvalidRequest(client, "/api/v1/auth/register", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.BadRequest, registration.StatusCode);

            using (var limitedRegistration = await PostInvalidRequest(client, "/api/v1/auth/register", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedRegistration.StatusCode);

            using (var otherClientRegistration = await PostInvalidRequest(client, "/api/v1/auth/register", "192.0.2.2"))
                Assert.AreEqual(HttpStatusCode.BadRequest, otherClientRegistration.StatusCode);

            using (var verification = await PostInvalidRequest(client, "/api/v1/auth/verify-email", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.BadRequest, verification.StatusCode);
            using (var limitedVerification = await PostInvalidRequest(client, "/api/v1/auth/verify-email", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedVerification.StatusCode);

            using (var resend = await PostInvalidRequest(client, "/api/v1/auth/resend-verification", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.BadRequest, resend.StatusCode);
            using (var limitedResend = await PostInvalidRequest(client, "/api/v1/auth/resend-verification", "192.0.2.1"))
                Assert.AreEqual(HttpStatusCode.TooManyRequests, limitedResend.StatusCode);
        }

        private static WebApplication CreateApplication()
        {
            var settings = new Dictionary<string, string>
            {
                ["Jwt:SigningKey"] = "RateLimit_Test_Key_0123456789_ABCDEF",
                ["ConnectionStrings:TaskPilotDBConnection"] =
                    "Server=localhost;Database=TaskPilotRateLimitTests;Integrated Security=true;TrustServerCertificate=true",
                ["RateLimiting:Auth:LoginPermitLimit"] = "2",
                ["RateLimiting:Auth:RegisterPermitLimit"] = "1",
                ["RateLimiting:Auth:VerifyEmailPermitLimit"] = "1",
                ["RateLimiting:Auth:ResendVerificationPermitLimit"] = "1"
            };

            var builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = "Testing";
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(settings);

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);
            builder.Services.AddControllers().AddApplicationPart(typeof(Startup).Assembly);

            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-IP"].ToString(), out var address))
                    context.Connection.RemoteIpAddress = address;
                await next();
            });
            startup.Configure(app, app.Environment);
            return app;
        }

        private static Task<HttpResponseMessage> PostInvalidRequest(HttpClient client, string path, string ipAddress)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new { })
            };
            request.Headers.Add("X-Test-Client-IP", ipAddress);
            return client.SendAsync(request);
        }
    }
}
