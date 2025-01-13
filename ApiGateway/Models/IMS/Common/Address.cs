using System.ComponentModel.DataAnnotations;

public class Address
{
    [Required(ErrorMessage = "Street address is required")]
    [StringLength(100, ErrorMessage = "Street address cannot exceed 100 characters")]
    public string Street1 { get; set; }

    [StringLength(100, ErrorMessage = "Street address line 2 cannot exceed 100 characters")]
    public string Street2 { get; set; }

    [Required(ErrorMessage = "City is required")]
    [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
    public string City { get; set; }

    [Required(ErrorMessage = "State is required")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "State must be 2 characters")]
    [RegularExpression(@"^[A-Z]{2}$", ErrorMessage = "State must be 2 uppercase letters")]
    public string State { get; set; }

    [Required(ErrorMessage = "ZIP code is required")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid ZIP code format")]
    public string Zip { get; set; }
}

public class DocumentInfo
{
    [Required(ErrorMessage = "Document ID is required")]
    public string Id { get; set; }

    [Required(ErrorMessage = "Document type is required")]
    public string Type { get; set; }

    [Url(ErrorMessage = "Invalid URL format")]
    public string Url { get; set; }
}

public class PaymentInfo
{
    [Required(ErrorMessage = "Payment type is required")]
    public string Type { get; set; }

    [Required(ErrorMessage = "Account number is required")]
    [RegularExpression(@"^\d{4,17}$", ErrorMessage = "Invalid account number format")]
    public string AccountNumber { get; set; }

    [Required(ErrorMessage = "Routing number is required")]
    [RegularExpression(@"^\d{9}$", ErrorMessage = "Invalid routing number format")]
    public string RoutingNumber { get; set; }

    [Required(ErrorMessage = "Expiration date is required")]
    [DataType(DataType.Date)]
    [FutureDate(ErrorMessage = "Expiration date must be in the future")]
    public DateTime ExpirationDate { get; set; }
}

public class ChangeInfo
{
    [Required(ErrorMessage = "Field name is required")]
    public string Field { get; set; }

    public string OldValue { get; set; }

    [Required(ErrorMessage = "New value is required")]
    public string NewValue { get; set; }

    [Required(ErrorMessage = "Change reason is required")]
    public string Reason { get; set; }
}

public class BillingInstallment
{
    [Required(ErrorMessage = "Due date is required")]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Status is required")]
    public string Status { get; set; }

    [Required(ErrorMessage = "Payment method is required")]
    public string PaymentMethod { get; set; }
}

public class InvoiceItem
{
    [Required(ErrorMessage = "Description is required")]
    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
    public string Description { get; set; }

    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Type is required")]
    public string Type { get; set; }
}

public class Claimant
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Claimant type is required")]
    public string Type { get; set; }

    [Phone(ErrorMessage = "Invalid phone number format")]
    public string Phone { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string Email { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class FutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object value)
    {
        if (value is DateTime date)
        {
            return date > DateTime.UtcNow;
        }
        return false;
    }
} 