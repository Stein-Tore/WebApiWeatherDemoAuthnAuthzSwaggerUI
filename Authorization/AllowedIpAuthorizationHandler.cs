using Microsoft.AspNetCore.Authorization;
using System.Net;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Authorization requirement that validates request IP against token's AllowedIPs list.
/// Empty AllowedIPs = no IP restriction.
/// </summary>
public class AllowedIpRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Validates that requests come from IP addresses whitelisted in the token configuration.
/// </summary>
public class AllowedIpAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AllowedIpAuthorizationHandler> logger) : AuthorizationHandler<AllowedIpRequirement>
{
   protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       AllowedIpRequirement requirement)
   {
      var httpContext = httpContextAccessor.HttpContext;
      if (httpContext == null)
      {
         logger.LogWarning("HttpContext is null in AllowedIpAuthorizationHandler");
         return Task.CompletedTask;
      }

      var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
      if (remoteIpAddress == null)
      {
         logger.LogWarning("Remote IP address is null");
         return Task.CompletedTask;
      }

      // Get allowed IPs from claims (populated by authentication handler)
      var allowedIpsClaim = context.User.FindFirst("allowed_ips");

      // No allowed_ips claim means no IP restrictions (empty array in config)
      if (allowedIpsClaim == null || string.IsNullOrWhiteSpace(allowedIpsClaim.Value))
      {
         context.Succeed(requirement);
         return Task.CompletedTask;
      }

      // Check if remote IP matches any allowed IP
      var allowedIps = allowedIpsClaim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
      
      foreach (var allowedIp in allowedIps)
      {
         if (IPAddress.TryParse(allowedIp.Trim(), out var allowedIpAddress))
         {
            if (remoteIpAddress.Equals(allowedIpAddress))
            {
               logger.LogInformation("Access granted from allowed IP: {RemoteIp}", remoteIpAddress);
               context.Succeed(requirement);
               return Task.CompletedTask;
            }
         }
      }

      // IP not in whitelist - deny access
      logger.LogWarning("Access denied from IP: {RemoteIp}. Allowed IPs: {AllowedIps}",
          remoteIpAddress, allowedIpsClaim.Value);

      return Task.CompletedTask;
   }
}
