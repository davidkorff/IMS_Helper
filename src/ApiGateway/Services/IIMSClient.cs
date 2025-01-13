public interface IIMSClient
{
    Task<string> GetAuthToken(string programCode, string email, string password);
    Task<QuoteResponse> CreateQuote(QuoteRequest request, string token);
    Task<DocumentResponse> GetDocuments(string quoteId, string token);
    Task<InsuredResponse> CreateInsured(InsuredRequest request, string token);
    Task<QuoteResponse> CreateQuote(CreateQuoteRequest request);
    Task<QuoteResponse> GetQuote(string quoteId);
    Task<PolicyResponse> BindQuote(string quoteId);
} 