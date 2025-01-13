using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class UsageDbContext : DbContext
{
    public UsageDbContext(DbContextOptions<UsageDbContext> options)
        : base(options)
    {
    }

    public DbSet<UsageRecord> UsageRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsageRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ApiKey);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}

public class UsageRecord
{
    public int Id { get; set; }
    public string ApiKey { get; set; }
    public string Endpoint { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; }
} 