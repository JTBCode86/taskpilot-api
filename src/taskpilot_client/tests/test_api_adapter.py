import unittest
from unittest.mock import patch

from infrastructure.api_adapter import TaskApiAdapter
from domain.entities import CreateTaskCommand


class TaskApiAdapterTests(unittest.TestCase):
    @patch("infrastructure.api_adapter.requests.get")
    def test_summary_uses_default_tls_verification_and_timeout(self, get):
        get.return_value.json.return_value = []

        result = TaskApiAdapter("https://example.test", "token").get_all_tasks(1)

        self.assertEqual([], result)
        get.assert_called_once_with(
            "https://example.test/api/v1/Tasks/summary/1",
            headers={"Authorization": "Bearer token"}, timeout=10,
        )

    @patch("infrastructure.api_adapter.requests.post")
    def test_complete_uses_default_tls_verification_and_timeout(self, post):
        post.return_value.status_code = 204

        self.assertTrue(TaskApiAdapter("https://example.test", "token").complete_task(2, 1))
        post.assert_called_once_with(
            "https://example.test/api/v1/Tasks/2/complete",
            headers={"Authorization": "Bearer token"}, timeout=10,
        )

    @patch("infrastructure.api_adapter.requests.post")
    def test_create_sends_the_bearer_token(self, post):
        post.return_value.status_code = 201
        post.return_value.text = "42"
        command = CreateTaskCommand(title="Task", description=None, due_date=None, user_id=1)

        self.assertEqual(42, TaskApiAdapter("https://example.test", "token").create_task(command))
        post.assert_called_once_with(
            "https://example.test/api/v1/Tasks",
            json={"title": "Task", "description": None, "dueDate": None, "userId": 1},
            headers={"Authorization": "Bearer token"}, timeout=10,
        )


if __name__ == "__main__":
    unittest.main()
