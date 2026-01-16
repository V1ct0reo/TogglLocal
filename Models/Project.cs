using System.Text.Json.Serialization;

namespace TogglAnalysis.Models
{
    public class Project
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("workspace_id")]
        public long WorkspaceId { get; set; }

        [JsonPropertyName("client_id")]
        public long? ClientId { get; set; }
    }
}
