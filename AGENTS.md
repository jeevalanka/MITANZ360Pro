# AGENTS.md

## Cursor Cloud specific instructions

### What this is
`MITANZ360Pro.Web` is a single **.NET 10 Blazor Server** web app ("AI Learning Hub" / LMS + business-process platform). One process; `.csproj`/`.sln` at repo root.

### Toolchain / setup
- .NET 10 SDK on PATH. Update script: `dotnet tool restore` then `dotnet restore`.
- Build: `dotnet build` (nullable/analyzer warnings expected; 0 errors).
- Run (dev): `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://0.0.0.0:5067" dotnet run --no-launch-profile`.
- No automated test project. Login at **`/xlogin`** (not `/Account/Login`). Seeded SysAdmin: `supper@mitanz360.edu` / `P@ssw0rd`.
- Blazor Server with `prerender: false` — wait ~10–15s after navigation for the circuit; `curl` will not show interactive UI.
- External deps (SQL Server, Graph/SharePoint) are remote; SQL Server must be reachable for startup migration.

### Entity feature location
All Entity feature code and templates live under **`Features/List-Entities/`** only (pages, services, models, templates, validators). Shared SharePoint list client stays in `Infrastructure/SharePoint/`.

---

## Project UI / dialog conventions (reuse across features)

Apply these patterns for modals, drawers, and action forms project-wide:

1. **Windows / dialogs**
   - Use **rounded cards** (`border-radius` ~12px).
   - Dialog must sit **on top of all screens** (`position: fixed` overlay, high `z-index`).
   - **Responsive**: auto-adjust width/height on mobile; body scrolls inside the card; overlay stays full-viewport.

2. **Chrome**
   - **Close (X) icon**: always **top-right** of the card header.
   - **Primary action buttons** (Save/Create/Delete confirm): **bottom-right** footer.
   - Secondary actions that belong in the header toolbar (e.g. New/Refresh/Export): **top-right**, inline with the title row.

3. **Buttons**
   - Consistent hover: brand blue hover (`#106ebe` over `#0078d4`) for primary; slight darken for secondary.
   - Prefer Fluent-like Segoe UI styling.

4. **Audit Information**
   - Show in a **colored card** (soft blue tint), not plain text block.
   - **Created By / Modified By** must use this app’s **login identity** (email from Identity / `UserSessionService`), **not** Office 365 / SharePoint Graph app-only person fields.
   - Persist app audit users in Entity `MetadataJson` keys `__CreatedBy` / `__ModifiedBy` (reserved; skipped by template validation).

5. **Entity editor specifics**
   - Entity Type is editable on Edit; switching type reloads type-gated metadata.
   - Saving after a type change requires **confirmation** that previous type-specific metadata will be removed.
   - **Delete** icon is available; delete only when the record is **not referenced** by other records, with **confirmation**. Otherwise block with a clear message.

6. **Lists / grids**
   - Never hide the DataGrid behind an empty-state that prevents `LoadData` from running; load on init and keep the grid mounted; show empty state as an overlay/message after load.
