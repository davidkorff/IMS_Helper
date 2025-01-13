public interface IPolicyService
{
    Task<PolicyResponse> GetPolicy(string policyId);
    Task<EndorsementResponse> CreateEndorsement(string policyId, EndorsementRequest request);
    Task<CancellationResponse> CancelPolicy(string policyId, CancellationRequest request);
    Task<PolicyResponse> ReinstatePolicy(string policyId, ReinstatementRequest request);
    Task<List<PolicyHistoryEntry>> GetPolicyHistory(string policyId);
} 