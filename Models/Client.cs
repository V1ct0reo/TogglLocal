using System.Text.Json.Serialization;

namespace TogglAnalysis.Models
{
    public class Client
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("wid")]
        public long WorkspaceId { get; set; }
    }
}
