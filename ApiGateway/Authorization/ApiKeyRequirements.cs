public class ApiKeyPermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public ApiKeyPermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

public class ApiKeyEnvironmentRequirement : IAuthorizationRequirement
{
    public string Environment { get; }

    public ApiKeyEnvironmentRequirement(string environment)
    {
        Environment = environment;
    }
}

public class ApiKeyAuthorizationHandler : 
    AuthorizationHandler<ApiKeyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyPermissionRequirement requirement)
    {
        var permissions = context.User.Claims
            .Where(c => c.Type == "Permission")
            .Select(c => c.Value);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class ApiKeyEnvironmentHandler : 
    AuthorizationHandler<ApiKeyEnvironmentRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyEnvironmentRequirement requirement)
    {
        var environment = context.User.Claims
            .FirstOrDefault(c => c.Type == "Environment")?.Value;

        if (environment == requirement.Environment)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
} 