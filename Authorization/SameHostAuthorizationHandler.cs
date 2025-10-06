using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

// Requirement that the request must originate from the same host (this machine).
public sealed class SameHostRequirement : IAuthorizationRequirement { }

public sealed class SameHostAuthorizationHandler : AuthorizationHandler<SameHostRequirement>
{
   private readonly IHttpContextAccessor _httpContextAccessor;
   private readonly ILogger<SameHostAuthorizationHandler> _logger;
   private static readonly object _initLock = new();
   private static volatile bool _initialized;
   private static HashSet<IPAddress> _localAddresses = new(IPAddressComparer.Instance);

   public SameHostAuthorizationHandler(
       IHttpContextAccessor httpContextAccessor,
       ILogger<SameHostAuthorizationHandler> logger,
       IOptions<SameHostPolicyOptions> options)
   {
      _httpContextAccessor = httpContextAccessor;
      _logger = logger;

      EnsureInitialized(options.Value, _logger);
   }

   protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       SameHostRequirement requirement)
   {
      var httpContext = _httpContextAccessor.HttpContext;
      if (httpContext == null)
      {
         return Task.CompletedTask;
      }

      var remote = Normalize(httpContext.Connection.RemoteIpAddress);
      if (remote == null)
      {
         _logger.LogDebug("RemoteIpAddress is null");
         return Task.CompletedTask;
      }

      if (IPAddress.IsLoopback(remote) || _localAddresses.Contains(remote))
      {
         _logger.LogDebug("Same-host access granted from {RemoteIp}", remote);
         context.Succeed(requirement);
      }
      else
      {
         _logger.LogInformation("Same-host access denied from {RemoteIp}", remote);
      }

      return Task.CompletedTask;
   }

   private static void EnsureInitialized(SameHostPolicyOptions opts, ILogger logger)
   {
      if (_initialized) return;
      lock (_initLock)
      {
         if (_initialized) return;

         try
         {
            var addresses = new HashSet<IPAddress>(IPAddressComparer.Instance);

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
               if (ni.OperationalStatus != OperationalStatus.Up) continue;
               var ipProps = ni.GetIPProperties();
               foreach (var ua in ipProps.UnicastAddresses)
               {
                  var addr = Normalize(ua.Address);
                  if (addr != null)
                  {
                     addresses.Add(addr);
                     logger.LogInformation("Added local address {Address} from network interface {Name}", addr, ni.Name);
                     _ = 0; // no-op to keep block non-empty for tool parsing
                  }
               }
            }

            // Always include loopbacks
            addresses.Add(IPAddress.Loopback);
            logger.LogInformation("Added IPAddress.Loopback {Loopback}", IPAddress.Loopback);
            addresses.Add(IPAddress.IPv6Loopback);
            logger.LogInformation("Added IPAddress.IPv6Loopback {IPv6Loopback}", IPAddress.IPv6Loopback);

            // Include configured additional same-host IPs (e.g., hairpin NAT / EIP)
            if (opts.AdditionalSameHostIPs is { Length: > 0 })
            {
               foreach (var ipText in opts.AdditionalSameHostIPs)
               {
                  if (IPAddress.TryParse(ipText, out var ip))
                  {
                     var norm = Normalize(ip)!;
                     addresses.Add(norm);
                     logger.LogInformation("Added configured same-host IP {Address}", norm);
                  }
               }
            }

            _localAddresses = addresses;
            _initialized = true;
         }
         catch
         {
            // If enumeration fails, fall back to loopbacks only
            _localAddresses = new HashSet<IPAddress>(IPAddressComparer.Instance)
            {
               IPAddress.Loopback,
               IPAddress.IPv6Loopback
            };
            logger.LogWarning("Enumeration failed, fall back to loopbacks only. IPAddress.Loopback {Loopback} and IPAddress.IPv6Loopback {IPv6Loopback}", IPAddress.Loopback, IPAddress.IPv6Loopback);
            _initialized = true;
         }
      }
   }

   private static IPAddress? Normalize(IPAddress? ip)
   {
      if (ip == null) return null;
      if (ip.IsIPv4MappedToIPv6) return ip.MapToIPv4();
      return ip;
   }

   private sealed class IPAddressComparer : IEqualityComparer<IPAddress>
   {
      public static readonly IPAddressComparer Instance = new();
      public bool Equals(IPAddress? x, IPAddress? y) => x?.Equals(y) ?? false;
      public int GetHashCode(IPAddress obj) => obj.GetHashCode();
   }
}
