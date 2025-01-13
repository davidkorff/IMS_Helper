public interface IIMSClient
{
    // Authentication
    Task<string> LoginAsync(string programCode, string email, string password);
    Task<bool> ValidateTokenAsync(string token);

    // Policy/Quote Operations
    Task<PolicyResponse> GetPolicy(string policyId);
    Task<List<EndorsementResponse>> GetPolicyEndorsements(string policyId);
    Task<string> CreateSubmission(SubmissionRequest request);
    Task<string> CreateQuote(QuoteRequest request);
    Task<EndorsementResponse> CreateEndorsement(string policyId, EndorsementRequest request);
    Task<CancellationResponse> CancelPolicy(string policyId, CancellationRequest request);
    Task<PolicyResponse> ReinstatePolicy(string policyId, ReinstatementRequest request);
    Task<BindResponse> BindQuote(string quoteId, BindRequest request);

    // Document Operations
    Task<byte[]> GetDocument(string documentId);
    Task<string> CreateQuoteDocument(string quoteId);
    Task<string> CreateBinderDocument(string policyId);
    Task<string> CreatePolicyDocument(string policyId);
    Task<string> InsertDocument(string entityId, DocumentMetadata metadata, Stream content);

    // Claims Operations
    Task<ClaimResponse> GetClaim(string claimId);
    Task<List<ClaimResponse>> GetPolicyClaims(string policyId);
    Task<ClaimResponse> CreateClaim(CreateClaimRequest request);
    Task<ClaimResponse> UpdateClaimStatus(string claimId, UpdateClaimStatusRequest request);
    Task<string> AddClaimDocument(string claimId, string documentId, ClaimDocumentMetadata metadata);

    // Data Access Operations
    Task<CompanyLineResponse> GetValidCompanyLines(string programCode);
    Task<List<BusinessType>> GetBusinessTypes();
    Task<List<LocationType>> GetLocationTypes();
    Task<List<PolicyType>> GetPolicyTypes();
} 