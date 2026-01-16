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

        public async Task SyncTimeEntriesAsync(DateTime start, DateTime end)
        {
            var startStr = start.ToString("yyyy-MM-dd");
            var endStr = end.ToString("yyyy-MM-dd");
            
            var response = await _httpClient.GetAsync($"me/time_entries?start_date={startStr}&end_date={endStr}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var entries = JsonSerializer.Deserialize<List<TimeEntry>>(content);

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
                        // Update existing
                        _dbContext.Entry(existing).CurrentValues.SetValues(entry);
                    }
                }
                await _dbContext.SaveChangesAsync();
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
