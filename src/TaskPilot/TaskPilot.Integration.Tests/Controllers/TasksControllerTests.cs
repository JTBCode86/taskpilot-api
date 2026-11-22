using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaskPilot.Integration.Tests.Controllers
{
    [TestClass]
    public class TasksControllerTests
    {
        private const string TestPassword = "IntegrationTest_12345";

        private sealed record Session(HttpClient Client, int UserId) : IDisposable
        {
            public void Dispose() => Client.Dispose();
        }

        private static HttpClient CreateClient()
        {
            var url = Environment.GetEnvironmentVariable("TASKPILOT_INTEGRATION_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                Assert.Inconclusive("Set TASKPILOT_INTEGRATION_URL to run tests against a dedicated development environment.");
            }

            return new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) };
        }

        private static async Task<Session> CreateSession()
        {
            var client = CreateClient();
            var email = $"integration-{Guid.NewGuid():N}@example.test";
            var registration = await client.PostAsJsonAsync("/api/v1/auth/register",
                new { firstName = "Integration", lastName = "Test", email, password = TestPassword });
            Assert.AreEqual(HttpStatusCode.Created, registration.StatusCode, await registration.Content.ReadAsStringAsync());
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestPassword });
            Assert.AreEqual(HttpStatusCode.OK, login.StatusCode, await login.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", json.RootElement.GetProperty("accessToken").GetString());
            return new Session(client, json.RootElement.GetProperty("userId").GetInt32());
        }

        [TestMethod]
        public async Task CreateAndCompleteTask_IsReflectedInSummary()
        {
            using var session = await CreateSession();
            var client = session.Client;
            var title = "Integration test " + Guid.NewGuid();

            var health = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);

            var create = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = session.UserId, title });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode, await create.Content.ReadAsStringAsync());
            var id = await create.Content.ReadFromJsonAsync<int>();
            Assert.IsTrue(id > 0);

            var complete = await client.PostAsync($"/api/v1/tasks/{id}/complete", null);
            Assert.AreEqual(HttpStatusCode.NoContent, complete.StatusCode, await complete.Content.ReadAsStringAsync());

            var summary = await client.GetAsync($"/api/v1/tasks/summary/{session.UserId}");
            Assert.AreEqual(HttpStatusCode.OK, summary.StatusCode, await summary.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
            var task = json.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual(title, task.GetProperty("title").GetString());
            Assert.AreEqual("Concluída", task.GetProperty("status").GetString());
            Assert.AreNotEqual(JsonValueKind.Null, task.GetProperty("completedAt").ValueKind);
        }

        [TestMethod]
        public async Task CompleteMissingTask_ReturnsNotFound()
        {
            using var session = await CreateSession();
            var client = session.Client;
            var response = await client.PostAsync("/api/v1/tasks/-1/complete", null);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task CreateTask_WithInvalidTitle_ReturnsBadRequest(string title)
        {
            using var session = await CreateSession();
            var response = await session.Client.PostAsJsonAsync("/api/v1/tasks", new { userId = session.UserId, title });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithMissingTitle_ReturnsBadRequest()
        {
            using var session = await CreateSession();
            var response = await session.Client.PostAsJsonAsync("/api/v1/tasks", new { userId = session.UserId });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithTitleLongerThanColumn_ReturnsBadRequest()
        {
            using var session = await CreateSession();
            var response = await session.Client.PostAsJsonAsync("/api/v1/tasks", new { userId = session.UserId, title = new string('a', 257) });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithMaximumLengthTitle_PreservesTitle()
        {
            using var session = await CreateSession();
            var client = session.Client;
            var title = new string('a', 256);
            var create = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = session.UserId, title });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode, await create.Content.ReadAsStringAsync());
            var id = await create.Content.ReadFromJsonAsync<int>();

            var summary = await client.GetAsync($"/api/v1/tasks/summary/{session.UserId}");
            Assert.AreEqual(HttpStatusCode.OK, summary.StatusCode, await summary.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
            var task = json.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual(title, task.GetProperty("title").GetString());
        }

        [TestMethod]
        public async Task UnauthenticatedAndInvalidTokens_AreRejected()
        {
            using var client = CreateClient();
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/tasks/summary/1")).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/tasks/summary/1")).StatusCode);
            using var session = await CreateSession();
            var token = session.Client.DefaultRequestHeaders.Authorization.Parameter;
            var signatureStart = token.LastIndexOf('.') + 1;
            var replacement = token[signatureStart] == 'A' ? 'B' : 'A';
            var forged = token.Substring(0, signatureStart) + replacement + token.Substring(signatureStart + 1);
            session.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);
            Assert.AreEqual(HttpStatusCode.Unauthorized,
                (await session.Client.GetAsync($"/api/v1/tasks/summary/{session.UserId}")).StatusCode);
        }

        [TestMethod]
        public async Task AnotherUsersTasks_CannotBeReadCreatedOrCompleted()
        {
            using var owner = await CreateSession();
            using var other = await CreateSession();
            var created = await owner.Client.PostAsJsonAsync("/api/v1/tasks", new { title = "Private task" });
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
            var id = await created.Content.ReadFromJsonAsync<int>();

            var forged = await other.Client.PostAsJsonAsync("/api/v1/tasks", new { userId = owner.UserId, title = "Forged" });
            Assert.AreEqual(HttpStatusCode.Forbidden, forged.StatusCode);
            Assert.AreEqual(HttpStatusCode.Forbidden, (await other.Client.GetAsync($"/api/v1/tasks/summary/{owner.UserId}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await other.Client.PostAsync($"/api/v1/tasks/{id}/complete", null)).StatusCode);
            var otherSummary = await other.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{other.UserId}");
            Assert.IsFalse(otherSummary.EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id));
            var summary = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{owner.UserId}");
            var task = summary.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual("Pendente", task.GetProperty("status").GetString());
        }

        [TestMethod]
        public async Task RegistrationAndLogin_ValidateCredentialsAndDuplicates()
        {
            using var client = CreateClient();
            var email = $"integration-{Guid.NewGuid():N}@example.test";
            var registration = new { firstName = "Test", lastName = "User", email, password = TestPassword };
            Assert.AreEqual(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", registration)).StatusCode);
            var duplicate = new { firstName = "Test", lastName = "User", email = email.ToUpperInvariant(), password = TestPassword };
            Assert.AreEqual(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/auth/register", duplicate)).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "WrongPassword_12345" })).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"missing-{Guid.NewGuid():N}@example.test", password = TestPassword })).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "teste@taskpilot.com", password = TestPassword })).StatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/v1/auth/register", new { firstName = "Test", lastName = "User", email = "new@example.test", password = "short" })).StatusCode);
        }

        [TestMethod]
        public async Task Swagger_DocumentsOnlyProtectedEndpointsAsRequiringBearer()
        {
            using var client = CreateClient();
            var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
            var paths = document.GetProperty("paths");
            var register = paths.GetProperty("/api/v1/auth/register").GetProperty("post");
            Assert.IsFalse(register.TryGetProperty("security", out var registerSecurity) && registerSecurity.GetArrayLength() > 0);
            var tasks = paths.GetProperty("/api/v1/Tasks").GetProperty("post");
            Assert.IsTrue(tasks.GetProperty("security").GetArrayLength() > 0);
            var me = paths.GetProperty("/api/v1/auth/me").GetProperty("get");
            Assert.IsTrue(me.GetProperty("security").GetArrayLength() > 0);
        }

        private static async Task AssertTitleValidationError(HttpResponseMessage response)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
            Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType.MediaType);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var errors = json.RootElement.GetProperty("errors");
            Assert.IsTrue(errors.EnumerateObject().Any(
                error => string.Equals(error.Name, "title", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
