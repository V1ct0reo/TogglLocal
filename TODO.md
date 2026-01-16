# Toggl Analysis Project - Next Steps

## 🏁 Completed Today
- [x] **API Key Handling**: Added button to load keys from environment variables (`TOGGL_API_KEY` or `TOGGL_API_TOKEN`).
- [x] **Identity Setup**: Installed ASP.NET Core Identity and configured SQLite backend.
- [x] **Database Migration**: Added Identity tables to `toggl.db`.
- [x] **Security**: Locked down all pages with `[Authorize]` and implemented a Login/Logout flow.
- [x] **User Management**: Created a `/users` page for creating/deleting users.
- [x] **Seeding**: Added default `admin`/`admin` user for initial setup.

## 🚀 Priority 1: Data Isolation & Ownership
- [ ] **Model Update**: Add `OwnerId` to `TimeEntry.cs` and `Project.cs` to link data to specific users.
- [ ] **Schema Migration**: Run `dotnet ef migrations add AddUserOwnership` to update the database.
- [ ] **Service Refactor**: Update `TogglService.cs` to:
    - Inject the current logged-in user's context.
    - Save data with the correct `OwnerId`.
    - Filter all `GET` requests to only show data owned by the user.
- [ ] **API Key Persistence**: Move API keys from session memory to the `AspNetUsers` table (`TogglApiKey` field).

## 📊 Priority 2: Enhanced Analysis & Metadata
- [ ] **Metadata Sync**: Update sync logic to save `Projects`, `Clients`, and `Workspaces` to the local database instead of fetching them live every time.
- [ ] **Reporting Engine**: Implement a service to calculate totals (e.g., hours per project/client per week).
- [ ] **Visualizations**: Integrate a library like `ChartJs.Blazor` to create visual dashboards.

## 🛠️ Priority 3: Advanced Features
- [ ] **Sharing Permissions**: Create a table to manage read-only access between users ("View As" functionality).
- [ ] **Multi-Source Timeline**: Research and plan integration of other data sources (e.g., Google Calendar, GitHub) into the timeline.
- [ ] **Encrypted Storage**: Implement encryption for the Toggl API keys stored in the database.

---
*Created on: 2026-01-16*
