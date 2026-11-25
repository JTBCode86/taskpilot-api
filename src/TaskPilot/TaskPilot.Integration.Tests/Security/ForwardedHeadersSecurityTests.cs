#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskPilot.API;

namespace TaskPilot.Integration.Tests.Security
{
    [TestClass]
    public sealed class ForwardedHeadersSecurityTests
    {
        private static readonly IPAddress ClientProxyAddress = IPAddress.Parse("192.0.2.10");

        [TestMethod]
        public async Task ProductionRequest_FromConfiguredProxyGetsHsts()
        {
            await AssertHstsHeaderAsync("192.0.2.10", expected: true);
        }

        [TestMethod]
        public async Task ProductionRequest_FromUnconfiguredProxyDoesNotTrustForwardedScheme()
        {
            await AssertHstsHeaderAsync("192.0.2.20", expected: false);
        }

        [TestMethod]
        public void InvalidConfiguredProxyAddressFailsFast()
        {
            var error = Assert.ThrowsException<InvalidOperationException>(
                () => CreateApplication("not-an-ip-address", "taskpilot.test"));

            StringAssert.Contains(error.Message, "ForwardedHeaders:KnownProxies");
        }

        [TestMethod]
        public void ProductionWithWildcardAllowedHostsFailsFast()
        {
            var error = Assert.ThrowsException<InvalidOperationException>(
                () => CreateApplication("192.0.2.10", "*"));

            StringAssert.Contains(error.Message, "AllowedHosts");
        }

        private static async Task AssertHstsHeaderAsync(string configuredProxy, bool expected)
        {
            await using var app = CreateApplication(configuredProxy, "taskpilot.test");
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.BaseAddress = new Uri("http://taskpilot.test");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
            request.Headers.Add("X-Forwarded-Proto", "https");

            using var response = await client.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(expected, response.Headers.Contains("Strict-Transport-Security"));
            if (expected)
                Assert.AreEqual("max-age=31536000", response.Headers.GetValues("Strict-Transport-Security").First());
        }

        private static WebApplication CreateApplication(string configuredProxy, string allowedHosts)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "Forwarded_Headers_Test_Key_0123456789",
                ["ConnectionStrings:TaskPilotDBConnection"] =
                    "Server=localhost;Database=TaskPilotForwardedHeadersTests;Integrated Security=true;TrustServerCertificate=true",
                ["ForwardedHeaders:KnownProxies:0"] = configuredProxy,
                ["AllowedHosts"] = allowedHosts
            };

            var builder = WebApplication.CreateBuilder();
            builder.Environment.EnvironmentName = "Production";
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(settings);

            var startup = new Startup(builder.Configuration);
            startup.ConfigureServices(builder.Services);
            builder.Services.Configure<HealthCheckServiceOptions>(options =>
                options.Registrations.Clear());
            builder.Services.AddControllers().AddApplicationPart(typeof(Startup).Assembly);

            var app = builder.Build();
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = ClientProxyAddress;
                return next();
            });
            startup.Configure(app, app.Environment);
            return app;
        }
    }
}
