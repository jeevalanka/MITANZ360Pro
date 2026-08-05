# AGENTS.md

## Cursor Cloud specific instructions

### What this is
`MITANZ360Pro.Web` is a single **.NET 10 Blazor Server** web app ("AI Learning Hub" / LMS + business-process platform) by MITANZ NZ Ltd. It uses ASP.NET Core Identity (EF Core + SQL Server), Radzen Blazor UI, Microsoft Graph/SharePoint, and optional AI (Azure OpenAI / OpenRouter). There is one process; the `.csproj`/`.sln` live at the repo root.

### Toolchain / setup
- The **.NET 10 SDK** is installed at `/usr/local/dotnet` and symlinked to `/usr/local/bin/dotnet` (on PATH by default). It is NOT part of the repo, so it is not reinstalled by the update script; it persists via the VM snapshot.
- The update script runs `dotnet tool restore` (restores `dotnet-ef`) and `dotnet restore` from the repo root.
- `dotnet tool restore` may print "An issue was encountered verifying workloads" — this is harmless.

### Build / run / test
- Build: `dotnet build` (root). Expect ~49 nullable/analyzer warnings and 0 errors; warnings are not treated as errors.
- Run (dev): `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://0.0.0.0:5067" dotnet run --no-launch-profile` from the repo root. Default launch profiles bind `http://localhost:5067` and `https://localhost:7164`; override `ASPNETCORE_URLS` to bind `0.0.0.0` when testing over the VM network.
- There is **no automated test project** in this repo (no `*.Tests` / test runner), so `dotnet test` has nothing to run. "Lint" is effectively the build's analyzer/nullable warnings.
- EF migrations run automatically on startup (`db.Database.MigrateAsync()` in `Program.cs`); `dotnet ef` is available via the restored local tool if manual migration work is needed.

### Non-obvious runtime notes
- **External dependencies are remote/cloud, not local.** `appsettings.json` points `DefaultConnection` at a hosted SQL Server (`sql5113.site4now.net`) and configures a live Azure AD app for Microsoft Graph/SharePoint. Startup requires the SQL Server to be reachable (migration runs on boot). No local DB/Docker is needed or provided.
- The SharePoint list validator (`SharePointListValidator.ValidateEntityListAsync`) runs on startup but **swallows exceptions** and only logs warnings (e.g. "Required index missing: field_x"), so SharePoint/Graph problems do NOT block startup.
- A **SysAdmin account is auto-seeded** on startup (`Data/DbInitializer.cs`): email `supper@mitanz360.edu`, password `P@ssw0rd`. (Password reset "change after first login" is only a comment; it is not enforced.)
- **Log in at `/xlogin`** (the app's custom login page), NOT the default Identity `/Account/Login`. `/xlogin` posts to the `/auth/login` MVC controller (`Controllers/AuthController.cs`) which cookie-signs-in and redirects to `/`. Navigating to `/Account/Login` (including the redirect for unauthenticated users hitting `/`) currently renders the router's "Page-Not-Found / Not-Authorized" fallback, so use `/xlogin` to sign in.
- This is Blazor Server: the initial HTML is a shell and real content renders over the SignalR/WebSocket circuit, so plain `curl` of a page will not show interactive content — use a browser to verify UI. `App.razor` sets `InteractiveServerRenderMode(prerender: false)`, so pages briefly show the "Page-Not-Found / Not-Authorized" placeholder for a few seconds until the circuit connects; wait ~10-15s after navigation before judging a page.
- `appsettings.json` contains committed secrets (SQL password, Azure OpenAI/OpenRouter keys, Azure AD client secret). Treat them as sensitive.
