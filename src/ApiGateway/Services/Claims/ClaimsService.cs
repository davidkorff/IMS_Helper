using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ApiGateway.Services.Claims;
using ApiGateway.Services.Documents;
using ApiGateway.Services.Policies;
using ApiGateway.Services.Validators;

public class ClaimsService : IClaimsService
{
    private readonly IIMSClient _imsClient;
    private readonly IDocumentService _documentService;
    private readonly IPolicyService _policyService;
    private readonly ILogger<ClaimsService> _logger;
    private readonly IValidator<CreateClaimRequest> _createClaimValidator;
    private readonly IValidator<UpdateClaimStatusRequest> _updateStatusValidator;

    public ClaimsService(
        IIMSClient imsClient,
        IDocumentService documentService,
        IPolicyService policyService,
        ILogger<ClaimsService> logger,
        IValidator<CreateClaimRequest> createClaimValidator,
        IValidator<UpdateClaimStatusRequest> updateStatusValidator)
    {
        _imsClient = imsClient;
        _documentService = documentService;
        _policyService = policyService;
        _logger = logger;
        _createClaimValidator = createClaimValidator;
        _updateStatusValidator = updateStatusValidator;
    }

    public async Task<ClaimResponse> CreateClaim(CreateClaimRequest request)
    {
        try
        {
            var validationResult = await _createClaimValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Verify policy exists and is active
            var policy = await _policyService.GetPolicy(request.PolicyId);
            if (policy.Status != PolicyStatus.Active)
            {
                throw new PolicyException("Claims can only be filed against active policies");
            }

            // Create claim in IMS
            var claim = await _imsClient.CreateClaim(request);

            // Upload any provided documents
            if (request.Documents?.Any() == true)
            {
                foreach (var doc in request.Documents)
                {
                    // Document metadata is stored with claim reference
                    doc.ReferenceId = claim.ClaimId;
                    // Actual upload handled by DocumentService
                }
            }

            _logger.LogInformation(
                "Created claim {ClaimId} for policy {PolicyId}", 
                claim.ClaimId, 
                request.PolicyId);

            return claim;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating claim for policy {PolicyId}", request.PolicyId);
            throw new ClaimException("Failed to create claim", ex);
        }
    }

    public async Task<ClaimResponse> GetClaim(string claimId)
    {
        try
        {
            var claim = await _imsClient.GetClaim(claimId);
            
            // Enrich with document information
            var documents = await _documentService.GetClaimDocuments(claimId);
            claim.Documents = documents.Select(d => new ClaimDocument
            {
                DocumentId = d.DocumentId,
                FileName = d.FileName,
                Type = Enum.Parse<ClaimDocumentType>(d.Description),
                UploadedAt = d.UploadedAt
            }).ToList();

            return claim;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving claim {ClaimId}", claimId);
            throw new ClaimException("Failed to retrieve claim", ex);
        }
    }

    public async Task<List<ClaimResponse>> GetPolicyClaims(string policyId)
    {
        try
        {
            var claims = await _imsClient.GetPolicyClaims(policyId);
            
            // Enrich each claim with its documents
            foreach (var claim in claims)
            {
                var documents = await _documentService.GetClaimDocuments(claim.ClaimId);
                claim.Documents = documents.Select(d => new ClaimDocument
                {
                    DocumentId = d.DocumentId,
                    FileName = d.FileName,
                    Type = Enum.Parse<ClaimDocumentType>(d.Description),
                    UploadedAt = d.UploadedAt
                }).ToList();
            }

            return claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving claims for policy {PolicyId}", policyId);
            throw new ClaimException("Failed to retrieve policy claims", ex);
        }
    }

    public async Task<DocumentResponse> AddClaimDocument(
        string claimId, 
        IFormFile file, 
        ClaimDocumentMetadata metadata)
    {
        try
        {
            // Verify claim exists
            var claim = await _imsClient.GetClaim(claimId);

            // Upload document
            var documentMetadata = new DocumentMetadata
            {
                ReferenceId = claimId,
                Type = DocumentType.Claim,
                Description = metadata.Description
            };

            var document = await _documentService.UploadDocument(file, documentMetadata);

            // Update claim with document reference
            await _imsClient.AddClaimDocument(claimId, document.DocumentId, metadata);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document to claim {ClaimId}", claimId);
            throw new ClaimException("Failed to add claim document", ex);
        }
    }

    public async Task<ClaimResponse> UpdateClaimStatus(
        string claimId, 
        UpdateClaimStatusRequest request)
    {
        try
        {
            var validationResult = await _updateStatusValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var claim = await _imsClient.GetClaim(claimId);
            ValidateStatusTransition(claim.Status, request.NewStatus);

            // Update claim status in IMS
            var updatedClaim = await _imsClient.UpdateClaimStatus(claimId, request);

            _logger.LogInformation(
                "Updated claim {ClaimId} status from {OldStatus} to {NewStatus}",
                claimId,
                claim.Status,
                request.NewStatus);

            return updatedClaim;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for claim {ClaimId}", claimId);
            throw new ClaimException("Failed to update claim status", ex);
        }
    }

    private void ValidateStatusTransition(ClaimStatus currentStatus, ClaimStatus newStatus)
    {
        var validTransitions = GetValidStatusTransitions(currentStatus);
        if (!validTransitions.Contains(newStatus))
        {
            throw new ValidationException(
                $"Invalid status transition from {currentStatus} to {newStatus}");
        }
    }

    private HashSet<ClaimStatus> GetValidStatusTransitions(ClaimStatus currentStatus)
    {
        return currentStatus switch
        {
            ClaimStatus.New => new HashSet<ClaimStatus> 
                { ClaimStatus.UnderReview, ClaimStatus.NeedsMoreInfo },
            ClaimStatus.UnderReview => new HashSet<ClaimStatus> 
                { ClaimStatus.Approved, ClaimStatus.Denied, ClaimStatus.NeedsMoreInfo },
            ClaimStatus.NeedsMoreInfo => new HashSet<ClaimStatus> 
                { ClaimStatus.UnderReview },
            ClaimStatus.Approved => new HashSet<ClaimStatus> 
                { ClaimStatus.InPayment, ClaimStatus.Closed },
            ClaimStatus.InPayment => new HashSet<ClaimStatus> 
                { ClaimStatus.Closed },
            ClaimStatus.Closed => new HashSet<ClaimStatus>(),
            ClaimStatus.Denied => new HashSet<ClaimStatus>(),
            _ => new HashSet<ClaimStatus>()
        };
    }
} 