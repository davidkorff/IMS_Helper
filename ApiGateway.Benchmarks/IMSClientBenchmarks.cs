using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using ApiGateway.Interfaces;

public class IMSClientBenchmarks
{
    private IIMSClient _client;
    private IMSCredentials _credentials;
    private IMSConnectionPool _connectionPool;
    private string _connectionId;

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup test dependencies
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Initialize test client
        _client = serviceProvider.GetRequiredService<IIMSClient>();
        _credentials = new IMSCredentials
        {
            Username = "benchmark_user",
            Password = "benchmark_pass",
            AccountId = "benchmark_account",
            Url = "https://benchmark-ims.example.com"
        };

        // Setup connection pool
        _connectionPool = serviceProvider.GetRequiredService<IMSConnectionPool>();
        var connection = new IMSConnection
        {
            Id = Guid.NewGuid().ToString(),
            ConnectionName = "Benchmark Connection",
            Environment = "Test"
        };
        _connectionId = connection.Id;

        // Authenticate
        await _client.AuthenticateAsync(_credentials);
    }

    [Benchmark]
    public async Task AuthenticateNewSession()
    {
        await _client.AuthenticateAsync(_credentials);
    }

    [Benchmark]
    public async Task GetPermissions()
    {
        await _client.GetPermissionsAsync();
    }

    [Benchmark]
    public async Task ValidateSession()
    {
        await _client.ValidateSessionAsync();
    }

    [Benchmark]
    public async Task GetPooledConnection()
    {
        await _connectionPool.GetClientAsync(_connectionId, new IMSConnection());
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add required services for benchmarking
        services.AddHttpClient();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IMSMetrics>();
        services.AddSingleton<IMSRetryPolicy>();
        services.AddSingleton<IMSResponseCache>();
        services.AddSingleton<IIMSClientFactory, IMSClientFactory>();
        services.AddSingleton<IMSConnectionPool>();
    }
} 