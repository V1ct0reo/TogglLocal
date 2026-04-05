using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TogglAnalysis.Data;
using TogglAnalysis.Models;

namespace TogglAnalysis.Services
{
    public class TogglService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _dbContext;
        private string? _apiKey;

        public TogglService(HttpClient httpClient, AppDbContext dbContext)
        {
            _httpClient = httpClient;
            _dbContext = dbContext;
            _httpClient.BaseAddress = new Uri("https://api.track.toggl.com/api/v9/");
        }

        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey;
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:api_token"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }

        public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);

        public async Task<List<Workspace>> FetchWorkspacesAsync()
        {
            var response = await _httpClient.GetAsync("workspaces");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var workspaces = JsonSerializer.Deserialize<List<Workspace>>(content);
            return workspaces ?? new List<Workspace>();
        }

        public async Task<List<Project>> FetchProjectsAsync(long workspaceId)
        {
            var response = await _httpClient.GetAsync($"workspaces/{workspaceId}/projects");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var projects = JsonSerializer.Deserialize<List<Project>>(content);
            return projects ?? new List<Project>();
        }

        public async Task<List<Client>> FetchClientsAsync(long workspaceId)
        {
            var response = await _httpClient.GetAsync($"workspaces/{workspaceId}/clients");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var clients = JsonSerializer.Deserialize<List<Client>>(content);
            return clients ?? new List<Client>();
        }

        /// <summary>
        /// Syncs time entries from Toggl for the given date range.
        /// The Toggl API (GET /me/time_entries) enforces a maximum date range of ~3 months.
        /// This method automatically splits larger ranges into 90-day chunks and falls back to the Reports API for historical data.
        /// </summary>
        public async Task SyncTimeEntriesAsync(DateTime start, DateTime end)
        {
            const int maxDaysPerChunk = 90;
            var chunkStart = start;

            while (chunkStart < end)
            {
                var chunkEnd = chunkStart.AddDays(maxDaysPerChunk);
                if (chunkEnd > end) chunkEnd = end;

                // Check if chunk is older than the API's arbitrary lookback limit
                var threshold = DateTime.UtcNow.AddDays(-80);
                bool isHistorical = chunkStart < threshold;
                Console.WriteLine($"[DEBUG] chunkStart={chunkStart:O}, threshold={threshold:O}, isHistorical={isHistorical}");

                if (isHistorical)
                {
                    var workspaces = await FetchWorkspacesAsync();
                    foreach (var ws in workspaces)
                    {
                        var payload = new
                        {
                            start_date = chunkStart.ToString("yyyy-MM-dd"),
                            end_date = chunkEnd.ToString("yyyy-MM-dd"),
                            page_size = 1000
                        };
                        var json = JsonSerializer.Serialize(payload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var url = $"https://api.track.toggl.com/reports/api/v3/workspace/{ws.Id}/search/time_entries";
                        var response = await _httpClient.PostAsync(url, content);
                        if (!response.IsSuccessStatusCode)
                        {
                            var errorBody = await response.Content.ReadAsStringAsync();
                            throw new HttpRequestException($"Reports API error {(int)response.StatusCode}: {errorBody}", null, response.StatusCode);
                        }

                        var respContent = await response.Content.ReadAsStringAsync();
                        var entries = new List<TimeEntry>();
                        using var doc = JsonDocument.Parse(respContent);
                        
                        foreach (var row in doc.RootElement.EnumerateArray())
                        {
                            long? projectId = null;
                            if (row.TryGetProperty("project_id", out var pidElem) && pidElem.ValueKind != JsonValueKind.Null)
                            {
                                projectId = pidElem.GetInt64();
                            }
                            
                            string? description = null;
                            if (row.TryGetProperty("description", out var descElem) && descElem.ValueKind != JsonValueKind.Null)
                            {
                                description = descElem.GetString();
                            }

                            if (row.TryGetProperty("time_entries", out var teArr) && teArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var te in teArr.EnumerateArray())
                                {
                                    var entry = new TimeEntry
                                    {
                                        Id = te.GetProperty("id").GetInt64(),
                                        WorkspaceId = ws.Id,
                                        ProjectId = projectId,
                                        Description = description,
                                        Start = te.GetProperty("start").GetDateTime(),
                                        Duration = te.TryGetProperty("seconds", out var secElem) ? secElem.GetInt64() : 0
                                    };
                                    
                                    if (te.TryGetProperty("stop", out var stopElem) && stopElem.ValueKind != JsonValueKind.Null)
                                    {
                                        entry.Stop = stopElem.GetDateTime();
                                    }
                                    
                                    entries.Add(entry);
                                }
                            }
                        }

                        if (entries.Any())
                        {
                            foreach (var entry in entries)
                            {
                                var existing = await _dbContext.TimeEntries.FindAsync(entry.Id);
                                if (existing == null)
                                {
                                    _dbContext.TimeEntries.Add(entry);
                                }
                                else
                                {
                                    _dbContext.Entry(existing).CurrentValues.SetValues(entry);
                                }
                            }
                            await _dbContext.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    var startStr = Uri.EscapeDataString(chunkStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    var endStr = Uri.EscapeDataString(chunkEnd.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

                    var response = await _httpClient.GetAsync($"me/time_entries?start_date={startStr}&end_date={endStr}");
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}", null, response.StatusCode);
                    }

                    var respContent = await response.Content.ReadAsStringAsync();
                    var entries = JsonSerializer.Deserialize<List<TimeEntry>>(respContent);

                    if (entries != null && entries.Any())
                    {
                        foreach (var entry in entries)
                        {
                            var existing = await _dbContext.TimeEntries.FindAsync(entry.Id);
                            if (existing == null)
                            {
                                _dbContext.TimeEntries.Add(entry);
                            }
                            else
                            {
                                _dbContext.Entry(existing).CurrentValues.SetValues(entry);
                            }
                        }
                        await _dbContext.SaveChangesAsync();
                    }
                }

                chunkStart = chunkEnd;
            }
        }

        public async Task<List<TimeEntry>> GetLocalTimeEntriesAsync(DateTime start, DateTime end)
        {
            return await _dbContext.TimeEntries
                .Where(t => t.Start >= start && t.Start <= end)
                .OrderByDescending(t => t.Start)
                .ToListAsync();
        }
    }
}
