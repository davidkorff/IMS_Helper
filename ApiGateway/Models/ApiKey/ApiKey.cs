using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccountId { get; set; }
    public string KeyHash { get; set; }
    public string Name { get; set; }
    public string Environment { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int RateLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public ApiKeyStatus Status { get; set; }
    
    // Only populated when key is first generated
    [JsonIgnore]
    public string PlaintextKey { get; set; }
}

public enum ApiKeyStatus
{
    Active,
    Revoked,
    Expired
}

public class ApiKeyOptions
{
    public string Name { get; set; }
    public string Environment { get; set; }
    public List<string> Permissions { get; set; } = new();
    public int RateLimit { get; set; } = 1000;
    public DateTime? ExpiresAt { get; set; }
} 