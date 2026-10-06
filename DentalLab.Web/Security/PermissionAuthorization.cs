using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DentalLab.Web.Security;

/// <summary>Requirement: the user must have this permission (Owner always passes).</summary>
public sealed class PermissionRequirement(string policy) : IAuthorizationRequirement
{
    public string Policy { get; } = policy;
}

/// <summary>Checks a PermissionRequirement against the live permission switches.</summary>
public sealed class PermissionHandler(PermissionStore store) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        await store.EnsureFreshAsync();

        if (store.IsAllowed(context.User, requirement.Policy))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// Builds "Perm:..." policies on the fly, so pages can use
/// [Authorize(Policy = Perm.CasesCreate)] without registering each policy by hand.
/// Every other policy name falls back to the normal provider.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Perm.Prefix, StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => fallback.GetFallbackPolicyAsync();
}
