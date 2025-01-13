using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApiGateway.Data.Configuration;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<IMSConnection> IMSConnections { get; set; }
    public DbSet<IMSConnectionPermission> IMSConnectionPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new IMSConnectionConfiguration());
        builder.ApplyConfiguration(new IMSConnectionPermissionConfiguration());
    }
} 