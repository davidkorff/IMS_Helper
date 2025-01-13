using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Moq.Protected;
using ApiGateway.Extensions;
using ApiGateway.Models;

public class RequestForwardingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;

    public RequestForwardingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        var client = new HttpClient(_mockHttpHandler.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(client);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_httpClientFactoryMock.Object);
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("testroutes.json", optional: false);
            });
        });
    }

    [Fact]
    public async Task ForwardRequest_SuccessfullyRoutesToDestination()
    {
        // Arrange
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test response")
        };

        SetupMockHandler(expectedResponse);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("test response", content);
    }

    [Fact]
    public async Task ForwardRequest_PreservesHeaders()
    {
        // Arrange
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        SetupMockHandler(expectedResponse);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Custom-Header", "test-value");

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Headers.Contains("X-Custom-Header") &&
                req.Headers.GetValues("X-Custom-Header").First() == "test-value"),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task ForwardRequest_HandlesRetryPolicy()
    {
        // Arrange
        var failureResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("success after retry")
        };

        var requestCount = 0;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                requestCount++;
                return requestCount < 2 ? failureResponse : successResponse;
            });

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test-retry");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("success after retry", content);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task ForwardRequest_HandlesTimeout()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns<HttpRequestMessage, CancellationToken>(async (_, _) =>
            {
                await Task.Delay(2000); // Delay longer than timeout
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var client = _factory.CreateClient();

        // Act & Assert
        await Assert.ThrowsAsync<RequestForwardingException>(() =>
            client.GetAsync("/api/test-timeout"));
    }

    private void SetupMockHandler(HttpResponseMessage response)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);
    }
} 