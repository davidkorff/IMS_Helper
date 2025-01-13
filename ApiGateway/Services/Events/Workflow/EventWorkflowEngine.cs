public interface IEventWorkflowEngine
{
    Task<string> CreateWorkflowAsync(EventWorkflow workflow);
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string workflowId, IntegrationEvent triggerEvent);
    Task<WorkflowStatus> GetWorkflowStatusAsync(string workflowId);
    Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(string workflowId);
}

public class EventWorkflowEngine : IEventWorkflowEngine
{
    private readonly ILogger<EventWorkflowEngine> _logger;
    private readonly IEventStore _eventStore;
    private readonly IWorkflowRegistry _registry;
    private readonly IWorkflowExecutor _executor;
    private readonly IWorkflowStateManager _stateManager;
    private readonly IMetricsCollector _metrics;

    public EventWorkflowEngine(
        ILogger<EventWorkflowEngine> logger,
        IEventStore eventStore,
        IWorkflowRegistry registry,
        IWorkflowExecutor executor,
        IWorkflowStateManager stateManager,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _eventStore = eventStore;
        _registry = registry;
        _executor = executor;
        _stateManager = stateManager;
        _metrics = metrics;
    }

    public async Task<string> CreateWorkflowAsync(EventWorkflow workflow)
    {
        try
        {
            // Validate workflow definition
            await ValidateWorkflowAsync(workflow);

            // Register workflow
            workflow.Id = await _registry.RegisterWorkflowAsync(workflow);

            // Initialize workflow state
            await _stateManager.InitializeWorkflowAsync(workflow);

            _logger.LogInformation(
                "Created workflow {WorkflowId} with {StepCount} steps",
                workflow.Id,
                workflow.Steps.Count);

            return workflow.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            throw;
        }
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        string workflowId, 
        IntegrationEvent triggerEvent)
    {
        var sw = Stopwatch.StartNew();
        var executionId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Starting workflow {WorkflowId} execution {ExecutionId}",
                workflowId,
                executionId);

            var workflow = await _registry.GetWorkflowAsync(workflowId);
            if (workflow == null)
                throw new WorkflowNotFoundException(workflowId);

            // Create execution context
            var context = new WorkflowExecutionContext
            {
                ExecutionId = executionId,
                WorkflowId = workflowId,
                TriggerEvent = triggerEvent,
                Variables = new Dictionary<string, object>(),
                Timestamp = DateTime.UtcNow
            };

            // Execute workflow
            var result = await _executor.ExecuteAsync(workflow, context);

            // Record metrics
            RecordExecutionMetrics(workflow, result, sw.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error executing workflow {WorkflowId}", workflowId);
            
            return new WorkflowExecutionResult
            {
                ExecutionId = executionId,
                WorkflowId = workflowId,
                Status = WorkflowExecutionStatus.Failed,
                Error = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    public async Task<WorkflowStatus> GetWorkflowStatusAsync(string workflowId)
    {
        return await _stateManager.GetWorkflowStatusAsync(workflowId);
    }

    public async Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(
        string workflowId)
    {
        return await _stateManager.GetWorkflowExecutionsAsync(workflowId);
    }

    private async Task ValidateWorkflowAsync(EventWorkflow workflow)
    {
        // Validate basic properties
        if (string.IsNullOrEmpty(workflow.Name))
            throw new WorkflowValidationException("Workflow name is required");

        if (!workflow.Steps.Any())
            throw new WorkflowValidationException("Workflow must have at least one step");

        // Validate step connections
        var stepIds = workflow.Steps.Select(s => s.Id).ToHashSet();
        foreach (var step in workflow.Steps)
        {
            foreach (var nextStepId in step.NextSteps)
            {
                if (!stepIds.Contains(nextStepId))
                {
                    throw new WorkflowValidationException(
                        $"Step {step.Id} references non-existent next step {nextStepId}");
                }
            }
        }

        // Validate step configurations
        foreach (var step in workflow.Steps)
        {
            await ValidateStepConfigurationAsync(step);
        }

        // Check for cycles
        if (HasCycles(workflow))
        {
            throw new WorkflowValidationException(
                "Workflow contains cycles which are not allowed");
        }
    }

    private async Task ValidateStepConfigurationAsync(WorkflowStep step)
    {
        switch (step.Type)
        {
            case WorkflowStepType.EventTransformation:
                ValidateTransformationStep(step);
                break;
            case WorkflowStepType.Condition:
                ValidateConditionStep(step);
                break;
            case WorkflowStepType.Action:
                await ValidateActionStepAsync(step);
                break;
            default:
                throw new WorkflowValidationException(
                    $"Unknown step type: {step.Type}");
        }
    }

    private bool HasCycles(EventWorkflow workflow)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var step in workflow.Steps)
        {
            if (IsCyclicUtil(workflow, step.Id, visited, recursionStack))
                return true;
        }

        return false;
    }

    private bool IsCyclicUtil(
        EventWorkflow workflow,
        string stepId,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(stepId))
            return true;

        if (visited.Contains(stepId))
            return false;

        visited.Add(stepId);
        recursionStack.Add(stepId);

        var step = workflow.Steps.First(s => s.Id == stepId);
        foreach (var nextStepId in step.NextSteps)
        {
            if (IsCyclicUtil(workflow, nextStepId, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(stepId);
        return false;
    }

    private void RecordExecutionMetrics(
        EventWorkflow workflow,
        WorkflowExecutionResult result,
        TimeSpan duration)
    {
        var tags = new Dictionary<string, string>
        {
            ["workflow_id"] = workflow.Id,
            ["status"] = result.Status.ToString(),
            ["trigger_type"] = workflow.TriggerType.ToString()
        };

        _metrics.Increment("workflow_executions", 1, tags);
        _metrics.RecordHistogram(
            "workflow_execution_duration",
            duration.TotalMilliseconds,
            tags);

        if (result.Status == WorkflowExecutionStatus.Failed)
        {
            _metrics.Increment("workflow_failures", 1, tags);
        }
    }
}

public class WorkflowExecutionContext
{
    public string ExecutionId { get; set; }
    public string WorkflowId { get; set; }
    public IntegrationEvent TriggerEvent { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorkflowExecutionResult
{
    public string ExecutionId { get; set; }
    public string WorkflowId { get; set; }
    public WorkflowExecutionStatus Status { get; set; }
    public string Error { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> OutputVariables { get; set; }
    public List<WorkflowStepResult> StepResults { get; set; }
}

public enum WorkflowExecutionStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public class WorkflowValidationException : Exception
{
    public WorkflowValidationException(string message) : base(message) { }
}

public class WorkflowNotFoundException : Exception
{
    public WorkflowNotFoundException(string workflowId) 
        : base($"Workflow {workflowId} not found") { }
} 