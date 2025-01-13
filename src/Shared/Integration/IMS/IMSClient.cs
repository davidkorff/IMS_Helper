using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Integration.IMS.Interfaces;
using Shared.Integration.IMS.Models;
using System.Xml.Serialization;

public class IMSClient : IIMSClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IMSClient> _logger;
    private readonly IMSSettings _settings;
    private readonly XmlSerializer _serializer;
    private string _authToken;

    public IMSClient(
        HttpClient httpClient,
        ILogger<IMSClient> logger,
        IOptions<IMSSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/xml"));
    }

    public async Task<string> LoginAsync(string programCode, string email, string password)
    {
        try
        {
            var request = new LoginRequest
            {
                ProgramCode = programCode,
                Email = email,
                Password = password
            };

            var response = await SendRequestAsync<LoginRequest, LoginResponse>(
                "Logon.asmx", 
                "Login", 
                request);

            _authToken = response.Token;
            return _authToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to IMS");
            throw new IMSException("Authentication failed", ex);
        }
    }

    public async Task<PolicyResponse> GetPolicy(string policyId)
    {
        try
        {
            var request = new GetPolicyRequest { PolicyId = policyId };
            var response = await SendRequestAsync<GetPolicyRequest, GetPolicyResponse>(
                "QuoteFunctions.asmx",
                "GetPolicy",
                request);

            return MapToApiModel(response.Policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get policy {PolicyId}", policyId);
            throw new IMSException("Failed to retrieve policy", ex);
        }
    }

    public async Task<string> CreateSubmission(SubmissionRequest request)
    {
        try
        {
            var imsRequest = new CreateSubmissionRequest
            {
                InsuredGuid = request.InsuredGuid,
                ProducerContactGuid = request.ProducerContactGuid,
                UnderwriterGuid = request.UnderwriterGuid,
                SubmissionDate = DateTime.UtcNow
            };

            var response = await SendRequestAsync<CreateSubmissionRequest, CreateSubmissionResponse>(
                "QuoteFunctions.asmx",
                "AddSubmission",
                imsRequest);

            return response.SubmissionGuid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create submission");
            throw new IMSException("Failed to create submission", ex);
        }
    }

    public async Task<ClaimResponse> CreateClaim(CreateClaimRequest request)
    {
        try
        {
            var imsRequest = new Models.CreateClaimRequest
            {
                PolicyId = request.PolicyId,
                Claim = MapToImsModel(request)
            };

            var response = await SendRequestAsync<Models.CreateClaimRequest, CreateClaimResponse>(
                "ClaimFunctions.asmx",
                "CreateClaim",
                imsRequest);

            return new ClaimResponse
            {
                ClaimId = response.ClaimId,
                Status = Enum.Parse<ClaimStatus>(response.Status)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create claim for policy {PolicyId}", request.PolicyId);
            throw new IMSException("Failed to create claim", ex);
        }
    }

    public async Task<List<EndorsementResponse>> GetPolicyEndorsements(string policyId)
    {
        try
        {
            var request = new GetEndorsementsRequest { PolicyId = policyId };
            var response = await SendRequestAsync<GetEndorsementsRequest, GetEndorsementsResponse>(
                "QuoteFunctions.asmx",
                "GetPolicyEndorsements",
                request);

            return response.Endorsements.Select(MapToApiModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get endorsements for policy {PolicyId}", policyId);
            throw new IMSException("Failed to retrieve endorsements", ex);
        }
    }

    public async Task<EndorsementResponse> CreateEndorsement(string policyId, EndorsementRequest request)
    {
        try
        {
            var imsRequest = new CreateEndorsementRequest
            {
                PolicyId = policyId,
                Endorsement = MapToImsModel(request)
            };

            var response = await SendRequestAsync<CreateEndorsementRequest, CreateEndorsementResponse>(
                "QuoteFunctions.asmx",
                "CreateEndorsement",
                imsRequest);

            return MapToApiModel(response.Endorsement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create endorsement for policy {PolicyId}", policyId);
            throw new IMSException("Failed to create endorsement", ex);
        }
    }

    public async Task<CancellationResponse> CancelPolicy(string policyId, CancellationRequest request)
    {
        try
        {
            var imsRequest = new CancelPolicyRequest
            {
                PolicyId = policyId,
                CancellationDate = request.CancellationDate,
                Reason = request.Reason,
                Type = request.Type.ToString()
            };

            var response = await SendRequestAsync<CancelPolicyRequest, CancelPolicyResponse>(
                "QuoteFunctions.asmx",
                "CancelPolicy",
                imsRequest);

            return new CancellationResponse
            {
                CancellationId = response.CancellationId,
                Status = Enum.Parse<PolicyStatus>(response.Status),
                EffectiveDate = response.EffectiveDate,
                ReturnPremium = response.ReturnPremium
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel policy {PolicyId}", policyId);
            throw new IMSException("Failed to cancel policy", ex);
        }
    }

    public async Task<byte[]> GetDocument(string documentId)
    {
        try
        {
            var request = new GetDocumentRequest { DocumentId = documentId };
            var response = await SendRequestAsync<GetDocumentRequest, GetDocumentResponse>(
                "DocumentFunctions.asmx",
                "GetDocument",
                request);

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document {DocumentId}", documentId);
            throw new IMSException("Failed to retrieve document", ex);
        }
    }

    public async Task<string> CreateQuoteDocument(string quoteId)
    {
        try
        {
            var request = new CreateDocumentRequest
            {
                EntityId = quoteId,
                Metadata = new DocumentMetadataXml
                {
                    DocumentType = "Quote",
                    ReferenceId = quoteId,
                    DocumentDate = DateTime.UtcNow
                }
            };

            var response = await SendRequestAsync<CreateDocumentRequest, CreateDocumentResponse>(
                "DocumentFunctions.asmx",
                "CreateQuoteDocument",
                request);

            return response.DocumentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create quote document for {QuoteId}", quoteId);
            throw new IMSException("Failed to create quote document", ex);
        }
    }

    public async Task<CompanyLineResponse> GetValidCompanyLines(string programCode)
    {
        try
        {
            var parameters = new[] { "@programCode", programCode };
            var response = await SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DataAccess.asmx",
                "ExecuteCommand",
                new ExecuteCommandRequest
                {
                    ProcedureName = "ValidCompanyLinesXml",
                    Parameters = parameters
                });

            return DeserializeCompanyLines(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get valid company lines for program {ProgramCode}", programCode);
            throw new IMSException("Failed to retrieve company lines", ex);
        }
    }

    private async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        string service, 
        string action, 
        TRequest request) 
        where TRequest : class 
        where TResponse : class
    {
        var envelope = new SoapEnvelope<TRequest>(request, _authToken);
        var serializer = new XmlSerializer(typeof(SoapEnvelope<TRequest>));

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        serializer.Serialize(writer, envelope);
        var content = new StringContent(
            Encoding.UTF8.GetString(ms.ToArray()), 
            Encoding.UTF8, 
            "text/xml");

        content.Headers.Add("SOAPAction", $"http://tempuri.org/{action}");

        var response = await _httpClient.PostAsync(service, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new IMSException($"IMS request failed: {responseContent}");
        }

        var responseSerializer = new XmlSerializer(typeof(SoapEnvelope<TResponse>));
        using var responseReader = new StringReader(responseContent);
        var responseEnvelope = (SoapEnvelope<TResponse>)responseSerializer.Deserialize(responseReader);

        return responseEnvelope.Body.Request;
    }

    private PolicyResponse MapToApiModel(PolicyXml policy)
    {
        return new PolicyResponse
        {
            PolicyId = policy.PolicyId,
            QuoteId = policy.QuoteId,
            Status = Enum.Parse<PolicyStatus>(policy.Status),
            EffectiveDate = policy.EffectiveDate,
            ExpirationDate = policy.ExpirationDate,
            Insured = new InsuredInfo
            {
                Name = policy.Insured.Name,
                Address1 = policy.Insured.Address1,
                Address2 = policy.Insured.Address2,
                City = policy.Insured.City,
                State = policy.Insured.State,
                Zip = policy.Insured.Zip,
                Phone = policy.Insured.Phone,
                Email = policy.Insured.Email
            },
            // Map other properties...
        };
    }

    private ClaimXml MapToImsModel(CreateClaimRequest request)
    {
        return new ClaimXml
        {
            DateOfLoss = request.DateOfLoss,
            LossDescription = request.LossDescription,
            ClaimType = request.Type.ToString(),
            LossLocations = request.LossLocations,
            EstimatedLoss = request.EstimatedLoss,
            Claimants = request.Claimants.Select(c => new ClaimantXml
            {
                FirstName = c.FirstName,
                LastName = c.LastName,
                Email = c.Email,
                Phone = c.Phone,
                ClaimantType = c.Type.ToString(),
                Address = new AddressXml
                {
                    // Map address properties...
                }
            }).ToList()
        };
    }

    private EndorsementResponse MapToApiModel(EndorsementXml endorsement)
    {
        return new EndorsementResponse
        {
            EndorsementId = endorsement.EndorsementId,
            Type = Enum.Parse<EndorsementType>(endorsement.Type),
            EffectiveDate = endorsement.EffectiveDate,
            Status = Enum.Parse<EndorsementStatus>(endorsement.Status),
            PremiumChange = endorsement.PremiumChange
        };
    }

    private EndorsementXml MapToImsModel(EndorsementRequest request)
    {
        return new EndorsementXml
        {
            Type = request.Type.ToString(),
            EffectiveDate = request.EffectiveDate,
            Status = "Pending",
            PremiumChange = request.PremiumChange
        };
    }

    private CompanyLineResponse DeserializeCompanyLines(string xml)
    {
        using var reader = new StringReader(xml);
        var serializer = new XmlSerializer(typeof(CompanyLineResponse));
        return (CompanyLineResponse)serializer.Deserialize(reader);
    }

    private async Task<TResponse> SendRequestWithRetryAsync<TRequest, TResponse>(
        string service,
        string action,
        TRequest request,
        int maxRetries = 3)
        where TRequest : class
        where TResponse : class
    {
        for (int i = 1; i <= maxRetries; i++)
        {
            try
            {
                return await SendRequestAsync<TRequest, TResponse>(service, action, request);
            }
            catch (Exception ex) when (i < maxRetries && IsTransientException(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i - 1));
                _logger.LogWarning(ex, "Retry attempt {Attempt} of {MaxRetries} after {Delay}s",
                    i, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        throw new IMSException($"Failed after {maxRetries} retry attempts");
    }

    private bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException || ex is TimeoutException;
    }

    // Implement remaining interface methods...
} 