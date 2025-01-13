using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class RateLimitingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public RateLimitingIntegrationTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddDistributedMemoryCache();
                services.Configure<RateLimitingOptions>(options =>
                {
                    options.DefaultPolicy = new RateLimitPolicy
                    {
                        RequestsPerMinute = 30,
                        BurstLimit = 5
                    };
                });
            });
        });
        _output = output;
    }

    [Fact]
    public async Task RateLimit_EnforcesLimit()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        // Send requests up to the limit
        for (int i = 0; i < 30; i++)
        {
            tasks.Add(client.GetAsync(endpoint));
        }

        var responses = await Task.WhenAll(tasks);
        var additionalResponse = await client.GetAsync(endpoint);

        // Assert
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, additionalResponse.StatusCode);

        var retryAfter = additionalResponse.Headers.GetValues("Retry-After").FirstOrDefault();
        Assert.NotNull(retryAfter);
        Assert.True(int.Parse(retryAfter) > 0);
    }

    [Fact]
    public async Task BurstLimit_PreventsSpikes()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        // Send burst of concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(client.GetAsync(endpoint));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task RateLimit_ResetsAfterWindow()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";

        // Act
        // Exhaust the rate limit
        var tasks = Enumerable.Range(0, 30)
            .Select(_ => client.GetAsync(endpoint));
        await Task.WhenAll(tasks);

        // Wait for the window to reset
        await Task.Delay(TimeSpan.FromSeconds(61));

        // Try another request
        var response = await client.GetAsync(endpoint);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExcludedPaths_BypassRateLimit()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/health";
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        // Send many requests to excluded path
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(client.GetAsync(endpoint));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task Headers_IncludeRateLimitInfo()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";

        // Act
        var response = await client.GetAsync(endpoint);

        // Assert
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
        Assert.True(response.Headers.Contains("X-RateLimit-Reset"));

        var remaining = int.Parse(
            response.Headers.GetValues("X-RateLimit-Remaining").First());
        Assert.Equal(29, remaining);
    }
} 