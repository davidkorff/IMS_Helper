public class IMSClaimsSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public ClaimValidationSettings ValidationSettings { get; set; }
    public DocumentSettings DocumentSettings { get; set; }
    public PaymentSettings PaymentSettings { get; set; }
    public NotificationSettings NotificationSettings { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
}

public class ClaimValidationSettings
{
    public bool RequireInitialDocuments { get; set; } = false;
    public bool RequireAdjusterAssignment { get; set; } = true;
    public decimal MaxInitialReserve { get; set; } = 100000;
    public int MaxDocumentsPerUpload { get; set; } = 10;
    public List<string> RequiredContactTypes { get; set; }
    public Dictionary<string, List<string>> RequiredFields { get; set; }
}

public class DocumentSettings
{
    public List<string> AllowedFileTypes { get; set; }
    public int MaxFileSizeMB { get; set; } = 10;
    public bool CompressFiles { get; set; } = true;
    public string StoragePath { get; set; }
    public Dictionary<string, string> RequiredMetadata { get; set; }
}

public class PaymentSettings
{
    public decimal MaxPaymentAmount { get; set; } = 50000;
    public bool RequireApproval { get; set; } = true;
    public List<string> AllowedPaymentTypes { get; set; }
    public Dictionary<string, decimal> PaymentLimits { get; set; }
    public List<string> ApproverRoles { get; set; }
}

public class NotificationSettings
{
    public bool EnableEmailNotifications { get; set; } = true;
    public bool EnableSmsNotifications { get; set; } = false;
    public List<string> NotificationRecipients { get; set; }
    public Dictionary<string, string> NotificationTemplates { get; set; }
} 