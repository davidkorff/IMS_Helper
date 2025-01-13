public class IMSConnectionDetails
{
    public string Id { get; set; }
    public string ConnectionName { get; set; }
    public string Environment { get; set; }
    public IMSCredentials Credentials { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public IMSConnectionStatus Status { get; set; }
    public Dictionary<string, bool> Permissions { get; set; }
}

public class IMSCredentials
{
    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }

    [Required]
    public string AccountId { get; set; }

    [Required]
    public string Url { get; set; }
}

public class CreateIMSConnectionRequest
{
    [Required]
    [StringLength(100)]
    public string ConnectionName { get; set; }

    [Required]
    [RegularExpression("^(Production|Test|Development)$")]
    public string Environment { get; set; }

    [Required]
    public IMSCredentials Credentials { get; set; }
}

public class UpdateIMSConnectionRequest
{
    [StringLength(100)]
    public string ConnectionName { get; set; }
    public IMSCredentials Credentials { get; set; }
    public bool? IsActive { get; set; }
}

public class IMSConnectionResponse
{
    public string Id { get; set; }
    public string ConnectionName { get; set; }
    public string Environment { get; set; }
    public string AccountId { get; set; }
    public string Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public IMSConnectionStatus Status { get; set; }
    public Dictionary<string, bool> Permissions { get; set; }
}

public enum IMSConnectionStatus
{
    Pending,
    Active,
    Failed,
    Disabled
}

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public Dictionary<string, bool> Permissions { get; set; }
    public IMSConnectionStatus Status { get; set; }
} 