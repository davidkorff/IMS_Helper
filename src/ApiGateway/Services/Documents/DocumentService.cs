using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ApiGateway.Services.Documents;
using ApiGateway.Services.Documents.Models;
using ApiGateway.Services.Documents.Interfaces;
using ApiGateway.Services.Documents.Exceptions;
using ApiGateway.Services.Documents.Validators;

public class DocumentService : IDocumentService
{
    private readonly IIMSClient _imsClient;
    private readonly IDocumentStorage _storage;
    private readonly ILogger<DocumentService> _logger;
    private readonly IValidator<DocumentMetadata> _validator;

    public DocumentService(
        IIMSClient imsClient,
        IDocumentStorage storage,
        ILogger<DocumentService> logger,
        IValidator<DocumentMetadata> validator)
    {
        _imsClient = imsClient;
        _storage = storage;
        _logger = logger;
        _validator = validator;
    }

    public async Task<List<DocumentResponse>> GetQuoteDocuments(string quoteId)
    {
        try
        {
            // Get IMS documents
            var imsDocuments = await _imsClient.GetQuoteDocuments(quoteId);
            
            // Get locally stored documents
            var storedDocuments = await _storage.ListDocuments($"quotes/{quoteId}");
            
            // Merge and return all documents
            return imsDocuments.Concat(storedDocuments).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for quote {QuoteId}", quoteId);
            throw new DocumentException("Failed to retrieve quote documents", ex);
        }
    }

    public async Task<List<DocumentResponse>> GetPolicyDocuments(string policyId)
    {
        try
        {
            var imsDocuments = await _imsClient.GetPolicyDocuments(policyId);
            var storedDocuments = await _storage.ListDocuments($"policies/{policyId}");
            return imsDocuments.Concat(storedDocuments).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents for policy {PolicyId}", policyId);
            throw new DocumentException("Failed to retrieve policy documents", ex);
        }
    }

    public async Task<DocumentResponse> UploadDocument(IFormFile file, DocumentMetadata metadata)
    {
        try
        {
            // Validate metadata
            var validationResult = await _validator.ValidateAsync(metadata);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            // Validate file
            ValidateFile(file);

            // Generate unique document ID
            var documentId = Guid.NewGuid().ToString();

            // Create storage path based on reference type
            var path = metadata.ReferenceId.StartsWith("Q") 
                ? $"quotes/{metadata.ReferenceId}/{documentId}"
                : $"policies/{metadata.ReferenceId}/{documentId}";

            // Upload to storage
            using var stream = file.OpenReadStream();
            var document = await _storage.UploadDocument(path, stream, new DocumentInfo
            {
                DocumentId = documentId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Metadata = metadata
            });

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            throw new DocumentException("Failed to upload document", ex);
        }
    }

    public async Task<DocumentContent> GetDocument(string documentId)
    {
        try
        {
            return await _storage.GetDocument(documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document {DocumentId}", documentId);
            throw new DocumentException("Failed to retrieve document", ex);
        }
    }

    private void ValidateFile(IFormFile file)
    {
        if (file.Length > 10 * 1024 * 1024) // 10MB limit
        {
            throw new ValidationException("File size exceeds limit");
        }

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            throw new ValidationException("Invalid file type");
        }
    }
} 