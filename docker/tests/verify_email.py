import os
import re
import time

import requests


def main():
    api_url = os.environ["TASKPILOT_API_URL"]
    email = os.environ["TASKPILOT_EMAIL"]
    mailpit_url = os.environ.get("TASKPILOT_MAILPIT_URL", "http://localhost:8025")

    for _ in range(40):
        response = requests.get(
            f"{mailpit_url}/api/v1/search",
            params={"query": f"to:{email}"},
            timeout=10,
        )
        response.raise_for_status()
        result = response.json()
        messages = result.get("messages") or result.get("Messages") or []
        if messages:
            snippet = messages[0].get("Snippet") or messages[0].get("snippet", "")
            match = re.search(
                r"#token=([A-Za-z0-9_-]+)",
                snippet,
            )
            if match:
                verification = requests.post(
                    f"{api_url}/api/v1/auth/verify-email",
                    json={"token": match.group(1)},
                    timeout=10,
                )
                verification.raise_for_status()
                print("The CI account email was verified through Mailpit.")
                return
        time.sleep(0.25)

    raise RuntimeError(f"No verification email was captured for {email}.")


if __name__ == "__main__":
    main()
