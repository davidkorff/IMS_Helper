using Microsoft.Extensions.Configuration;
using System;
using System.IO;

public class IMSTestConfiguration
{
    public string BaseUrl { get; set; }
    public string ProgramCode { get; set; }
    public string ClientId { get; set; }
    public string TestEmail { get; set; }
    public string TestPassword { get; set; }
    public string TestProducerGuid { get; set; }
    public RetryPolicy RetryPolicy { get; set; }

    public static IMSTestConfiguration Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return new IMSTestConfiguration
        {
            BaseUrl = config["IMS:BaseUrl"] ?? "https://webservices.mgasystems.com/ims_demo/",
            ProgramCode = config["IMS:ProgramCode"] ?? "TFLOD",
            ClientId = config.GetRequiredValue("IMS_CLIENT_ID"),
            TestEmail = config.GetRequiredValue("IMS_TEST_EMAIL"),
            TestPassword = config.GetRequiredValue("IMS_TEST_PASSWORD"),
            TestProducerGuid = config.GetRequiredValue("IMS_TEST_PRODUCER_GUID"),
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = int.Parse(config["IMS:RetryPolicy:MaxRetries"] ?? "3"),
                DelayMilliseconds = int.Parse(config["IMS:RetryPolicy:DelayMilliseconds"] ?? "1000"),
                ExponentialBackoff = bool.Parse(config["IMS:RetryPolicy:ExponentialBackoff"] ?? "true")
            }
        };
    }
}

public static class ConfigurationExtensions
{
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var value = configuration[key] ?? 
            Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Required configuration value '{key}' was not found");
        }

        return value;
    }
} 