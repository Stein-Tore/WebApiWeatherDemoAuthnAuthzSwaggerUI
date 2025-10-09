using Microsoft.AspNetCore.Authorization;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Authorization requirement that implements OR logic between IP and Identity authorization.
/// Succeeds if EITHER IP authorization OR identity authorization passes.
/// </summary>
public class IpOrIdentityRequirement : IAuthorizationRequirement
{
   /// <summary>
   /// Name of the IP policy (e.g., "AdminOffice").
   /// </summary>
   public string IpPolicyName { get; }

   /// <summary>
   /// Required roles for identity authorization.
   /// </summary>
   public string[] RequiredRoles { get; }

   public IpOrIdentityRequirement(string ipPolicyName, params string[] requiredRoles)
   {
      IpPolicyName = ipPolicyName;
      RequiredRoles = requiredRoles;
   }
}

/// <summary>
/// Validates that request satisfies EITHER IP authorization OR identity authorization.
/// </summary>
public class IpOrIdentityAuthorizationHandler : AuthorizationHandler<IpOrIdentityRequirement>
{
   private readonly IpAuthorizationHandler _ipHandler;
   private readonly ILogger<IpOrIdentityAuthorizationHandler> _logger;

   public IpOrIdentityAuthorizationHandler(
       IpAuthorizationHandler ipHandler,
       ILogger<IpOrIdentityAuthorizationHandler> logger)
   {
      _ipHandler = ipHandler;
      _logger = logger;
   }

   protected override async Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       IpOrIdentityRequirement requirement)
   {
      // Check 1: IP authorization (delegate to IpAuthorizationHandler)
      var ipRequirement = new IpAuthorizationRequirement(requirement.IpPolicyName);
      var ipContext = new AuthorizationHandlerContext(
          new[] { ipRequirement },
          context.User,
          context.Resource);

      await _ipHandler.HandleAsync(ipContext);

      if (ipContext.HasSucceeded)
      {
         _logger.LogInformation("OR logic: IP authorization succeeded for policy '{PolicyName}'", requirement.IpPolicyName);
         context.Succeed(requirement);
         return;
      }

      // Check 2: Identity authorization (authenticated + roles)
      if (context.User.Identity?.IsAuthenticated == true)
      {
         // Check if user has any of the required roles
         if (requirement.RequiredRoles.Length == 0)
         {
            // No specific roles required, just authenticated
            _logger.LogInformation("OR logic: Identity authorization succeeded (authenticated, no specific role required)");
            context.Succeed(requirement);
            return;
         }

         foreach (var role in requirement.RequiredRoles)
         {
            if (context.User.IsInRole(role))
            {
               _logger.LogInformation("OR logic: Identity authorization succeeded with role '{Role}'", role);
               context.Succeed(requirement);
               return;
            }
         }
      }

      // Neither condition met
      _logger.LogWarning("OR logic: Both IP and identity authorization failed for policy '{PolicyName}'", requirement.IpPolicyName);
   }
}
