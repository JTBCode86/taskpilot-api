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

        [TestMethod]
        public async Task UpdateAndDeleteCompletedTask_PreservesCompletionAndRemovesTask()
        {
            using var session = await CreateSession();
            var client = session.Client;
            var create = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "Original" });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);
            var id = await create.Content.ReadFromJsonAsync<int>();
            Assert.AreEqual(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/tasks/{id}/complete", null)).StatusCode);
            var before = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{session.UserId}");
            var completedAt = before.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id).GetProperty("completedAt").GetString();

            var update = await client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Atualizada", description = "Detalhes", priorityLevel = 3 });
            Assert.AreEqual(HttpStatusCode.OK, update.StatusCode, await update.Content.ReadAsStringAsync());
            var updatedResponse = await update.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual(id, updatedResponse.GetProperty("id").GetInt32());
            var summary = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{session.UserId}");
            var task = summary.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual("Atualizada", task.GetProperty("title").GetString());
            Assert.AreEqual("Detalhes", task.GetProperty("description").GetString());
            Assert.AreEqual("High", task.GetProperty("priority").GetString());
            Assert.AreEqual("Concluída", task.GetProperty("status").GetString());
            Assert.IsNotNull(completedAt);
            Assert.AreEqual(completedAt, task.GetProperty("completedAt").GetString());

            Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/tasks/{id}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/tasks/{id}")).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Nova", priorityLevel = 1 })).StatusCode);
            var after = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{session.UserId}");
            Assert.IsFalse(after.EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id));
        }

        [TestMethod]
        public async Task UpdateTask_RejectsInvalidTitleAndPriority()
        {
            using var session = await CreateSession();
            var create = await session.Client.PostAsJsonAsync("/api/v1/tasks", new { title = "Original" });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);
            var id = await create.Content.ReadFromJsonAsync<int>();
            Assert.AreEqual(HttpStatusCode.BadRequest, (await session.Client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = "   ", priorityLevel = 1 })).StatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest, (await session.Client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = new string('a', 257), priorityLevel = 1 })).StatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest, (await session.Client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Válida", priorityLevel = 4 })).StatusCode);
            var summary = await session.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{session.UserId}");
            Assert.AreEqual("Original", summary.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id).GetProperty("title").GetString());
            var maximumTitle = new string('a', 256);
            Assert.AreEqual(HttpStatusCode.OK, (await session.Client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = maximumTitle, description = (string)null, priorityLevel = 1 })).StatusCode);
            summary = await session.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{session.UserId}");
            var updated = summary.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual(maximumTitle, updated.GetProperty("title").GetString());
            Assert.AreEqual(JsonValueKind.Null, updated.GetProperty("description").ValueKind);
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
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/tasks/1", new { title = "Alterada", priorityLevel = 1 })).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/tasks/1")).StatusCode);
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
            Assert.AreEqual(HttpStatusCode.NotFound, (await other.Client.PutAsJsonAsync($"/api/v1/tasks/{id}", new { title = "Alterada", priorityLevel = 1 })).StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, (await other.Client.DeleteAsync($"/api/v1/tasks/{id}")).StatusCode);
            var otherSummary = await other.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{other.UserId}");
            Assert.IsFalse(otherSummary.EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id));
            var summary = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/summary/{owner.UserId}");
            var task = summary.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual("Pendente", task.GetProperty("status").GetString());
            Assert.AreEqual("Private task", task.GetProperty("title").GetString());
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
            var taskById = paths.GetProperty("/api/v1/Tasks/{taskId}");
            Assert.IsTrue(taskById.GetProperty("put").GetProperty("security").GetArrayLength() > 0);
            Assert.IsTrue(taskById.GetProperty("delete").GetProperty("security").GetArrayLength() > 0);
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
