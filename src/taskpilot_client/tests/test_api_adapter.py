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

    @patch("infrastructure.api_adapter.requests.get")
    def test_summary_parses_api_dates_with_seven_fractional_digits(self, get):
        get.return_value.json.return_value = [{
            "id": 42,
            "title": "Task",
            "status": "Concluída",
            "createdAt": "2026-11-22T23:52:20.1234567",
            "dueDate": "2026-11-29T23:52:20.1234567Z",
        }]

        tasks = TaskApiAdapter("https://example.test", "token").get_all_tasks(1)

        self.assertEqual(1, len(tasks))
        self.assertTrue(tasks[0].is_completed)
        self.assertEqual(123456, tasks[0].created_at.microsecond)
        self.assertEqual(123456, tasks[0].due_date.microsecond)

    @patch("infrastructure.api_adapter.requests.get")
    def test_summary_parses_short_fraction_and_offset(self, get):
        get.return_value.json.return_value = [{
            "id": 43,
            "title": "Task",
            "status": "Pendente",
            "createdAt": "2026-11-22T23:52:20.1+03:00",
            "dueDate": None,
        }]

        tasks = TaskApiAdapter("https://example.test", "token").get_all_tasks(1)

        self.assertEqual(1, len(tasks))
        self.assertEqual(100000, tasks[0].created_at.microsecond)
        self.assertEqual(10800, tasks[0].created_at.utcoffset().total_seconds())
        self.assertIsNone(tasks[0].due_date)

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
