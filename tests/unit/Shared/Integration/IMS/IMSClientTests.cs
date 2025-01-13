using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IMSClient;

public class IMSClientTests
{
    private readonly IMSClient _client;
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly Mock<ILogger<IMSClient>> _loggerMock;
    private readonly IMSSettings _settings;

    public IMSClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_handlerMock.Object);
        _loggerMock = new Mock<ILogger<IMSClient>>();
        _settings = new IMSSettings
        {
            BaseUrl = "https://webservices.mgasystems.com/ims_demo/",
            ProgramCode = "TEST1",
            ClientId = "123"
        };

        _client = new IMSClient(
            httpClient,
            _loggerMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var expectedToken = Guid.NewGuid().ToString();
        SetupMockResponse("Logon.asmx", CreateLoginResponse(expectedToken));

        // Act
        var token = await _client.LoginAsync("TEST1", "test@test.com", "password");

        // Assert
        Assert.Equal(expectedToken, token);
        VerifyHttpCall("Logon.asmx", Times.Once());
    }

    [Fact]
    public async Task GetPolicy_ValidPolicyId_ReturnsPolicyResponse()
    {
        // Arrange
        var policyId = "P123456";
        var policy = CreateSamplePolicy(policyId);
        SetupMockResponse("QuoteFunctions.asmx", CreatePolicyResponse(policy));

        // Act
        var response = await _client.GetPolicy(policyId);

        // Assert
        Assert.Equal(policyId, response.PolicyId);
        Assert.Equal(PolicyStatus.Active, response.Status);
        VerifyHttpCall("QuoteFunctions.asmx", Times.Once());
    }

    [Fact]
    public async Task CreateClaim_ValidRequest_ReturnsClaimResponse()
    {
        // Arrange
        var request = new CreateClaimRequest
        {
            PolicyId = "P123456",
            DateOfLoss = DateTime.UtcNow.AddDays(-1),
            Type = ClaimType.Property
        };

        var expectedClaimId = "C789";
        SetupMockResponse("ClaimFunctions.asmx", CreateClaimResponse(expectedClaimId));

        // Act
        var response = await _client.CreateClaim(request);

        // Assert
        Assert.Equal(expectedClaimId, response.ClaimId);
        Assert.Equal(ClaimStatus.New, response.Status);
        VerifyHttpCall("ClaimFunctions.asmx", Times.Once());
    }

    [Fact]
    public async Task GetValidCompanyLines_ValidProgramCode_ReturnsCompanyLines()
    {
        // Arrange
        var programCode = "TEST1";
        var companyLines = CreateSampleCompanyLines();
        SetupMockResponse("DataAccess.asmx", CreateCompanyLinesResponse(companyLines));

        // Act
        var response = await _client.GetValidCompanyLines(programCode);

        // Assert
        Assert.NotNull(response.CompanyLines);
        Assert.NotEmpty(response.CompanyLines);
        VerifyHttpCall("DataAccess.asmx", Times.Once());
    }

    [Fact]
    public async Task SendRequest_TransientError_RetriesAndSucceeds()
    {
        // Arrange
        var policyId = "P123456";
        var policy = CreateSamplePolicy(policyId);
        
        SetupMockResponseWithRetry("QuoteFunctions.asmx", 
            CreatePolicyResponse(policy),
            HttpStatusCode.ServiceUnavailable,
            2);

        // Act
        var response = await _client.GetPolicy(policyId);

        // Assert
        Assert.Equal(policyId, response.PolicyId);
        VerifyHttpCall("QuoteFunctions.asmx", Times.Exactly(3));
    }

    [Fact]
    public async Task SendRequest_PermanentError_ThrowsException()
    {
        // Arrange
        SetupMockResponseWithError("QuoteFunctions.asmx", 
            HttpStatusCode.BadRequest,
            "Invalid request");

        // Act & Assert
        await Assert.ThrowsAsync<IMSException>(() => 
            _client.GetPolicy("invalid"));
    }

    private void SetupMockResponse(string service, string response)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri.PathAndQuery.Contains(service)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(response)
            });
    }

    private void SetupMockResponseWithRetry(
        string service, 
        string successResponse, 
        HttpStatusCode errorCode, 
        int errorCount)
    {
        var responses = new Queue<HttpResponseMessage>();
        
        // Add error responses
        for (int i = 0; i < errorCount; i++)
        {
            responses.Enqueue(new HttpResponseMessage
            {
                StatusCode = errorCode,
                Content = new StringContent("Service Unavailable")
            });
        }

        // Add success response
        responses.Enqueue(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(successResponse)
        });

        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri.PathAndQuery.Contains(service)),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(responses.Dequeue()));
    }

    private void VerifyHttpCall(string service, Times times)
    {
        _handlerMock
            .Protected()
            .Verify(
                "SendAsync",
                times,
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.RequestUri.PathAndQuery.Contains(service)),
                ItExpr.IsAny<CancellationToken>());
    }

    private string CreateLoginResponse(string token)
    {
        return $@"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <LoginResponse xmlns='http://tempuri.org/'>
                        <Token>{token}</Token>
                    </LoginResponse>
                </soap:Body>
            </soap:Envelope>";
    }

    private string CreatePolicyResponse(PolicyXml policy)
    {
        var serializer = new XmlSerializer(typeof(PolicyXml));
        using var writer = new StringWriter();
        serializer.Serialize(writer, policy);
        
        return $@"
            <soap:Envelope xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                <soap:Body>
                    <GetPolicyResponse xmlns='http://tempuri.org/'>
                        <Policy>{writer.ToString()}</Policy>
                    </GetPolicyResponse>
                </soap:Body>
            </soap:Envelope>";
    }

    private PolicyXml CreateSamplePolicy(string policyId)
    {
        return new PolicyXml
        {
            PolicyId = policyId,
            QuoteId = "Q" + policyId.Substring(1),
            PolicyNumber = "POL-" + policyId,
            Status = "Active",
            EffectiveDate = DateTime.UtcNow,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            Insured = new InsuredXml
            {
                InsuredId = "I123",
                Name = "Test Insured",
                Address1 = "123 Test St",
                City = "Test City",
                State = "TX",
                Zip = "12345"
            }
        };
    }

    private CompanyLineResponse CreateSampleCompanyLines()
    {
        return new CompanyLineResponse
        {
            CompanyLines = new List<CompanyLine>
            {
                new CompanyLine
                {
                    CompanyLineGUID = Guid.NewGuid().ToString(),
                    LocationName = "Test Company",
                    LineName = "Test Line",
                    StateID = "TX",
                    BillTypes = new List<BillType>
                    {
                        new BillType { BillingTypeID = 1, BillingType = "Direct Bill" }
                    }
                }
            }
        };
    }
} 