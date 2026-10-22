using Kreddit.api3.Models;
using Microsoft.EntityFrameworkCore;

namespace Kreddit.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>()
            .HasOne(p => p.User);

        modelBuilder.Entity<Post>()
            .HasMany(p => p.Comments);

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User);
    }
}