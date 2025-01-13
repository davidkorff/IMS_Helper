using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class DefaultRuleExpressionEvaluator : IRuleExpressionEvaluator
{
    private readonly ILogger<DefaultRuleExpressionEvaluator> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMSUnderwritingSettings _settings;
    private readonly DataTable _dataTable;

    public DefaultRuleExpressionEvaluator(
        ILogger<DefaultRuleExpressionEvaluator> logger,
        IMemoryCache cache,
        IOptions<IMSUnderwritingSettings> settings)
    {
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
        _dataTable = new DataTable();
    }

    public async Task<bool> Evaluate(string expression, Dictionary<string, object> data)
    {
        try
        {
            var sanitizedExpression = await PrepareExpression(expression, data);
            
            // Use DataTable.Compute for safe expression evaluation
            var result = _dataTable.Compute(sanitizedExpression, string.Empty);
            return Convert.ToBoolean(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate expression: {Expression}", expression);
            throw new RuleEvaluationException("Expression evaluation failed", ex);
        }
    }

    public async Task<bool> ValidateExpression(string expression)
    {
        try
        {
            // Basic syntax validation
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            // Check for balanced parentheses
            if (!HasBalancedParentheses(expression))
            {
                return false;
            }

            // Validate operators
            if (!HasValidOperators(expression))
            {
                return false;
            }

            // Try evaluating with sample data
            var sampleData = CreateSampleData(await GetRequiredFields(expression));
            await Evaluate(expression, sampleData);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<string>> GetRequiredFields(string expression)
    {
        var fields = new HashSet<string>();
        var regex = new Regex(@"\[([^\]]+)\]");
        var matches = regex.Matches(expression);

        foreach (Match match in matches)
        {
            fields.Add(match.Groups[1].Value);
        }

        return fields.ToList();
    }

    public async Task<ExpressionMetadata> GetExpressionMetadata(string expression)
    {
        var metadata = new ExpressionMetadata
        {
            RequiredFields = await GetRequiredFields(expression),
            Functions = GetUsedFunctions(expression),
            FieldTypes = await InferFieldTypes(expression),
            Dependencies = await AnalyzeDependencies(expression)
        };

        metadata.OptionalFields = metadata.RequiredFields
            .Where(f => IsFieldOptional(expression, f))
            .ToList();

        return metadata;
    }

    private async Task<string> PrepareExpression(
        string expression, 
        Dictionary<string, object> data)
    {
        // Replace field references with actual values
        foreach (var kvp in data)
        {
            var placeholder = $"[{kvp.Key}]";
            var value = FormatValue(kvp.Value);
            expression = expression.Replace(placeholder, value);
        }

        // Handle any default values for missing fields
        var missingFields = await GetRequiredFields(expression);
        foreach (var field in missingFields)
        {
            if (_settings.RuleEvaluation.DefaultValues.TryGetValue(field, out var defaultValue))
            {
                expression = expression.Replace($"[{field}]", defaultValue);
            }
        }

        return expression;
    }

    private string FormatValue(object value)
    {
        if (value == null) return "null";

        switch (value)
        {
            case string s:
                return $"'{s}'";
            case DateTime dt:
                return $"'{dt:yyyy-MM-dd}'";
            case bool b:
                return b.ToString().ToLower();
            default:
                return value.ToString();
        }
    }

    private bool HasBalancedParentheses(string expression)
    {
        var stack = new Stack<char>();
        foreach (char c in expression)
        {
            if (c == '(')
            {
                stack.Push(c);
            }
            else if (c == ')')
            {
                if (stack.Count == 0) return false;
                stack.Pop();
            }
        }
        return stack.Count == 0;
    }

    private bool HasValidOperators(string expression)
    {
        var validOperators = new[] { "AND", "OR", "NOT", "<", ">", "=", "<=", ">=", "<>", "+" };
        var regex = new Regex(@"\b\w+\b");
        var words = regex.Matches(expression)
            .Select(m => m.Value)
            .Where(w => !decimal.TryParse(w, out _));

        return words.All(w => !w.All(char.IsUpper) || validOperators.Contains(w));
    }

    private Dictionary<string, object> CreateSampleData(List<string> fields)
    {
        return fields.ToDictionary(
            f => f,
            f => DetermineSampleValue(f));
    }

    private object DetermineSampleValue(string fieldName)
    {
        if (fieldName.Contains("DATE", StringComparison.OrdinalIgnoreCase))
            return DateTime.Today;
        if (fieldName.Contains("AMOUNT", StringComparison.OrdinalIgnoreCase))
            return 1000m;
        if (fieldName.Contains("COUNT", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (fieldName.Contains("IS", StringComparison.OrdinalIgnoreCase))
            return true;
        return "SAMPLE";
    }

    private List<string> GetUsedFunctions(string expression)
    {
        var functions = new List<string>();
        var regex = new Regex(@"\b([A-Z]+)\s*\(");
        var matches = regex.Matches(expression);

        foreach (Match match in matches)
        {
            functions.Add(match.Groups[1].Value);
        }

        return functions.Distinct().ToList();
    }

    private async Task<Dictionary<string, string>> InferFieldTypes(string expression)
    {
        var types = new Dictionary<string, string>();
        var fields = await GetRequiredFields(expression);

        foreach (var field in fields)
        {
            types[field] = InferType(expression, field);
        }

        return types;
    }

    private string InferType(string expression, string field)
    {
        if (expression.Contains($"[{field}] = '{{'"))
            return "DateTime";
        if (expression.Contains($"[{field}] IN ("))
            return "Enum";
        if (expression.Contains($"[{field}] = TRUE") || 
            expression.Contains($"[{field}] = FALSE"))
            return "Boolean";
        if (expression.Contains($"[{field}] > ") || 
            expression.Contains($"[{field}] < "))
            return "Numeric";
        return "String";
    }

    private async Task<List<string>> AnalyzeDependencies(string expression)
    {
        var dependencies = new List<string>();
        var fields = await GetRequiredFields(expression);

        foreach (var field in fields)
        {
            if (expression.Contains($"DEPENDS_ON([{field}])"))
            {
                dependencies.Add(field);
            }
        }

        return dependencies;
    }

    private bool IsFieldOptional(string expression, string field)
    {
        return expression.Contains($"ISNULL([{field}])") || 
               expression.Contains($"COALESCE([{field}]");
    }
} 