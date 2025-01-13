public class IMSConfiguration
{
    public const string Section = "IMS";

    public string[] AllowedEnvironments { get; set; } = 
        { "Production", "Test", "Development" };
    
    public int MaxPoolSize { get; set; } = 100;
    public int ConnectionTimeoutMinutes { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    
    public TimeSpan HealthCheckInterval { get; set; } = 
        TimeSpan.FromMinutes(5);
    
    public class ValidationRules : AbstractValidator<IMSConfiguration>
    {
        public ValidationRules()
        {
            RuleFor(x => x.MaxPoolSize)
                .GreaterThan(0)
                .LessThanOrEqualTo(1000)
                .WithMessage("MaxPoolSize must be between 1 and 1000");

            RuleFor(x => x.ConnectionTimeoutMinutes)
                .GreaterThan(0)
                .LessThanOrEqualTo(60)
                .WithMessage("ConnectionTimeoutMinutes must be between 1 and 60");

            RuleFor(x => x.RetryCount)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(10)
                .WithMessage("RetryCount must be between 0 and 10");

            RuleFor(x => x.CircuitBreakerThreshold)
                .GreaterThan(0)
                .LessThanOrEqualTo(100)
                .WithMessage("CircuitBreakerThreshold must be between 1 and 100");

            RuleFor(x => x.CircuitBreakerDurationSeconds)
                .GreaterThan(0)
                .LessThanOrEqualTo(300)
                .WithMessage("CircuitBreakerDurationSeconds must be between 1 and 300");

            RuleFor(x => x.HealthCheckInterval)
                .Must(x => x >= TimeSpan.FromSeconds(30) && x <= TimeSpan.FromMinutes(30))
                .WithMessage("HealthCheckInterval must be between 30 seconds and 30 minutes");
        }
    }
} 