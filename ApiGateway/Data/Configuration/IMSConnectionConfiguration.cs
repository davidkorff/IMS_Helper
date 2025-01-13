using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApiGateway.Models.IMS;

public class IMSConnectionConfiguration : IEntityTypeConfiguration<IMSConnection>
{
    public void Configure(EntityTypeBuilder<IMSConnection> builder)
    {
        builder.ToTable("IMSConnections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ConnectionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Environment)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.EncryptedCredentials)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.User)
            .WithMany(u => u.IMSConnections)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Permissions)
            .WithOne(p => p.Connection)
            .HasForeignKey(p => p.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class IMSConnectionPermissionConfiguration : IEntityTypeConfiguration<IMSConnectionPermission>
{
    public void Configure(EntityTypeBuilder<IMSConnectionPermission> builder)
    {
        builder.ToTable("IMSConnectionPermissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PermissionName)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.ConnectionId, x.PermissionName })
            .IsUnique();
    }
} 