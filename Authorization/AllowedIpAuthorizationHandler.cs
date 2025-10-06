using Microsoft.AspNetCore.Authorization;
using System.Net;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Requirement that the request must originate from an IP address in the token's AllowedIPs list.
/// If AllowedIPs is empty, no IP restriction is enforced.
/// </summary>
public class AllowedIpRequirement : IAuthorizationRequirement
{
}

public class AllowedIpAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AllowedIpAuthorizationHandler> logger) : AuthorizationHandler<AllowedIpRequirement>
{
   private readonly ILogger<AllowedIpAuthorizationHandler> _logger = logger;

   protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       AllowedIpRequirement requirement)
   {
      var httpContext = httpContextAccessor.HttpContext;

      if (httpContext == null)
      {
         _logger.LogWarning("HttpContext is null in AllowedIpAuthorizationHandler");
         return Task.CompletedTask;
      }

      // Get the remote IP address
      var remoteIpAddress = httpContext.Connection.RemoteIpAddress;

      if (remoteIpAddress == null)
      {
         _logger.LogWarning("Remote IP address is null");
         return Task.CompletedTask;
      }

      // Get allowed IPs from claims (set by authentication handler)
      var allowedIpsClaim = context.User.FindFirst("allowed_ips");

      // If no allowed_ips claim, no IP restriction (empty array in config)
      if (allowedIpsClaim == null || string.IsNullOrWhiteSpace(allowedIpsClaim.Value))
      {
         context.Succeed(requirement);
         return Task.CompletedTask;
      }

      // Parse allowed IPs
      var allowedIps = allowedIpsClaim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries);

      // Check if remote IP is in the allowed list
      foreach (var allowedIp in allowedIps)
      {
         if (IPAddress.TryParse(allowedIp.Trim(), out var allowedIpAddress))
         {
            if (remoteIpAddress.Equals(allowedIpAddress))
            {
               _logger.LogInformation("Access granted from allowed IP: {RemoteIp}", remoteIpAddress);
               context.Succeed(requirement);
               return Task.CompletedTask;
            }
         }
      }

      // IP not in whitelist
      _logger.LogWarning("Access denied from IP: {RemoteIp}. Allowed IPs: {AllowedIps}",
          remoteIpAddress, allowedIpsClaim.Value);

      return Task.CompletedTask;
   }
}
