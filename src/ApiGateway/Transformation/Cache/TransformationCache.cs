using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class TransformationCache : ITransformationCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<TransformationCache> _logger;
    private readonly TransformationOptions _options;

    public TransformationCache(
        IDistributedCache cache,
        ILogger<TransformationCache> logger,
        IOptions<TransformationOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Stream> GetTransformedContentAsync(
        string cacheKey,
        string etag)
    {
        try
        {
            var cacheEntry = await GetCacheEntryAsync(cacheKey);
            if (cacheEntry == null) return null;

            if (etag != null && cacheEntry.ETag == etag)
            {
                return null; // Not modified
            }

            return new MemoryStream(cacheEntry.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error retrieving transformed content for key {Key}", 
                cacheKey);
            return null;
        }
    }

    public async Task CacheTransformedContentAsync(
        string cacheKey,
        Stream content,
        string contentType,
        TimeSpan? expiration = null)
    {
        try
        {
            var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            content.Position = 0;
            ms.Position = 0;

            var entry = new TransformedContentCacheEntry
            {
                Content = ms.ToArray(),
                ContentType = contentType,
                ETag = GenerateETag(ms),
                Timestamp = DateTimeOffset.UtcNow
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = 
                    expiration ?? TimeSpan.FromMinutes(60)
            };

            await _cache.SetAsync(
                cacheKey,
                SerializeCacheEntry(entry),
                options);

            _logger.LogDebug(
                "Cached transformed content for key {Key}, ETag: {ETag}",
                cacheKey,
                entry.ETag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error caching transformed content for key {Key}",
                cacheKey);
        }
    }

    public async Task<string> GetETagAsync(string cacheKey)
    {
        var entry = await GetCacheEntryAsync(cacheKey);
        return entry?.ETag;
    }

    private async Task<TransformedContentCacheEntry> GetCacheEntryAsync(
        string cacheKey)
    {
        var data = await _cache.GetAsync(cacheKey);
        if (data == null) return null;

        return DeserializeCacheEntry(data);
    }

    private static string GenerateETag(Stream content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(
            ((MemoryStream)content).ToArray());
        return Convert.ToBase64String(hash);
    }

    private static byte[] SerializeCacheEntry(
        TransformedContentCacheEntry entry)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            entry,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
    }

    private static TransformedContentCacheEntry DeserializeCacheEntry(
        byte[] data)
    {
        return JsonSerializer.Deserialize<TransformedContentCacheEntry>(
            data,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
    }
}

public class TransformedContentCacheEntry
{
    public byte[] Content { get; set; }
    public string ContentType { get; set; }
    public string ETag { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public interface ITransformationCache
{
    Task<Stream> GetTransformedContentAsync(string cacheKey, string etag);
    Task CacheTransformedContentAsync(
        string cacheKey,
        Stream content,
        string contentType,
        TimeSpan? expiration = null);
    Task<string> GetETagAsync(string cacheKey);
} 