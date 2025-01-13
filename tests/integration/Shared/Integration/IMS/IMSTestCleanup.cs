using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class IMSTestCleanup : IAsyncDisposable
{
    private readonly ILogger<IMSTestCleanup> _logger;
    private readonly List<string> _createdInsuredGuids = new();
    private readonly List<string> _createdQuoteGuids = new();
    private readonly List<string> _createdDocumentIds = new();
    private readonly IMSClient _client;

    public IMSTestCleanup(IMSClient client, ILogger<IMSTestCleanup> logger)
    {
        _client = client;
        _logger = logger;
    }

    public void TrackInsured(string insuredGuid)
    {
        if (!string.IsNullOrEmpty(insuredGuid))
        {
            _createdInsuredGuids.Add(insuredGuid);
        }
    }

    public void TrackQuote(string quoteGuid)
    {
        if (!string.IsNullOrEmpty(quoteGuid))
        {
            _createdQuoteGuids.Add(quoteGuid);
        }
    }

    public void TrackDocument(string documentId)
    {
        if (!string.IsNullOrEmpty(documentId))
        {
            _createdDocumentIds.Add(documentId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var documentId in _createdDocumentIds)
            {
                try
                {
                    await _client.DeleteDocument(documentId);
                    _logger.LogInformation("Cleaned up document {DocumentId}", documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup document {DocumentId}", documentId);
                }
            }

            foreach (var quoteGuid in _createdQuoteGuids)
            {
                try
                {
                    await _client.DeleteQuote(quoteGuid);
                    _logger.LogInformation("Cleaned up quote {QuoteGuid}", quoteGuid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup quote {QuoteGuid}", quoteGuid);
                }
            }

            foreach (var insuredGuid in _createdInsuredGuids)
            {
                try
                {
                    await _client.DeleteInsured(insuredGuid);
                    _logger.LogInformation("Cleaned up insured {InsuredGuid}", insuredGuid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup insured {InsuredGuid}", insuredGuid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test cleanup");
        }
    }
} 