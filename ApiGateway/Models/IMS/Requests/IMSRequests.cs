using System.ComponentModel.DataAnnotations;

public class QuoteRequest
{
    [Required(ErrorMessage = "Insured ID is required")]
    public string InsuredId { get; set; }

    [Required(ErrorMessage = "Effective date is required")]
    [DataType(DataType.Date)]
    [FutureDate(ErrorMessage = "Effective date must be in the future")]
    public DateTime EffectiveDate { get; set; }

    [Required(ErrorMessage = "Coverage type is required")]
    public string CoverageType { get; set; }

    [Required(ErrorMessage = "At least one coverage limit is required")]
    [MinLength(1, ErrorMessage = "At least one coverage limit is required")]
    public List<CoverageLimit> Limits { get; set; }
}

public class InsuredRequest
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Address is required")]
    public Address Address { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string Phone { get; set; }
}

public class PolicyRequest
{
    [Required(ErrorMessage = "Quote ID is required")]
    public string QuoteId { get; set; }

    [Required(ErrorMessage = "Effective date is required")]
    [DataType(DataType.Date)]
    [FutureDate(ErrorMessage = "Effective date must be in the future")]
    public DateTime EffectiveDate { get; set; }

    [Required(ErrorMessage = "Payment plan is required")]
    public string PaymentPlan { get; set; }

    [Required(ErrorMessage = "At least one document is required")]
    [MinLength(1, ErrorMessage = "At least one document is required")]
    public List<DocumentInfo> Documents { get; set; }
}

public class DocumentRequest
{
    [Required(ErrorMessage = "Document type is required")]
    public string Type { get; set; }

    [Required(ErrorMessage = "Filename is required")]
    [StringLength(255, ErrorMessage = "Filename cannot exceed 255 characters")]
    public string Filename { get; set; }

    [Required(ErrorMessage = "Content is required")]
    [MinLength(1, ErrorMessage = "Content cannot be empty")]
    public byte[] Content { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}

public class SubmissionRequest
{
    [Required(ErrorMessage = "Insured ID is required")]
    public string InsuredId { get; set; }

    [Required(ErrorMessage = "Submission type is required")]
    public string Type { get; set; }

    [Required(ErrorMessage = "Underwriter is required")]
    public string Underwriter { get; set; }

    [Required(ErrorMessage = "At least one document is required")]
    [MinLength(1, ErrorMessage = "At least one document is required")]
    public List<DocumentInfo> Documents { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string Notes { get; set; }
}

public class ClaimRequest
{
    [Required(ErrorMessage = "Policy ID is required")]
    public string PolicyId { get; set; }

    [Required(ErrorMessage = "Loss date is required")]
    [DataType(DataType.Date)]
    [PastDate(ErrorMessage = "Loss date must be in the past")]
    public DateTime LossDate { get; set; }

    [Required(ErrorMessage = "Loss type is required")]
    public string LossType { get; set; }

    [Required(ErrorMessage = "Description is required")]
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Loss location is required")]
    public Address Location { get; set; }

    [Required(ErrorMessage = "At least one claimant is required")]
    [MinLength(1, ErrorMessage = "At least one claimant is required")]
    public List<Claimant> Claimants { get; set; }
}

public class EndorsementRequest
{
    [Required(ErrorMessage = "Policy ID is required")]
    public string PolicyId { get; set; }

    [Required(ErrorMessage = "Effective date is required")]
    [DataType(DataType.Date)]
    [FutureDate(ErrorMessage = "Effective date must be in the future")]
    public DateTime EffectiveDate { get; set; }

    [Required(ErrorMessage = "Endorsement type is required")]
    public string Type { get; set; }

    [Required(ErrorMessage = "At least one change is required")]
    [MinLength(1, ErrorMessage = "At least one change is required")]
    public List<ChangeInfo> Changes { get; set; }

    [Required(ErrorMessage = "At least one document is required")]
    [MinLength(1, ErrorMessage = "At least one document is required")]
    public List<DocumentInfo> Documents { get; set; }
}

public class PaymentRequest
{
    [Required(ErrorMessage = "Policy ID is required")]
    public string PolicyId { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Payment method is required")]
    public string Method { get; set; }

    [Required(ErrorMessage = "Payment information is required")]
    public PaymentInfo PaymentInfo { get; set; }

    [Required(ErrorMessage = "Billing address is required")]
    public Address BillingAddress { get; set; }
}

public class RenewalRequest
{
    public string PolicyId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public List<ChangeInfo> Changes { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class CancellationRequest
{
    public string PolicyId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Reason { get; set; }
    public string Type { get; set; }
    public List<DocumentInfo> Documents { get; set; }
    public string RefundMethod { get; set; }
}

public class InvoiceRequest
{
    public string PolicyId { get; set; }
    public DateTime DueDate { get; set; }
    public List<InvoiceItem> Items { get; set; }
    public string DeliveryMethod { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class PastDateAttribute : ValidationAttribute
{
    public override bool IsValid(object value)
    {
        if (value is DateTime date)
        {
            return date <= DateTime.UtcNow;
        }
        return false;
    }
} 