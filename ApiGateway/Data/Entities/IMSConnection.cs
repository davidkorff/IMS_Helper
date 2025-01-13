public class IMSConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public string ConnectionName { get; set; }
    public string Environment { get; set; }
    public string EncryptedCredentials { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public IMSConnectionStatus Status { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; }
    public virtual ICollection<IMSConnectionPermission> Permissions { get; set; }
}

public class IMSConnectionPermission
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConnectionId { get; set; }
    public string PermissionName { get; set; }
    public bool IsGranted { get; set; }
    
    public virtual IMSConnection Connection { get; set; }
} 