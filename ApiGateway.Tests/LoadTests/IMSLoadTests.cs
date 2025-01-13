using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;
using NBomber.Plugins.Network.Ping;
using System.Text.Json;
using Serilog;

public class IMSLoadTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    public static void Run()
    {
        // Initialize logging
        Log.Logger = IMSLoadTestReporting.CreateLogger();

        try
        {
            var httpFactory = ClientFactory.Create(
                name: "IMSLoad",
                clientCount: 100,
                initClient: _ => Task.FromResult(CreateClient())
            );

            // Create Quote Scenario
            var createQuoteStep = Step.Create("create_quote", httpFactory, async context =>
            {
                var request = new QuoteRequest
                {
                    InsuredId = $"INS{Random.Shared.Next(1000, 9999)}",
                    EffectiveDate = DateTime.UtcNow.AddDays(30),
                    CoverageType = "Commercial",
                    Limits = new List<CoverageLimit>
                    {
                        new() { Type = "General", Amount = 1000000 }
                    }
                };

                var response = await context.Client.PostAsJsonAsync("/api/quotes", request);
                
                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: (int)response.StatusCode)
                    : Response.Fail(statusCode: (int)response.StatusCode);
            });

            // Create Insured Scenario
            var createInsuredStep = Step.Create("create_insured", httpFactory, async context =>
            {
                var request = new InsuredRequest
                {
                    FirstName = $"Test{Random.Shared.Next(1000, 9999)}",
                    LastName = $"User{Random.Shared.Next(1000, 9999)}",
                    Email = $"test{Random.Shared.Next(1000, 9999)}@example.com",
                    Phone = "555-555-5555",
                    Address = new Address
                    {
                        Street1 = "123 Main St",
                        City = "Anytown",
                        State = "NY",
                        Zip = "12345"
                    }
                };

                var response = await context.Client.PostAsJsonAsync("/api/insureds", request);
                
                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: (int)response.StatusCode)
                    : Response.Fail(statusCode: (int)response.StatusCode);
            });

            // Complete Policy Flow Scenario
            var completePolicyFlowSteps = Step.Create("complete_policy_flow", httpFactory, async context =>
            {
                // 1. Create Insured
                var insuredRequest = new InsuredRequest
                {
                    FirstName = $"Test{Random.Shared.Next(1000, 9999)}",
                    LastName = $"User{Random.Shared.Next(1000, 9999)}",
                    Email = $"test{Random.Shared.Next(1000, 9999)}@example.com",
                    Phone = "555-555-5555",
                    Address = new Address
                    {
                        Street1 = "123 Main St",
                        City = "Anytown",
                        State = "NY",
                        Zip = "12345"
                    }
                };

                var insuredResponse = await context.Client.PostAsJsonAsync("/api/insureds", insuredRequest);
                if (!insuredResponse.IsSuccessStatusCode)
                    return Response.Fail(statusCode: (int)insuredResponse.StatusCode);

                var insured = await insuredResponse.Content.ReadFromJsonAsync<InsuredResponse>(JsonOptions);

                // 2. Create Quote
                var quoteRequest = new QuoteRequest
                {
                    InsuredId = insured.InsuredId,
                    EffectiveDate = DateTime.UtcNow.AddDays(30),
                    CoverageType = "Commercial",
                    Limits = new List<CoverageLimit>
                    {
                        new() { Type = "General", Amount = 1000000 }
                    }
                };

                var quoteResponse = await context.Client.PostAsJsonAsync("/api/quotes", quoteRequest);
                if (!quoteResponse.IsSuccessStatusCode)
                    return Response.Fail(statusCode: (int)quoteResponse.StatusCode);

                var quote = await quoteResponse.Content.ReadFromJsonAsync<QuoteResponse>(JsonOptions);

                // 3. Create Policy
                var policyRequest = new PolicyRequest
                {
                    QuoteId = quote.QuoteId,
                    EffectiveDate = DateTime.UtcNow.AddDays(30),
                    PaymentPlan = "Monthly",
                    Documents = new List<DocumentInfo>
                    {
                        new() { Id = "DOC1", Type = "Application" }
                    }
                };

                var policyResponse = await context.Client.PostAsJsonAsync("/api/policies", policyRequest);
                
                return policyResponse.IsSuccessStatusCode
                    ? Response.Ok(statusCode: (int)policyResponse.StatusCode)
                    : Response.Fail(statusCode: (int)policyResponse.StatusCode);
            });

            // Define scenarios
            var createQuoteScenario = ScenarioBuilder
                .CreateScenario("create_quote_load", createQuoteStep)
                .WithWarmUpDuration(TimeSpan.FromSeconds(30))
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 50, during: TimeSpan.FromMinutes(5))
                );

            var createInsuredScenario = ScenarioBuilder
                .CreateScenario("create_insured_load", createInsuredStep)
                .WithWarmUpDuration(TimeSpan.FromSeconds(30))
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 30, during: TimeSpan.FromMinutes(5))
                );

            var completePolicyFlowScenario = ScenarioBuilder
                .CreateScenario("complete_policy_flow_load", completePolicyFlowSteps)
                .WithWarmUpDuration(TimeSpan.FromSeconds(30))
                .WithLoadSimulations(
                    Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(5))
                );

            // Run the test
            NBomberRunner
                .RegisterScenarios(
                    createQuoteScenario,
                    createInsuredScenario,
                    completePolicyFlowScenario)
                .WithTestName("IMS API Load Test")
                .WithTestSuite("IMS Integration")
                .WithReportFileName("ims_load_test_report")
                .WithReportFormats(
                    ReportFormat.Txt, 
                    ReportFormat.Csv, 
                    ReportFormat.Html,
                    ReportFormat.Md)
                .WithReportingInterval(TimeSpan.FromSeconds(5))
                .WithPlugins(IMSLoadTestReporting.CreateReportingPlugins())
                .Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Load test failed");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
} 