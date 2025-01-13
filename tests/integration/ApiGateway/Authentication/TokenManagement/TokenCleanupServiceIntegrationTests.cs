using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class TokenCleanupServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITokenManagementService _tokenManagement;
    private readonly IDistributedCache _cache;
    private readonly List<string> _testTokenIds;

    public TokenCleanupServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var scope = _factory.Services.CreateScope();
        _tokenManagement = scope.ServiceProvider.GetRequiredService<ITokenManagementService>();
        _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        _testTokenIds = new List<string>();
    }

    public async Task InitializeAsync()
    {
        // Setup test data before each test
        await CreateTestTokens();
    }

    public async Task DisposeAsync()
    {
        // Cleanup test data after each test
        foreach (var tokenId in _testTokenIds)
        {
            await _cache.RemoveAsync($"token:{tokenId}");
        }
    }

    [Fact]
    public async Task CleanupService_RemovesExpiredTokens()
    {
        // Arrange
        var cleanupService = _factory.Services.GetRequiredService<TokenCleanupService>();
        await cleanupService.StartAsync(CancellationToken.None);

        // Act - Wait for cleanup cycle
        await Task.Delay(TimeSpan.FromSeconds(70)); // Allow time for cleanup

        // Assert
        foreach (var tokenId in _testTokenIds.Take(2)) // First two tokens were expired
        {
            var metadata = await _tokenManagement.GetTokenMetadataAsync(tokenId);
            Assert.Null(metadata); // Should be removed
        }

        var activeToken = await _tokenManagement.GetTokenMetadataAsync(_testTokenIds[2]);
        Assert.NotNull(activeToken); // Should still exist
        Assert.Equal(TokenStatus.Active, activeToken.Status);

        // Cleanup
        await cleanupService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CleanupService_HandlesLargeNumberOfTokens()
    {
        // Arrange
        const int tokenCount = 100;
        var tokens = new List<string>();
        
        for (int i = 0; i < tokenCount; i++)
        {
            var metadata = await _tokenManagement.StoreTokenAsync(
                $"user{i}",
                $"access{i}",
                $"refresh{i}");
            
            if (i % 2 == 0) // Make half the tokens expired
            {
                metadata.ExpiresAt = DateTime.UtcNow.AddDays(-1);
                await UpdateTokenMetadata(metadata);
            }
            
            tokens.Add(metadata.TokenId);
        }

        var cleanupService = _factory.Services.GetRequiredService<TokenCleanupService>();
        await cleanupService.StartAsync(CancellationToken.None);

        // Act
        await Task.Delay(TimeSpan.FromSeconds(70)); // Allow time for cleanup

        // Assert
        var remainingTokens = await Task.WhenAll(
            tokens.Select(t => _tokenManagement.GetTokenMetadataAsync(t)));
        
        Assert.Equal(tokenCount / 2, remainingTokens.Count(t => t != null));

        // Cleanup
        await cleanupService.StopAsync(CancellationToken.None);
        foreach (var token in tokens)
        {
            await _cache.RemoveAsync($"token:{token}");
        }
    }

    [Fact]
    public async Task CleanupService_HandlesServiceRestart()
    {
        // Arrange
        var cleanupService = _factory.Services.GetRequiredService<TokenCleanupService>();
        await cleanupService.StartAsync(CancellationToken.None);

        // Act
        await Task.Delay(TimeSpan.FromSeconds(30));
        await cleanupService.StopAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(5));
        await cleanupService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(40));

        // Assert
        foreach (var tokenId in _testTokenIds.Take(2))
        {
            var metadata = await _tokenManagement.GetTokenMetadataAsync(tokenId);
            Assert.Null(metadata);
        }

        // Cleanup
        await cleanupService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CleanupService_HandlesTokenRevocation()
    {
        // Arrange
        var cleanupService = _factory.Services.GetRequiredService<TokenCleanupService>();
        await cleanupService.StartAsync(CancellationToken.None);

        // Act
        await _tokenManagement.RevokeTokenAsync(_testTokenIds[2]); // Revoke active token
        await Task.Delay(TimeSpan.FromSeconds(70));

        // Assert
        var revokedToken = await _tokenManagement.GetTokenMetadataAsync(_testTokenIds[2]);
        Assert.Null(revokedToken); // Should be cleaned up

        // Cleanup
        await cleanupService.StopAsync(CancellationToken.None);
    }

    private async Task CreateTestTokens()
    {
        // Create expired tokens
        var expiredToken1 = await _tokenManagement.StoreTokenAsync(
            "user1", "access1", "refresh1");
        expiredToken1.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await UpdateTokenMetadata(expiredToken1);
        _testTokenIds.Add(expiredToken1.TokenId);

        var expiredToken2 = await _tokenManagement.StoreTokenAsync(
            "user2", "access2", "refresh2");
        expiredToken2.ExpiresAt = DateTime.UtcNow.AddHours(-12);
        await UpdateTokenMetadata(expiredToken2);
        _testTokenIds.Add(expiredToken2.TokenId);

        // Create active token
        var activeToken = await _tokenManagement.StoreTokenAsync(
            "user3", "access3", "refresh3");
        _testTokenIds.Add(activeToken.TokenId);
    }

    private async Task UpdateTokenMetadata(TokenMetadata metadata)
    {
        await _cache.SetStringAsync(
            $"token:{metadata.TokenId}",
            JsonSerializer.Serialize(metadata),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            });
    }
} 