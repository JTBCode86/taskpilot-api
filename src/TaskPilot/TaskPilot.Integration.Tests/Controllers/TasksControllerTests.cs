using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaskPilot.Integration.Tests.Controllers
{
    [TestClass]
    public class TasksControllerTests
    {
        private static HttpClient CreateClient()
        {
            var url = Environment.GetEnvironmentVariable("TASKPILOT_INTEGRATION_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                Assert.Inconclusive("Set TASKPILOT_INTEGRATION_URL to run tests against a dedicated development environment.");
            }

            return new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) };
        }

        [TestMethod]
        public async Task CreateAndCompleteTask_IsReflectedInSummary()
        {
            using var client = CreateClient();
            var title = "Integration test " + Guid.NewGuid();

            var health = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);

            var create = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = 1, title });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode, await create.Content.ReadAsStringAsync());
            var id = await create.Content.ReadFromJsonAsync<int>();
            Assert.IsTrue(id > 0);

            var complete = await client.PostAsync($"/api/v1/tasks/{id}/complete", null);
            Assert.AreEqual(HttpStatusCode.NoContent, complete.StatusCode, await complete.Content.ReadAsStringAsync());

            var summary = await client.GetAsync("/api/v1/tasks/summary/1");
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
            using var client = CreateClient();
            var response = await client.PostAsync("/api/v1/tasks/-1/complete", null);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task CreateTask_WithInvalidTitle_ReturnsBadRequest(string title)
        {
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = 1, title });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithMissingTitle_ReturnsBadRequest()
        {
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = 1 });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithTitleLongerThanColumn_ReturnsBadRequest()
        {
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = 1, title = new string('a', 257) });
            await AssertTitleValidationError(response);
        }

        [TestMethod]
        public async Task CreateTask_WithMaximumLengthTitle_PreservesTitle()
        {
            using var client = CreateClient();
            var title = new string('a', 256);
            var create = await client.PostAsJsonAsync("/api/v1/tasks", new { userId = 1, title });
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode, await create.Content.ReadAsStringAsync());
            var id = await create.Content.ReadFromJsonAsync<int>();

            var summary = await client.GetAsync("/api/v1/tasks/summary/1");
            Assert.AreEqual(HttpStatusCode.OK, summary.StatusCode, await summary.Content.ReadAsStringAsync());
            using var json = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
            var task = json.RootElement.EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == id);
            Assert.AreEqual(title, task.GetProperty("title").GetString());
        }

        [TestMethod]
        public async Task CreateTask_WithMissingUser_ReturnsNotFound()
        {
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/v1/tasks", new { userId = int.MaxValue, title = "Tarefa" });
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, await response.Content.ReadAsStringAsync());
            Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType.MediaType);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.AreEqual(404, json.RootElement.GetProperty("status").GetInt32());
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
