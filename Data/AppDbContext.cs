using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TogglAnalysis.Models;

namespace TogglAnalysis.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TimeEntry> TimeEntries { get; set; }
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<Project> Projects { get; set; }
    }
}
