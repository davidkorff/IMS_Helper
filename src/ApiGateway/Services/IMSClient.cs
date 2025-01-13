using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class IMSClient : IIMSClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IMSClient> _logger;
    private readonly IConfiguration _configuration;

    public IMSClient(
        HttpClient httpClient,
        ILogger<IMSClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<QuoteResponse> CreateQuote(CreateQuoteRequest request)
    {
        try
        {
            var soapRequest = CreateQuoteSoapRequest(request);
            var response = await SendSoapRequest("quote.asmx", soapRequest);
            return ParseQuoteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quote for insured {InsuredName}", 
                $"{request.Insured.FirstName} {request.Insured.LastName}");
            throw new IMSException("QUOTE_CREATE_ERROR", "Failed to create quote", ex);
        }
    }

    public async Task<QuoteResponse> GetQuote(string quoteId)
    {
        try
        {
            var soapRequest = CreateGetQuoteSoapRequest(quoteId);
            var response = await SendSoapRequest("quote.asmx", soapRequest);
            return ParseQuoteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quote {QuoteId}", quoteId);
            throw new IMSException("QUOTE_GET_ERROR", "Failed to retrieve quote", ex);
        }
    }

    public async Task<PolicyResponse> BindQuote(string quoteId)
    {
        try
        {
            var soapRequest = CreateBindQuoteSoapRequest(quoteId);
            var response = await SendSoapRequest("policy.asmx", soapRequest);
            return ParsePolicyResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error binding quote {QuoteId}", quoteId);
            throw new IMSException("QUOTE_BIND_ERROR", "Failed to bind quote", ex);
        }
    }

    private string CreateQuoteSoapRequest(CreateQuoteRequest request)
    {
        // Implementation details in documentation
        return BuildSoapEnvelope("CreateQuote", new Dictionary<string, string>
        {
            {"programCode", request.ProgramCode},
            {"insuredFirstName", request.Insured.FirstName},
            // ... additional mappings
        });
    }
} 