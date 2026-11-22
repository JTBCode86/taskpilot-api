import os
import secrets
import subprocess

import requests


def reset_password(user_id, password, confirmation, should_succeed):
    result = subprocess.run(
        ["docker", "compose", "run", "--rm", "--no-deps", "-T", "api", "reset-password", str(user_id)],
        input=f"{password}\n{confirmation}\n",
        capture_output=True,
        text=True,
    )
    if password in result.stdout or password in result.stderr:
        raise RuntimeError("The password appeared in the recovery command output.")
    if (result.returncode == 0) != should_succeed:
        raise RuntimeError(f"Unexpected recovery result: {result.stdout}\n{result.stderr}")


def login(url, password):
    response = requests.post(
        f"{url}/api/v1/auth/login",
        json={"email": "teste@taskpilot.com", "password": password},
        timeout=10,
    )
    return response


def main():
    url = os.environ["TASKPILOT_INTEGRATION_URL"]
    old_password = secrets.token_urlsafe(24)
    new_password = secrets.token_urlsafe(24)
    reset_password(1, old_password, old_password, True)
    response = login(url, old_password)
    response.raise_for_status()
    old_token = response.json()["accessToken"]
    old_headers = {"Authorization": f"Bearer {old_token}"}
    response = requests.post(
        f"{url}/api/v1/tasks", json={"title": "Recovery test task"},
        headers=old_headers, timeout=10,
    )
    response.raise_for_status()
    task_id = response.json()

    reset_password(1, new_password, old_password, False)
    reset_password(2147483647, new_password, new_password, False)
    response = requests.get(f"{url}/api/v1/auth/me", headers=old_headers, timeout=10)
    response.raise_for_status()

    reset_password(1, new_password, new_password, True)
    if requests.get(f"{url}/api/v1/auth/me", headers=old_headers, timeout=10).status_code != 401:
        raise RuntimeError("The old token was not invalidated.")
    if login(url, old_password).status_code != 401:
        raise RuntimeError("The old password was not invalidated.")
    response = login(url, new_password)
    response.raise_for_status()
    headers = {"Authorization": f"Bearer {response.json()['accessToken']}"}
    response = requests.get(f"{url}/api/v1/tasks/summary/1", headers=headers, timeout=10)
    response.raise_for_status()
    if not any(item["id"] == task_id and item["title"] == "Recovery test task" for item in response.json()):
        raise RuntimeError("The existing task was not preserved.")
    print("Password recovery preserved tasks and invalidated old credentials.")


if __name__ == "__main__":
    main()
