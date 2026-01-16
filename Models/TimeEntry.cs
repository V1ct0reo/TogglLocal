using System;
using System.Text.Json.Serialization;

namespace TogglAnalysis.Models
{
    public class TimeEntry
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("start")]
        public DateTime Start { get; set; }

        [JsonPropertyName("stop")]
        public DateTime? Stop { get; set; }

        [JsonPropertyName("duration")]
        public long Duration { get; set; }

        [JsonPropertyName("workspace_id")]
        public long WorkspaceId { get; set; }

        [JsonPropertyName("project_id")]
        public long? ProjectId { get; set; }
        
        public TimeSpan GetDuration()
        {
            if (Duration < 0)
            {
                // Running entry
                return DateTime.UtcNow - Start;
            }
            return TimeSpan.FromSeconds(Duration);
        }
    }
}
