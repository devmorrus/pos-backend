using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;

namespace MorrusPOS.Api.Security;

public static class CapabilityPolicy
{
    public const string Prefix = "capability:";
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequiresCapabilityAttribute : AuthorizeAttribute
{
    public RequiresCapabilityAttribute(string capabilityCode)
        : base(CapabilityPolicy.Prefix + capabilityCode) { }
}

public sealed class CapabilityRequirement(string capabilityCode) : IAuthorizationRequirement
{
    public string CapabilityCode { get; } = capabilityCode;
}

public sealed class CapabilityAuthorizationHandler
    : AuthorizationHandler<CapabilityRequirement>
{
    private static readonly string[] OutletKeys =
        ["outletId", "OutletId", "fromOutletId", "FromOutletId"];

    private readonly ICapabilityEvaluator _evaluator;
    private readonly ICurrentUserService _currentUser;

    public CapabilityAuthorizationHandler(
        ICapabilityEvaluator evaluator,
        ICurrentUserService currentUser)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CapabilityRequirement requirement)
    {
        if (!_currentUser.IsAuthenticated)
            return;

        var outletId = _currentUser.OutletId ?? ResolveOutlet(context.Resource as HttpContext);
        if (await _evaluator.IsEnabledAsync(requirement.CapabilityCode, outletId))
            context.Succeed(requirement);
    }

    private static Guid? ResolveOutlet(HttpContext? httpContext)
    {
        if (httpContext is null) return null;
        foreach (var key in OutletKeys)
        {
            if (httpContext.Request.RouteValues.TryGetValue(key, out var routeValue) &&
                Guid.TryParse(routeValue?.ToString(), out var routeId))
                return routeId;
            if (httpContext.Request.Query.TryGetValue(key, out var queryValue) &&
                Guid.TryParse(queryValue.FirstOrDefault(), out var queryId))
                return queryId;
        }
        return null;
    }
}

public sealed class CapabilityAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var requirement = authorizeResult.AuthorizationFailure?.FailedRequirements
            .OfType<CapabilityRequirement>()
            .FirstOrDefault();
        if (authorizeResult.Forbidden && requirement is not null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "CAPABILITY_DISABLED",
                capability = requirement.CapabilityCode,
                message = "Capability tidak aktif untuk Business atau Outlet ini.",
            }));
            return;
        }

        await _fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}

/// <summary>
/// Authorization runs before model binding, so Owner requests with OutletId in
/// the body only receive the Business-level check there. This post-binding
/// filter enforces every outlet id carried by the action arguments.
/// </summary>
public sealed class CapabilityOutletActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> OutletPropertyNames =
        ["OutletId", "FromOutletId", "ToOutletId"];
    private readonly ICapabilityEvaluator _evaluator;
    private readonly ICurrentUserService _currentUser;

    public CapabilityOutletActionFilter(
        ICapabilityEvaluator evaluator,
        ICurrentUserService currentUser)
    {
        _evaluator = evaluator;
        _currentUser = currentUser;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var requirements = context.ActionDescriptor.EndpointMetadata
            .OfType<RequiresCapabilityAttribute>()
            .Select(x => x.Policy![CapabilityPolicy.Prefix.Length..])
            .Distinct()
            .ToArray();
        if (requirements.Length == 0)
        {
            await next();
            return;
        }

        var outletIds = new HashSet<Guid>();
        if (_currentUser.OutletId.HasValue) outletIds.Add(_currentUser.OutletId.Value);
        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is Guid direct && OutletPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                outletIds.Add(direct);
            if (value is null) continue;
            foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!OutletPropertyNames.Contains(property.Name) || property.GetValue(value) is not Guid id || id == Guid.Empty)
                    continue;
                outletIds.Add(id);
            }
        }

        foreach (var requirement in requirements)
        foreach (var outletId in outletIds)
            await _evaluator.EnsureEnabledAsync(requirement, outletId, context.HttpContext.RequestAborted);

        await next();
    }
}
