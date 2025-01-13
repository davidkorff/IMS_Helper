using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

public class IMSConnectionPool
{
    private readonly ConcurrentDictionary<string, IMSClientWrapper> _pool;
    private readonly IIMSClientFactory _clientFactory;
    private readonly ICredentialEncryptionService _encryptionService;
    private readonly ILogger<IMSConnectionPool> _logger;
    private readonly int _maxPoolSize;
    private readonly TimeSpan _connectionTimeout;

    public IMSConnectionPool(
        IIMSClientFactory clientFactory,
        ICredentialEncryptionService encryptionService,
        ILogger<IMSConnectionPool> logger,
        IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
        _pool = new ConcurrentDictionary<string, IMSClientWrapper>();
        _maxPoolSize = configuration.GetValue<int>("IMS:MaxPoolSize", 100);
        _connectionTimeout = TimeSpan.FromMinutes(
            configuration.GetValue<int>("IMS:ConnectionTimeoutMinutes", 30));
    }

    public async Task<IIMSClient> GetClientAsync(string connectionId, IMSConnection connection)
    {
        if (_pool.TryGetValue(connectionId, out var wrapper) && 
            !wrapper.IsExpired(_connectionTimeout))
        {
            return wrapper.Client;
        }

        return await CreateAndCacheClientAsync(connectionId, connection);
    }

    private async Task<IIMSClient> CreateAndCacheClientAsync(
        string connectionId, 
        IMSConnection connection)
    {
        if (_pool.Count >= _maxPoolSize)
        {
            CleanupExpiredConnections();
        }

        var credentials = _encryptionService.DecryptCredentials(
            connection.EncryptedCredentials);
        var client = _clientFactory.CreateClient();
        
        await client.AuthenticateAsync(credentials);

        var wrapper = new IMSClientWrapper(client);
        _pool.AddOrUpdate(connectionId, wrapper, (_, __) => wrapper);

        return client;
    }

    private void CleanupExpiredConnections()
    {
        var expiredKeys = _pool
            .Where(kvp => kvp.Value.IsExpired(_connectionTimeout))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _pool.TryRemove(key, out _);
        }
    }

    private class IMSClientWrapper
    {
        public IIMSClient Client { get; }
        public DateTime LastUsed { get; private set; }

        public IMSClientWrapper(IIMSClient client)
        {
            Client = client;
            LastUsed = DateTime.UtcNow;
        }

        public bool IsExpired(TimeSpan timeout)
        {
            return DateTime.UtcNow - LastUsed > timeout;
        }
    }
} 