Backend for Tenty
==================

Simple README with instructions to run the backend locally and with Docker.

Overview
--------
This repository contains the backend service for the Tenty project (F# / .NET). The backend provides REST endpoints, validation logic and database access for camp and feature data.

Requirements
------------
- .NET SDK 9.0 (or compatible .NET 9 SDK)
- Git (to clone the repository)
- Optional: Docker & Docker Compose (to run in containers)

Quick start (development)
-------------------------
1. Open a command prompt (cmd.exe) and go to the repository root (where `backend-tenty.sln` lives):

```cmd
cd \path\to\repo\tenty\backend\backend-tenty
```

2. Restore and build dependencies:

```cmd
dotnet restore
dotnet build
```

3. Run the backend:

```cmd
cd backend-tenty
dotnet run --project backend-tenty.fsproj
```

By default the app will read `appsettings.json` and `appsettings.Development.json` for configuration. You can change environment specific settings using environment variables or by editing `appsettings.Development.json` for local development.

Run tests
---------
From the repository root run the Tests project:

```cmd
cd Tests.test
dotnet test
```

This runs the unit tests (xUnit) found in the `Tests.test` project.

Docker (optional)
-----------------
A `Dockerfile` and `compose.yaml` are present in the repository. To run the backend with Docker Compose (from the repo root):

```cmd
docker compose up --build
```

This will build the container image and start the service. Adjust environment variables in the compose file or Docker run command as needed.

Configuration
-------------
- `appsettings.json`: main configuration
- `appsettings.Development.json`: local overrides for development

Secrets and environment variables (e.g. JWT signing keys) should be provided via environment variables or a secrets store in production. Do not commit secrets to source control.

Useful commands
---------------
- Build solution: `dotnet build backend-tenty.sln`
- Run specific project: `dotnet run --project backend-tenty/backend-tenty.fsproj`
- Run tests: `dotnet test Tests.test/Tests.test.fsproj`

Troubleshooting
---------------
- If you see SDK version errors, install .NET 9 SDK: https://dotnet.microsoft.com
- For Docker issues, ensure Docker Desktop is running and you have enough resources allocated.

Contact / Notes
---------------
If you need additional instructions (database setup, migrations, CI), tell me which environment you plan to deploy to and I can extend this README with step-by-step details.

