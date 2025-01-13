using BenchmarkDotNet.Running;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--load-test")
        {
            IMSLoadTests.Run();
        }
        else
        {
            var summary = BenchmarkRunner.Run<IMSValidationPerformanceTests>();
        }
    }
} 