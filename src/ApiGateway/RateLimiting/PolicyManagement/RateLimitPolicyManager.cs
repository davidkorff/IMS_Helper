using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class RateLimitPolicyManager : IRateLimitPolicyManager
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RateLimitPolicyManager> _logger;
    private readonly RateLimitingOptions _options;
    private readonly string _policyCacheKey = "rate_limit_policies";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RateLimitPolicyManager(
        IDistributedCache cache,
        ILogger<RateLimitPolicyManager> logger,
        IOptions<RateLimitingOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IEnumerable<RateLimitPolicyConfig>> GetAllPoliciesAsync()
    {
        try
        {
            var data = await _cache.GetAsync(_policyCacheKey);
            if (data == null) return Array.Empty<RateLimitPolicyConfig>();

            return JsonSerializer.Deserialize<List<RateLimitPolicyConfig>>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rate limit policies");
            return Array.Empty<RateLimitPolicyConfig>();
        }
    }

    public async Task<RateLimitPolicyConfig> GetPolicyAsync(string endpoint)
    {
        var policies = await GetAllPoliciesAsync();
        return policies.FirstOrDefault(p => 
            PathMatchesPattern(endpoint, p.PathPattern));
    }

    public async Task AddOrUpdatePolicyAsync(RateLimitPolicyConfig policy)
    {
        await _lock.WaitAsync();
        try
        {
            var policies = (await GetAllPoliciesAsync()).ToList();
            
            var existingIndex = policies.FindIndex(p => 
                p.PathPattern == policy.PathPattern);
            
            if (existingIndex >= 0)
            {
                policies[existingIndex] = policy;
            }
            else
            {
                policies.Add(policy);
            }

            await SavePoliciesAsync(policies);
            
            _logger.LogInformation(
                "Rate limit policy {Pattern} updated/added", 
                policy.PathPattern);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemovePolicyAsync(string pathPattern)
    {
        await _lock.WaitAsync();
        try
        {
            var policies = (await GetAllPoliciesAsync())
                .Where(p => p.PathPattern != pathPattern)
                .ToList();

            await SavePoliciesAsync(policies);
            
            _logger.LogInformation(
                "Rate limit policy {Pattern} removed", 
                pathPattern);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ValidatePolicyAsync(RateLimitPolicyConfig policy)
    {
        if (string.IsNullOrEmpty(policy.PathPattern))
            return false;

        if (policy.RequestsPerMinute <= 0)
            return false;

        if (policy.BurstLimit <= 0)
            return false;

        var existingPolicies = await GetAllPoliciesAsync();
        foreach (var existing in existingPolicies)
        {
            if (existing.PathPattern == policy.PathPattern)
                continue;

            if (PathPatternsConflict(existing.PathPattern, policy.PathPattern))
                return false;
        }

        return true;
    }

    private async Task SavePoliciesAsync(IEnumerable<RateLimitPolicyConfig> policies)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(policies);
        await _cache.SetAsync(
            _policyCacheKey,
            data,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
            });
    }

    private static bool PathMatchesPattern(string path, string pattern)
    {
        if (pattern.EndsWith("/**"))
        {
            var prefix = pattern[..^3];
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.Contains('*'))
        {
            var regex = new Regex(
                "^" + Regex.Escape(pattern).Replace("\\*", "[^/]+") + "$",
                RegexOptions.IgnoreCase);
            return regex.IsMatch(path);
        }

        return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathPatternsConflict(string pattern1, string pattern2)
    {
        // Check for exact matches
        if (pattern1 == pattern2) return true;

        // Check if one pattern is a subset of another
        if (pattern1.EndsWith("/**") && pattern2.StartsWith(pattern1[..^3]))
            return true;

        if (pattern2.EndsWith("/**") && pattern1.StartsWith(pattern2[..^3]))
            return true;

        // Check for wildcard conflicts
        var regex1 = new Regex(
            "^" + Regex.Escape(pattern1).Replace("\\*", "[^/]+") + "$",
            RegexOptions.IgnoreCase);
        var regex2 = new Regex(
            "^" + Regex.Escape(pattern2).Replace("\\*", "[^/]+") + "$",
            RegexOptions.IgnoreCase);

        // Generate test paths for each pattern
        var testPaths = GenerateTestPaths(pattern1)
            .Concat(GenerateTestPaths(pattern2));

        // Check if any test path matches both patterns
        return testPaths.Any(path => 
            regex1.IsMatch(path) && regex2.IsMatch(path));
    }

    private static IEnumerable<string> GenerateTestPaths(string pattern)
    {
        var parts = pattern.Split('/');
        var result = new List<string>();
        var current = new List<string>();

        void GenerateCombinations(int index)
        {
            if (index == parts.Length)
            {
                result.Add(string.Join("/", current));
                return;
            }

            if (parts[index] == "*")
            {
                current.Add("test");
                GenerateCombinations(index + 1);
                current.RemoveAt(current.Count - 1);
            }
            else if (parts[index] == "**")
            {
                // Test with 0, 1, and 2 levels
                GenerateCombinations(index + 1);
                
                current.Add("test");
                GenerateCombinations(index + 1);
                current.RemoveAt(current.Count - 1);
                
                current.Add("test");
                current.Add("nested");
                GenerateCombinations(index + 1);
                current.RemoveAt(current.Count - 1);
                current.RemoveAt(current.Count - 1);
            }
            else
            {
                current.Add(parts[index]);
                GenerateCombinations(index + 1);
                current.RemoveAt(current.Count - 1);
            }
        }

        GenerateCombinations(0);
        return result;
    }
} 