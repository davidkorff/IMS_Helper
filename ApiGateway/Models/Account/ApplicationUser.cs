using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string CompanyName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string TimeZone { get; set; } = "UTC";
    
    // Navigation properties
    public virtual ICollection<UserApiKey> ApiKeys { get; set; }
    public virtual ICollection<IMSConnection> IMSConnections { get; set; }
}

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
    
    public virtual ApplicationUser User { get; set; }
}

public class UserApiKey
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string ApiKeyId { get; set; }
    
    public virtual ApplicationUser User { get; set; }
    public virtual ApiKey ApiKey { get; set; }
} 