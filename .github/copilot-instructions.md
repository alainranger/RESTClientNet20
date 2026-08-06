# Copilot Instructions

## Project Guidelines
- In this repo, integration tests must use the Web API base address from TestSettings.ApiBaseUrl instead of hardcoding or replacing it with ad-hoc local endpoints.
- For this test suite, tests should be implemented with the local TestHttpServer harness rather than relying on external API endpoints via TestSettings.