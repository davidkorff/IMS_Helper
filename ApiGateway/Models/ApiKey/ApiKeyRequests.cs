public class CreateApiKeyRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [RegularExpression("^(Production|Staging|Development)$")]
    public string Environment { get; set; }

    [Required]
    public List<string> Permissions { get; set; }

    [Range(1, 10000)]
    public int RateLimit { get; set; } = 1000;

    public DateTime? ExpiresAt { get; set; }
}

public class ApiKeyListRequest
{
    public string Environment { get; set; }
    public ApiKeyStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ApiKeyResponse
{
    public string Id { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public string Environment { get; set; }
    public List<string> Permissions { get; set; }
    public int RateLimit { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApiKeyStatus Status { get; set; }
} 