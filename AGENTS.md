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

### Student Visa public portal
- Routes: `/student-visa` (optional `?r=REFCODE`) and `/student-visa/confirm?token=…` — `[AllowAnonymous]`, under `Features/List-Entities/Public/`.
- Uses generic Entity (`EntityType=Student`) + Metadata only; no Student SharePoint list / StudentRepository.
- Create-or-update by unique Metadata `Email`; Student Number from sequence (`ST######`); status defaults to `Draft`.
- Welcome email via `IGraphMailService`; verification sets `Metadata.EmailVerified` / `EmailVerifiedDate`.
- Public audit details (Who/When/IP/Browser/Operation) go into Activity `Details`. In-memory rate limit on submit.
- If SharePoint Activities list columns do not match (`Field 'Action' is not recognized`), activity writes soft-fail to logs; Entity create/update still succeeds.

### Reference Data
- Admin page: `/reference-data` (`Admin`/`SysAdmin`). Feature code under `Features/List-ReferenceData/`.
- SharePoint list GUID: `SharePoint:Lists:ReferenceData` in `appsettings.json`.
- UI → `IReferenceDataService` → `IReferenceDataRepository` → `ISharePointListClient` → Graph (never call Graph from Razor).
- Import Defaults skips duplicate `Category`+`Code`; does not overwrite.
- App consumers: Student Visa (`/student-visa`) and Entity `DynamicMetadataRenderer` load active options via `GetLookupOptionsAsync(category)` where category matches field name (Gender, Nationality, Country, PreferredCountry, etc.).
- After adding/editing Reference Data, re-open forms (5‑minute lookup cache). Re-run **Import Defaults** to seed Gender / PreferredContactMethod / EnglishTest if missing.

### Entity Documents Library
- All document code lives under `Features/List-Entities/DocumentsLibrary/`. Shared lookups: `Features/Shared/DocumentCategories.json` and `DocumentStatus.json` (do not hardcode status/category codes).
- Files are stored in the SharePoint **Documents** document library (flat — **no folders**). Relationship columns: `EntityType`, `EntityNumber` (= Entity.EntityId), `DocumentCode`, plus Status / versioning (`DocumentVersion`, `IsLatest`).
- Config: `SharePoint:Libraries:Documents` (drive id) and `SharePoint:Lists:Documents` (library list id for metadata queries). Do **not** create a separate list for file storage.
- UI → `IDocumentLibraryService` → Graph via existing `SharePointService.GraphClient` (no second SharePoint connector). Admin page: `/documents`. Entity editor/drawer embeds `EntityDocumentsPanel`.
- **Sources of truth:** Entity `Metadata.RequiredDocuments[]` = required docs; SharePoint library = uploaded files. Match by `DocumentCode`. Replace always uploads a new file and flips `IsLatest` (never overwrite).
- Download/preview API: `GET /api/entity-documents/{driveItemId}?inline=true` (authorized). LMS preview remains `/api/documents/render/{id}`.
- Permissions: pass `IsAdminMode=false` on `EntityDocumentsPanel` for student UX (upload/replace requested only; no verify/delete/metadata).
- Library list id is resolved from the Documents **drive** (`drives/{id}/list`). Do not rely on `SharePoint:Lists:Documents` if that GUID points at a non-library list.
- Optional columns (Description, Remarks, ExpiryDate, UploadedBy) are patched best-effort; core fields (Title, EntityType, EntityNumber, DocumentCode, Status, DocumentVersion, IsLatest, Active, UploadedByRole) are required for full behavior — provision them on the Documents library if missing.
- One-shot smoke: `DOC_SMOKE=1 dotnet run --no-launch-profile` runs request+upload against an existing Student and exits (see `DocumentHelloWorldSmoke.cs`).
