public interface IRuleExpressionEvaluator
{
    Task<bool> Evaluate(string expression, Dictionary<string, object> data);
    Task<bool> ValidateExpression(string expression);
    Task<List<string>> GetRequiredFields(string expression);
    Task<ExpressionMetadata> GetExpressionMetadata(string expression);
}

public class ExpressionMetadata
{
    public List<string> RequiredFields { get; set; }
    public List<string> OptionalFields { get; set; }
    public List<string> Functions { get; set; }
    public List<string> Dependencies { get; set; }
    public Dictionary<string, string> FieldTypes { get; set; }
} 