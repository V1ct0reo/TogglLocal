# Local Database Documentation

This document describes the current structure, update mechanism, and conflict resolution strategy for the local SQLite database (`toggl.db`) used in the TogglAnalysis application.

## 1. Local Database Structure

The application uses Entity Framework Core (EF Core) with a local SQLite database file named `toggl.db`. The primary context class managing this connection is `AppDbContext`, which extends `IdentityDbContext` to support user authentication.

### Core Tables

*   **`AspNetUsers` (Identity):** Manages user accounts for the application.
    *   *Custom Additions:* Includes a `TogglApiKey` column so that each user can securely store their own Toggl API credentials locally.
*   **`TimeEntries`:** Stores the raw time tracking data fetched from Toggl.
    *   `Id` (Primary Key): The original unique ID provided by Toggl.
    *   `Description`: The task name/description.
    *   `Start` / `Stop`: Timestamps for the start and end of the entry.
    *   `Duration`: Total duration in seconds (running entries have a negative duration based on the start epoch).
    *   `WorkspaceId`: The ID of the structural entity defining where the time was tracked.
    *   `ProjectId`: (Optional) The ID of the specific project the time was categorized under.
*   **`Workspaces`:** Represents Toggl workspaces (the top-level organizational unit).
    *   `Id` (Primary Key), `Name`.
*   **`Projects`:** Represents Toggl projects.
    *   `Id` (Primary Key), `Name`, `WorkspaceId`, `ClientId`.
*   **`Clients`:** Represents Toggl clients.
    *   `Id` (Primary Key), `Name`, `WorkspaceId`.

---

## 2. When and How is Local Data Updated?

Currently, data synchronization is triggered **manually** by the user via the user interface.

1.  **Trigger:** On the Dashboard (`Index.razor`), the user clicks the "Sync Data (Last 30 Days)" button.
2.  **Service Call:** The UI invokes `TogglService.SyncTimeEntriesAsync(start, end)`, passing in the current UTC date minus 30 days as the `start` parameter, and the current UTC date as `end`.
3.  **API Fetch:** The service makes an HTTP GET request to the Toggl API (`me/time_entries`) using the user's stored API Key (Basic Auth).
4.  **Parsing:** The JSON response is deserialized into a list of `TimeEntry` C# objects.

*Note: Workspaces, Projects, and Clients are currently fetched separately when viewing the Analysis page and are not aggressively synced to the local DB in the same automated manner as time entries yet.*

---

## 3. Handling Duplicates on Overlapping Windows

Because users can click "Sync Data" multiple times, the time windows (the last 30 days) will frequently overlap. It is critical that we don't insert duplicate time entries. 

This is handled seamlessly by ensuring that our local primary key for the `TimeEntries` table corresponds *exactly* to Toggl's unique `id` field.

**The Upsert Strategy in `TogglService.cs`:**
```csharp
foreach (var entry in entries)
{
    // 1. Attempt to find the entry in our local DB by its Toggl ID
    var existing = await _dbContext.TimeEntries.FindAsync(entry.Id);
    
    if (existing == null)
    {
        // 2a. If it doesn't exist, safely insert it as a brand new record
        _dbContext.TimeEntries.Add(entry);
    }
    else
    {
        // 2b. If it ALREADY exists, update the local record's fields
        // This ensures changes made on Toggl.com (like changing a description 
        // or stopping a running timer) are reflected locally without duplication.
        _dbContext.Entry(existing).CurrentValues.SetValues(entry);
    }
}
// 3. Commit the transaction
await _dbContext.SaveChangesAsync();
```

Because of this "Find and Update, else Insert" strategy (Upsert), overlapping time windows will never create new rows for previously synced entries. Instead, they simply refresh the local database with the most up-to-date information from Toggl.
