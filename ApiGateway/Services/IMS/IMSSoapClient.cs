using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

public class IMSSoapClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IMSSoapClient> _logger;
    private readonly IMSSettings _settings;
    private string _authToken;
    private DateTime _tokenExpiration;

    public IMSSoapClient(
        HttpClient httpClient,
        ILogger<IMSSoapClient> logger,
        IOptions<IMSSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        
        // Configure client
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/xml"));
    }

    public async Task<string> AuthenticateAsync(
        string username, 
        string password)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <AuthenticationRequest>
                        <username>{SecurityElement.Escape(username)}</username>
                        <password>{SecurityElement.Escape(password)}</password>
                    </AuthenticationRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync(
                "Authentication", 
                soapEnvelope);

            // Parse token from response
            _authToken = ExtractTokenFromResponse(response);
            _tokenExpiration = DateTime.UtcNow.AddHours(1); // Or parse from response
            
            return _authToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with IMS");
            throw new IMSAuthenticationException(
                "Failed to authenticate with IMS", 
                ex);
        }
    }

    private async Task<string> SendSoapRequestAsync(
        string action, 
        string soapEnvelope)
    {
        using var content = new StringContent(
            soapEnvelope, 
            Encoding.UTF8, 
            "text/xml");
            
        content.Headers.Add("SOAPAction", $"http://ims.com/{action}");

        // Add authentication token if we have one and it's not expired
        if (!string.IsNullOrEmpty(_authToken) && 
            DateTime.UtcNow < _tokenExpiration)
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _authToken);
        }

        var response = await _httpClient.PostAsync(
            _settings.SoapEndpoint, 
            content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "IMS SOAP request failed. Status: {Status}, Error: {Error}",
                response.StatusCode,
                errorContent);
                
            throw new IMSRequestException(
                $"IMS request failed with status {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private string ExtractTokenFromResponse(string soapResponse)
    {
        // Implementation depends on actual IMS response format
        // This is a placeholder
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);
        
        var tokenNode = doc.SelectSingleNode(
            "//AuthenticationResponse/token");
            
        return tokenNode?.InnerText 
            ?? throw new IMSAuthenticationException(
                "No token found in authentication response");
    }

    public async Task<QuoteResponse> CreateQuoteAsync(QuoteRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateQuoteRequest>
                        <insuredId>{SecurityElement.Escape(request.InsuredId)}</insuredId>
                        <effectiveDate>{request.EffectiveDate:yyyy-MM-dd}</effectiveDate>
                        <coverageType>{SecurityElement.Escape(request.CoverageType)}</coverageType>
                        <limits>
                            {string.Join("", request.Limits.Select(l => 
                                $"<limit><type>{SecurityElement.Escape(l.Type)}</type><amount>{l.Amount}</amount></limit>"))}
                        </limits>
                    </CreateQuoteRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateQuote", soapEnvelope);
            return ParseQuoteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create quote in IMS");
            throw new IMSRequestException("Failed to create quote", ex);
        }
    }

    public async Task<InsuredResponse> CreateInsuredAsync(InsuredRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateInsuredRequest>
                        <name>
                            <first>{SecurityElement.Escape(request.FirstName)}</first>
                            <last>{SecurityElement.Escape(request.LastName)}</last>
                        </name>
                        <address>
                            <street1>{SecurityElement.Escape(request.Address.Street1)}</street1>
                            <street2>{SecurityElement.Escape(request.Address.Street2)}</street2>
                            <city>{SecurityElement.Escape(request.Address.City)}</city>
                            <state>{SecurityElement.Escape(request.Address.State)}</state>
                            <zip>{SecurityElement.Escape(request.Address.Zip)}</zip>
                        </address>
                        <contact>
                            <email>{SecurityElement.Escape(request.Email)}</email>
                            <phone>{SecurityElement.Escape(request.Phone)}</phone>
                        </contact>
                    </CreateInsuredRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateInsured", soapEnvelope);
            return ParseInsuredResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create insured in IMS");
            throw new IMSRequestException("Failed to create insured", ex);
        }
    }

    public async Task<InsuredResponse> GetInsuredAsync(string insuredId)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <GetInsuredRequest>
                        <insuredId>{SecurityElement.Escape(insuredId)}</insuredId>
                    </GetInsuredRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("GetInsured", soapEnvelope);
            return ParseInsuredResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get insured from IMS");
            throw new IMSRequestException("Failed to get insured", ex);
        }
    }

    public async Task<PolicyResponse> CreatePolicyAsync(PolicyRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreatePolicyRequest>
                        <quoteId>{SecurityElement.Escape(request.QuoteId)}</quoteId>
                        <effectiveDate>{request.EffectiveDate:yyyy-MM-dd}</effectiveDate>
                        <paymentPlan>{SecurityElement.Escape(request.PaymentPlan)}</paymentPlan>
                        <documents>
                            {string.Join("", request.Documents.Select(d => 
                                $"<document><type>{SecurityElement.Escape(d.Type)}</type><id>{SecurityElement.Escape(d.Id)}</id></document>"))}
                        </documents>
                    </CreatePolicyRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreatePolicy", soapEnvelope);
            return ParsePolicyResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create policy in IMS");
            throw new IMSRequestException("Failed to create policy", ex);
        }
    }

    public async Task<DocumentResponse> UploadDocumentAsync(DocumentRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <UploadDocumentRequest>
                        <type>{SecurityElement.Escape(request.Type)}</type>
                        <filename>{SecurityElement.Escape(request.Filename)}</filename>
                        <content>{Convert.ToBase64String(request.Content)}</content>
                        <metadata>
                            {string.Join("", request.Metadata.Select(m => 
                                $"<item><key>{SecurityElement.Escape(m.Key)}</key><value>{SecurityElement.Escape(m.Value)}</value></item>"))}
                        </metadata>
                    </UploadDocumentRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("UploadDocument", soapEnvelope);
            return ParseDocumentResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document to IMS");
            throw new IMSRequestException("Failed to upload document", ex);
        }
    }

    public async Task<byte[]> GetDocumentAsync(string documentId)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <GetDocumentRequest>
                        <documentId>{SecurityElement.Escape(documentId)}</documentId>
                    </GetDocumentRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("GetDocument", soapEnvelope);
            return ParseDocumentContent(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document from IMS");
            throw new IMSRequestException("Failed to get document", ex);
        }
    }

    public async Task<SubmissionResponse> CreateSubmissionAsync(SubmissionRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateSubmissionRequest>
                        <insuredId>{SecurityElement.Escape(request.InsuredId)}</insuredId>
                        <type>{SecurityElement.Escape(request.Type)}</type>
                        <underwriter>{SecurityElement.Escape(request.Underwriter)}</underwriter>
                        <documents>
                            {string.Join("", request.Documents.Select(d => 
                                $"<document><type>{SecurityElement.Escape(d.Type)}</type><id>{SecurityElement.Escape(d.Id)}</id></document>"))}
                        </documents>
                        <notes>{SecurityElement.Escape(request.Notes)}</notes>
                    </CreateSubmissionRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateSubmission", soapEnvelope);
            return ParseSubmissionResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create submission in IMS");
            throw new IMSRequestException("Failed to create submission", ex);
        }
    }

    public async Task<ClaimResponse> CreateClaimAsync(ClaimRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateClaimRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <lossDate>{request.LossDate:yyyy-MM-dd}</lossDate>
                        <lossType>{SecurityElement.Escape(request.LossType)}</lossType>
                        <description>{SecurityElement.Escape(request.Description)}</description>
                        <location>
                            <street1>{SecurityElement.Escape(request.Location.Street1)}</street1>
                            <street2>{SecurityElement.Escape(request.Location.Street2)}</street2>
                            <city>{SecurityElement.Escape(request.Location.City)}</city>
                            <state>{SecurityElement.Escape(request.Location.State)}</state>
                            <zip>{SecurityElement.Escape(request.Location.Zip)}</zip>
                        </location>
                        <claimants>
                            {string.Join("", request.Claimants.Select(c => $@"
                                <claimant>
                                    <name>
                                        <first>{SecurityElement.Escape(c.FirstName)}</first>
                                        <last>{SecurityElement.Escape(c.LastName)}</last>
                                    </name>
                                    <type>{SecurityElement.Escape(c.Type)}</type>
                                    <contact>
                                        <phone>{SecurityElement.Escape(c.Phone)}</phone>
                                        <email>{SecurityElement.Escape(c.Email)}</email>
                                    </contact>
                                </claimant>"))}
                        </claimants>
                    </CreateClaimRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateClaim", soapEnvelope);
            return ParseClaimResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create claim in IMS");
            throw new IMSRequestException("Failed to create claim", ex);
        }
    }

    public async Task<EndorsementResponse> CreateEndorsementAsync(EndorsementRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateEndorsementRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <effectiveDate>{request.EffectiveDate:yyyy-MM-dd}</effectiveDate>
                        <type>{SecurityElement.Escape(request.Type)}</type>
                        <changes>
                            {string.Join("", request.Changes.Select(c => $@"
                                <change>
                                    <field>{SecurityElement.Escape(c.Field)}</field>
                                    <oldValue>{SecurityElement.Escape(c.OldValue)}</oldValue>
                                    <newValue>{SecurityElement.Escape(c.NewValue)}</newValue>
                                    <reason>{SecurityElement.Escape(c.Reason)}</reason>
                                </change>"))}
                        </changes>
                        <documents>
                            {string.Join("", request.Documents.Select(d => 
                                $"<document><type>{SecurityElement.Escape(d.Type)}</type><id>{SecurityElement.Escape(d.Id)}</id></document>"))}
                        </documents>
                    </CreateEndorsementRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateEndorsement", soapEnvelope);
            return ParseEndorsementResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create endorsement in IMS");
            throw new IMSRequestException("Failed to create endorsement", ex);
        }
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <ProcessPaymentRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <amount>{request.Amount}</amount>
                        <method>{SecurityElement.Escape(request.Method)}</method>
                        <paymentInfo>
                            <type>{SecurityElement.Escape(request.PaymentInfo.Type)}</type>
                            <accountNumber>{SecurityElement.Escape(request.PaymentInfo.AccountNumber)}</accountNumber>
                            <routingNumber>{SecurityElement.Escape(request.PaymentInfo.RoutingNumber)}</routingNumber>
                            <expirationDate>{request.PaymentInfo.ExpirationDate:MM/yy}</expirationDate>
                        </paymentInfo>
                        <billingAddress>
                            <street1>{SecurityElement.Escape(request.BillingAddress.Street1)}</street1>
                            <street2>{SecurityElement.Escape(request.BillingAddress.Street2)}</street2>
                            <city>{SecurityElement.Escape(request.BillingAddress.City)}</city>
                            <state>{SecurityElement.Escape(request.BillingAddress.State)}</state>
                            <zip>{SecurityElement.Escape(request.BillingAddress.Zip)}</zip>
                        </billingAddress>
                    </ProcessPaymentRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("ProcessPayment", soapEnvelope);
            return ParsePaymentResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment in IMS");
            throw new IMSRequestException("Failed to process payment", ex);
        }
    }

    public async Task<RenewalResponse> CreateRenewalAsync(RenewalRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CreateRenewalRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <effectiveDate>{request.EffectiveDate:yyyy-MM-dd}</effectiveDate>
                        <changes>
                            {string.Join("", request.Changes.Select(c => $@"
                                <change>
                                    <field>{SecurityElement.Escape(c.Field)}</field>
                                    <oldValue>{SecurityElement.Escape(c.OldValue)}</oldValue>
                                    <newValue>{SecurityElement.Escape(c.NewValue)}</newValue>
                                </change>"))}
                        </changes>
                        <documents>
                            {string.Join("", request.Documents.Select(d => 
                                $"<document><type>{SecurityElement.Escape(d.Type)}</type><id>{SecurityElement.Escape(d.Id)}</id></document>"))}
                        </documents>
                    </CreateRenewalRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CreateRenewal", soapEnvelope);
            return ParseRenewalResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create renewal in IMS");
            throw new IMSRequestException("Failed to create renewal", ex);
        }
    }

    public async Task<CancellationResponse> CancelPolicyAsync(CancellationRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <CancelPolicyRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <effectiveDate>{request.EffectiveDate:yyyy-MM-dd}</effectiveDate>
                        <reason>{SecurityElement.Escape(request.Reason)}</reason>
                        <type>{SecurityElement.Escape(request.Type)}</type>
                        <documents>
                            {string.Join("", request.Documents.Select(d => 
                                $"<document><type>{SecurityElement.Escape(d.Type)}</type><id>{SecurityElement.Escape(d.Id)}</id></document>"))}
                        </documents>
                        <refundMethod>{SecurityElement.Escape(request.RefundMethod)}</refundMethod>
                    </CancelPolicyRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("CancelPolicy", soapEnvelope);
            return ParseCancellationResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel policy in IMS");
            throw new IMSRequestException("Failed to cancel policy", ex);
        }
    }

    public async Task<BillingScheduleResponse> GetBillingScheduleAsync(string policyId)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <GetBillingScheduleRequest>
                        <policyId>{SecurityElement.Escape(policyId)}</policyId>
                    </GetBillingScheduleRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("GetBillingSchedule", soapEnvelope);
            return ParseBillingScheduleResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get billing schedule from IMS");
            throw new IMSRequestException("Failed to get billing schedule", ex);
        }
    }

    public async Task<InvoiceResponse> GenerateInvoiceAsync(InvoiceRequest request)
    {
        var soapEnvelope = $@"
            <soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
                <soapenv:Header/>
                <soapenv:Body>
                    <GenerateInvoiceRequest>
                        <policyId>{SecurityElement.Escape(request.PolicyId)}</policyId>
                        <dueDate>{request.DueDate:yyyy-MM-dd}</dueDate>
                        <items>
                            {string.Join("", request.Items.Select(i => $@"
                                <item>
                                    <description>{SecurityElement.Escape(i.Description)}</description>
                                    <amount>{i.Amount}</amount>
                                    <type>{SecurityElement.Escape(i.Type)}</type>
                                </item>"))}
                        </items>
                        <deliveryMethod>{SecurityElement.Escape(request.DeliveryMethod)}</deliveryMethod>
                    </GenerateInvoiceRequest>
                </soapenv:Body>
            </soapenv:Envelope>";

        try
        {
            var response = await SendSoapRequestAsync("GenerateInvoice", soapEnvelope);
            return ParseInvoiceResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate invoice in IMS");
            throw new IMSRequestException("Failed to generate invoice", ex);
        }
    }

    private QuoteResponse ParseQuoteResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var quoteNode = doc.SelectSingleNode("//CreateQuoteResponse");
        if (quoteNode == null)
            throw new IMSRequestException("Invalid quote response format");

        return new QuoteResponse
        {
            QuoteId = quoteNode.SelectSingleNode("quoteId")?.InnerText,
            Premium = decimal.Parse(quoteNode.SelectSingleNode("premium")?.InnerText ?? "0"),
            EffectiveDate = DateTime.Parse(quoteNode.SelectSingleNode("effectiveDate")?.InnerText),
            ExpirationDate = DateTime.Parse(quoteNode.SelectSingleNode("expirationDate")?.InnerText),
            Status = quoteNode.SelectSingleNode("status")?.InnerText
        };
    }

    private InsuredResponse ParseInsuredResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var insuredNode = doc.SelectSingleNode("//CreateInsuredResponse | //GetInsuredResponse");
        if (insuredNode == null)
            throw new IMSRequestException("Invalid insured response format");

        return new InsuredResponse
        {
            InsuredId = insuredNode.SelectSingleNode("insuredId")?.InnerText,
            FirstName = insuredNode.SelectSingleNode("name/first")?.InnerText,
            LastName = insuredNode.SelectSingleNode("name/last")?.InnerText,
            Email = insuredNode.SelectSingleNode("contact/email")?.InnerText,
            Phone = insuredNode.SelectSingleNode("contact/phone")?.InnerText,
            Address = new Address
            {
                Street1 = insuredNode.SelectSingleNode("address/street1")?.InnerText,
                Street2 = insuredNode.SelectSingleNode("address/street2")?.InnerText,
                City = insuredNode.SelectSingleNode("address/city")?.InnerText,
                State = insuredNode.SelectSingleNode("address/state")?.InnerText,
                Zip = insuredNode.SelectSingleNode("address/zip")?.InnerText
            }
        };
    }

    private PolicyResponse ParsePolicyResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var policyNode = doc.SelectSingleNode("//CreatePolicyResponse");
        if (policyNode == null)
            throw new IMSRequestException("Invalid policy response format");

        return new PolicyResponse
        {
            PolicyId = policyNode.SelectSingleNode("policyId")?.InnerText,
            Status = policyNode.SelectSingleNode("status")?.InnerText,
            EffectiveDate = DateTime.Parse(policyNode.SelectSingleNode("effectiveDate")?.InnerText),
            ExpirationDate = DateTime.Parse(policyNode.SelectSingleNode("expirationDate")?.InnerText),
            Premium = decimal.Parse(policyNode.SelectSingleNode("premium")?.InnerText ?? "0"),
            Documents = ParseDocumentList(policyNode.SelectNodes("documents/document"))
        };
    }

    private DocumentResponse ParseDocumentResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var documentNode = doc.SelectSingleNode("//UploadDocumentResponse");
        if (documentNode == null)
            throw new IMSRequestException("Invalid document response format");

        return new DocumentResponse
        {
            DocumentId = documentNode.SelectSingleNode("documentId")?.InnerText,
            Status = documentNode.SelectSingleNode("status")?.InnerText,
            Url = documentNode.SelectSingleNode("url")?.InnerText
        };
    }

    private byte[] ParseDocumentContent(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var contentNode = doc.SelectSingleNode("//GetDocumentResponse/content");
        if (contentNode == null)
            throw new IMSRequestException("Invalid document content response format");

        return Convert.FromBase64String(contentNode.InnerText);
    }

    private SubmissionResponse ParseSubmissionResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var submissionNode = doc.SelectSingleNode("//CreateSubmissionResponse");
        if (submissionNode == null)
            throw new IMSRequestException("Invalid submission response format");

        return new SubmissionResponse
        {
            SubmissionId = submissionNode.SelectSingleNode("submissionId")?.InnerText,
            Status = submissionNode.SelectSingleNode("status")?.InnerText,
            UnderwriterId = submissionNode.SelectSingleNode("underwriterId")?.InnerText,
            CreatedDate = DateTime.Parse(submissionNode.SelectSingleNode("createdDate")?.InnerText),
            Documents = ParseDocumentList(submissionNode.SelectNodes("documents/document"))
        };
    }

    private ClaimResponse ParseClaimResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var claimNode = doc.SelectSingleNode("//CreateClaimResponse");
        if (claimNode == null)
            throw new IMSRequestException("Invalid claim response format");

        return new ClaimResponse
        {
            ClaimId = claimNode.SelectSingleNode("claimId")?.InnerText,
            Status = claimNode.SelectSingleNode("status")?.InnerText,
            AdjusterId = claimNode.SelectSingleNode("adjusterId")?.InnerText,
            CreatedDate = DateTime.Parse(claimNode.SelectSingleNode("createdDate")?.InnerText),
            Documents = ParseDocumentList(claimNode.SelectNodes("documents/document"))
        };
    }

    private EndorsementResponse ParseEndorsementResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var endorsementNode = doc.SelectSingleNode("//CreateEndorsementResponse");
        if (endorsementNode == null)
            throw new IMSRequestException("Invalid endorsement response format");

        return new EndorsementResponse
        {
            EndorsementId = endorsementNode.SelectSingleNode("endorsementId")?.InnerText,
            Status = endorsementNode.SelectSingleNode("status")?.InnerText,
            EffectiveDate = DateTime.Parse(endorsementNode.SelectSingleNode("effectiveDate")?.InnerText),
            PremiumChange = decimal.Parse(endorsementNode.SelectSingleNode("premiumChange")?.InnerText ?? "0"),
            Documents = ParseDocumentList(endorsementNode.SelectNodes("documents/document"))
        };
    }

    private PaymentResponse ParsePaymentResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var paymentNode = doc.SelectSingleNode("//ProcessPaymentResponse");
        if (paymentNode == null)
            throw new IMSRequestException("Invalid payment response format");

        return new PaymentResponse
        {
            TransactionId = paymentNode.SelectSingleNode("transactionId")?.InnerText,
            Status = paymentNode.SelectSingleNode("status")?.InnerText,
            Amount = decimal.Parse(paymentNode.SelectSingleNode("amount")?.InnerText ?? "0"),
            ProcessedDate = DateTime.Parse(paymentNode.SelectSingleNode("processedDate")?.InnerText),
            ReceiptUrl = paymentNode.SelectSingleNode("receiptUrl")?.InnerText
        };
    }

    private RenewalResponse ParseRenewalResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var renewalNode = doc.SelectSingleNode("//CreateRenewalResponse");
        if (renewalNode == null)
            throw new IMSRequestException("Invalid renewal response format");

        return new RenewalResponse
        {
            RenewalId = renewalNode.SelectSingleNode("renewalId")?.InnerText,
            Status = renewalNode.SelectSingleNode("status")?.InnerText,
            EffectiveDate = DateTime.Parse(renewalNode.SelectSingleNode("effectiveDate")?.InnerText),
            ExpirationDate = DateTime.Parse(renewalNode.SelectSingleNode("expirationDate")?.InnerText),
            Premium = decimal.Parse(renewalNode.SelectSingleNode("premium")?.InnerText ?? "0"),
            Documents = ParseDocumentList(renewalNode.SelectNodes("documents/document"))
        };
    }

    private CancellationResponse ParseCancellationResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var cancellationNode = doc.SelectSingleNode("//CancelPolicyResponse");
        if (cancellationNode == null)
            throw new IMSRequestException("Invalid cancellation response format");

        return new CancellationResponse
        {
            CancellationId = cancellationNode.SelectSingleNode("cancellationId")?.InnerText,
            Status = cancellationNode.SelectSingleNode("status")?.InnerText,
            EffectiveDate = DateTime.Parse(cancellationNode.SelectSingleNode("effectiveDate")?.InnerText),
            RefundAmount = decimal.Parse(cancellationNode.SelectSingleNode("refundAmount")?.InnerText ?? "0"),
            Documents = ParseDocumentList(cancellationNode.SelectNodes("documents/document"))
        };
    }

    private BillingScheduleResponse ParseBillingScheduleResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var scheduleNode = doc.SelectSingleNode("//GetBillingScheduleResponse");
        if (scheduleNode == null)
            throw new IMSRequestException("Invalid billing schedule response format");

        var installments = new List<BillingInstallment>();
        var installmentNodes = scheduleNode.SelectNodes("installments/installment");
        
        if (installmentNodes != null)
        {
            foreach (XmlNode node in installmentNodes)
            {
                installments.Add(new BillingInstallment
                {
                    DueDate = DateTime.Parse(node.SelectSingleNode("dueDate")?.InnerText),
                    Amount = decimal.Parse(node.SelectSingleNode("amount")?.InnerText ?? "0"),
                    Status = node.SelectSingleNode("status")?.InnerText,
                    PaymentMethod = node.SelectSingleNode("paymentMethod")?.InnerText
                });
            }
        }

        return new BillingScheduleResponse
        {
            PolicyId = scheduleNode.SelectSingleNode("policyId")?.InnerText,
            TotalPremium = decimal.Parse(scheduleNode.SelectSingleNode("totalPremium")?.InnerText ?? "0"),
            PaymentPlan = scheduleNode.SelectSingleNode("paymentPlan")?.InnerText,
            Installments = installments
        };
    }

    private InvoiceResponse ParseInvoiceResponse(string soapResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(soapResponse);

        var invoiceNode = doc.SelectSingleNode("//GenerateInvoiceResponse");
        if (invoiceNode == null)
            throw new IMSRequestException("Invalid invoice response format");

        return new InvoiceResponse
        {
            InvoiceId = invoiceNode.SelectSingleNode("invoiceId")?.InnerText,
            Status = invoiceNode.SelectSingleNode("status")?.InnerText,
            Amount = decimal.Parse(invoiceNode.SelectSingleNode("amount")?.InnerText ?? "0"),
            DueDate = DateTime.Parse(invoiceNode.SelectSingleNode("dueDate")?.InnerText),
            DocumentId = invoiceNode.SelectSingleNode("documentId")?.InnerText,
            DeliveryStatus = invoiceNode.SelectSingleNode("deliveryStatus")?.InnerText
        };
    }

    private List<DocumentInfo> ParseDocumentList(XmlNodeList documentNodes)
    {
        var documents = new List<DocumentInfo>();
        
        if (documentNodes != null)
        {
            foreach (XmlNode node in documentNodes)
            {
                documents.Add(new DocumentInfo
                {
                    Id = node.SelectSingleNode("id")?.InnerText,
                    Type = node.SelectSingleNode("type")?.InnerText,
                    Url = node.SelectSingleNode("url")?.InnerText
                });
            }
        }
        
        return documents;
    }
}

public class IMSSettings
{
    public string BaseUrl { get; set; }
    public string SoapEndpoint { get; set; }
    public int TokenExpirationMinutes { get; set; } = 60;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
}

public class IMSAuthenticationException : Exception
{
    public IMSAuthenticationException(string message) 
        : base(message) { }
    
    public IMSAuthenticationException(string message, Exception inner) 
        : base(message, inner) { }
}

public class IMSRequestException : Exception
{
    public IMSRequestException(string message) 
        : base(message) { }
    
    public IMSRequestException(string message, Exception inner) 
        : base(message, inner) { }
} 