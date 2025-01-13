using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApiGateway.Services.Policies;
using ApiGateway.Models;
using ApiGateway.Repositories;
using ApiGateway.Validators;

public class PolicyService : IPolicyService
{
    private readonly IIMSClient _imsClient;
    private readonly IDocumentService _documentService;
    private readonly IPolicyHistoryRepository _historyRepository;
    private readonly ILogger<PolicyService> _logger;
    private readonly IValidator<EndorsementRequest> _endorsementValidator;
    private readonly IValidator<CancellationRequest> _cancellationValidator;

    public PolicyService(
        IIMSClient imsClient,
        IDocumentService documentService,
        IPolicyHistoryRepository historyRepository,
        ILogger<PolicyService> logger,
        IValidator<EndorsementRequest> endorsementValidator,
        IValidator<CancellationRequest> cancellationValidator)
    {
        _imsClient = imsClient;
        _documentService = documentService;
        _historyRepository = historyRepository;
        _logger = logger;
        _endorsementValidator = endorsementValidator;
        _cancellationValidator = cancellationValidator;
    }

    public async Task<PolicyResponse> GetPolicy(string policyId)
    {
        try
        {
            var policy = await _imsClient.GetPolicy(policyId);
            var endorsements = await _imsClient.GetPolicyEndorsements(policyId);
            
            policy.Endorsements = endorsements
                .Select(e => new EndorsementInfo
                {
                    EndorsementId = e.EndorsementId,
                    EffectiveDate = e.EffectiveDate,
                    Status = e.Status
                })
                .ToList();

            return policy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy {PolicyId}", policyId);
            throw new PolicyException("Failed to retrieve policy", ex);
        }
    }

    public async Task<EndorsementResponse> CreateEndorsement(string policyId, EndorsementRequest request)
    {
        try
        {
            var validationResult = await _endorsementValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var policy = await _imsClient.GetPolicy(policyId);
            ValidatePolicyForEndorsement(policy);

            var endorsement = await _imsClient.CreateEndorsement(policyId, request);

            await _historyRepository.AddEntry(new PolicyHistoryEntry
            {
                PolicyId = policyId,
                Timestamp = DateTime.UtcNow,
                Action = PolicyAction.Endorsed,
                Description = request.Description,
                Changes = request.Changes.ToDictionary(
                    c => c.Field,
                    c => (object)new { Old = c.OldValue, New = c.NewValue }
                )
            });

            return endorsement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating endorsement for policy {PolicyId}", policyId);
            throw new PolicyException("Failed to create endorsement", ex);
        }
    }

    public async Task<CancellationResponse> CancelPolicy(string policyId, CancellationRequest request)
    {
        try
        {
            var validationResult = await _cancellationValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var policy = await _imsClient.GetPolicy(policyId);
            ValidatePolicyForCancellation(policy);

            var cancellation = await _imsClient.CancelPolicy(policyId, request);

            await _historyRepository.AddEntry(new PolicyHistoryEntry
            {
                PolicyId = policyId,
                Timestamp = DateTime.UtcNow,
                Action = PolicyAction.Cancelled,
                Description = request.Description,
                Changes = new Dictionary<string, object>
                {
                    { "CancellationDate", request.CancellationDate },
                    { "Reason", request.Reason },
                    { "ReturnPremium", cancellation.ReturnPremium }
                }
            });

            return cancellation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling policy {PolicyId}", policyId);
            throw new PolicyException("Failed to cancel policy", ex);
        }
    }

    public async Task<PolicyResponse> ReinstatePolicy(string policyId, ReinstatementRequest request)
    {
        try
        {
            var policy = await _imsClient.GetPolicy(policyId);
            ValidatePolicyForReinstatement(policy);

            // Upload supporting documents
            if (request.SupportingDocuments?.Any() == true)
            {
                foreach (var doc in request.SupportingDocuments)
                {
                    doc.ReferenceId = policyId;
                    // Document upload handled by DocumentService
                }
            }

            var reinstatedPolicy = await _imsClient.ReinstatePolicy(policyId, request);

            await _historyRepository.AddEntry(new PolicyHistoryEntry
            {
                PolicyId = policyId,
                Timestamp = DateTime.UtcNow,
                Action = PolicyAction.Reinstated,
                Description = request.Reason
            });

            return reinstatedPolicy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reinstating policy {PolicyId}", policyId);
            throw new PolicyException("Failed to reinstate policy", ex);
        }
    }

    public async Task<List<PolicyHistoryEntry>> GetPolicyHistory(string policyId)
    {
        try
        {
            return await _historyRepository.GetPolicyHistory(policyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving history for policy {PolicyId}", policyId);
            throw new PolicyException("Failed to retrieve policy history", ex);
        }
    }

    private void ValidatePolicyForEndorsement(PolicyResponse policy)
    {
        if (policy.Status != PolicyStatus.Active)
        {
            throw new PolicyException("Policy must be active for endorsement");
        }
    }

    private void ValidatePolicyForCancellation(PolicyResponse policy)
    {
        if (policy.Status != PolicyStatus.Active)
        {
            throw new PolicyException("Policy must be active for cancellation");
        }
    }

    private void ValidatePolicyForReinstatement(PolicyResponse policy)
    {
        if (policy.Status != PolicyStatus.Cancelled)
        {
            throw new PolicyException("Only cancelled policies can be reinstated");
        }
    }
} 