public interface IClaimsService
{
    Task<ClaimResponse> CreateClaim(CreateClaimRequest request);
    Task<ClaimResponse> GetClaim(string claimId);
    Task<List<ClaimResponse>> GetPolicyClaims(string policyId);
    Task<DocumentResponse> AddClaimDocument(string claimId, IFormFile file, ClaimDocumentMetadata metadata);
    Task<ClaimResponse> UpdateClaimStatus(string claimId, UpdateClaimStatusRequest request);
} 