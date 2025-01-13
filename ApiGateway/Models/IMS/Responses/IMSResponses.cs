using System.ComponentModel.DataAnnotations;

public class QuoteResponse
{
    [Required]
    public string QuoteId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal Premium { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public DateTime ExpirationDate { get; set; }

    [Required]
    public string Status { get; set; }
}

public class InsuredResponse
{
    public string InsuredId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address Address { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
}

public class PolicyResponse
{
    public string PolicyId { get; set; }
    public string Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal Premium { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class DocumentResponse
{
    public string DocumentId { get; set; }
    public string Status { get; set; }
    public string Url { get; set; }
}

public class SubmissionResponse
{
    public string SubmissionId { get; set; }
    public string Status { get; set; }
    public string UnderwriterId { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class ClaimResponse
{
    public string ClaimId { get; set; }
    public string Status { get; set; }
    public string AdjusterId { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class EndorsementResponse
{
    public string EndorsementId { get; set; }
    public string Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal PremiumChange { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class PaymentResponse
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string ReceiptUrl { get; set; }
}

public class RenewalResponse
{
    public string RenewalId { get; set; }
    public string Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal Premium { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class CancellationResponse
{
    public string CancellationId { get; set; }
    public string Status { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal RefundAmount { get; set; }
    public List<DocumentInfo> Documents { get; set; }
}

public class BillingScheduleResponse
{
    public string PolicyId { get; set; }
    public decimal TotalPremium { get; set; }
    public string PaymentPlan { get; set; }
    public List<BillingInstallment> Installments { get; set; }
}

public class InvoiceResponse
{
    public string InvoiceId { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string DocumentId { get; set; }
    public string DeliveryStatus { get; set; }
} 