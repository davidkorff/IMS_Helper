public class IMSCancellationSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public CancellationEngineSettings EngineSettings { get; set; }
    public DocumentSettings DocumentSettings { get; set; }
    public ValidationSettings ValidationSettings { get; set; }
    public WorkflowSettings WorkflowSettings { get; set; }
    public RefundSettings RefundSettings { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
}

public class CancellationEngineSettings
{
    public bool EnableParallelProcessing { get; set; } = true;
    public int MaxConcurrentCancellations { get; set; } = 5;
    public int CancellationTimeoutSeconds { get; set; } = 300;
    public bool RequireApprovalForMaterialChanges { get; set; } = true;
    public decimal MaterialChangeThreshold { get; set; } = 1000;
    public List<string> AutoApprovalCancellationTypes { get; set; }
    public Dictionary<string, CancellationTypeSettings> CancellationTypeConfigs { get; set; }
}

public class CancellationTypeSettings
{
    public bool RequiresApproval { get; set; }
    public bool RequiresDocuments { get; set; }
    public bool AllowsRescission { get; set; }
    public int RescissionWindowDays { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, ValidationRule> ValidationRules { get; set; }
}

public class DocumentSettings
{
    public bool CompressDocuments { get; set; } = true;
    public string StoragePath { get; set; }
    public List<string> RequiredSignatures { get; set; }
    public Dictionary<string, string> DocumentTemplates { get; set; }
    public int DocumentRetentionDays { get; set; } = 90;
    public List<string> AllowedFileTypes { get; set; }
}

public class ValidationSettings
{
    public bool StrictValidation { get; set; } = true;
    public List<string> RequiredFields { get; set; }
    public Dictionary<string, ValidationRule> CustomValidationRules { get; set; }
    public bool ValidateEffectiveDate { get; set; } = true;
    public bool ValidateRefundCalculation { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}

public class WorkflowSettings
{
    public bool EnableWorkflowEngine { get; set; } = true;
    public List<string> RequiredSteps { get; set; }
    public Dictionary<string, WorkflowStep> CustomSteps { get; set; }
    public bool AllowParallelSteps { get; set; } = false;
    public int StepTimeoutSeconds { get; set; } = 60;
    public List<string> AutoApprovalCriteria { get; set; }
}

public class RefundSettings
{
    public string DefaultCalculationMethod { get; set; } = "ProRata";
    public bool RequireRefundApproval { get; set; } = true;
    public decimal RefundApprovalThreshold { get; set; } = 5000;
    public List<string> AcceptedPaymentMethods { get; set; }
    public bool AllowRefundDeferral { get; set; } = false;
    public int RefundProcessingDays { get; set; } = 10;
    public Dictionary<string, decimal> RefundThresholds { get; set; }
}

public class ValidationRule
{
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public bool IsBlocking { get; set; }
    public int Priority { get; set; }
}

public class WorkflowStep
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> RequiredRoles { get; set; }
    public List<string> DependentSteps { get; set; }
    public bool IsOptional { get; set; }
    public int Order { get; set; }
} 