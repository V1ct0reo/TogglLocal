using Microsoft.AspNetCore.Identity;

namespace TogglAnalysis.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? TogglApiKey { get; set; }
    }
}
