using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Polly;
using System;
using System.Threading.Tasks;

public class IMSRetryPolicy
{
    private readonly ILogger<IMSRetryPolicy> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly AsyncPolicyWrap _resilientPolicy;

    public IMSRetryPolicy(ILogger<IMSRetryPolicy> logger, IConfiguration configuration)
    {
        _logger = logger;

        _retryPolicy = Policy
            .Handle<IMSException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: configuration.GetValue<int>("IMS:RetryCount", 3),
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "Retry {RetryCount} after {Delay}ms for {OperationKey}",
                        retryCount, timeSpan.TotalMilliseconds, context.OperationKey);
                });

        _circuitBreaker = Policy
            .Handle<IMSException>()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: configuration.GetValue<int>("IMS:CircuitBreakerThreshold", 5),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue<int>("IMS:CircuitBreakerDurationSeconds", 30)),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(exception, 
                        "Circuit breaker opened for {Duration}s", 
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                });

        _resilientPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreaker);
    }

    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operationKey)
    {
        return _resilientPolicy.ExecuteAsync(async (context) =>
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Operation {OperationKey} failed", operationKey);
                throw;
            }
        }, new Context(operationKey));
    }
} 