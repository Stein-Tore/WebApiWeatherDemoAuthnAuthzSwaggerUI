using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Authorization requirement that the request must originate from the same machine.
/// </summary>
public sealed class SameHostRequirement : IAuthorizationRequirement { }

/// <summary>
/// Validates that requests come from the same machine (loopback, local NIC IPs, or configured additional IPs).
/// Useful for web app + API on same server without requiring bearer tokens.
/// </summary>
public sealed class SameHostAuthorizationHandler : AuthorizationHandler<SameHostRequirement>
{
   private readonly IHttpContextAccessor _httpContextAccessor;
   private readonly ILogger<SameHostAuthorizationHandler> _logger;
   
   // Thread-safe lazy initialization of local IP addresses
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

      // Initialize local IP addresses once at startup
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

      // Check if remote IP is loopback or in local addresses set
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

   /// <summary>
   /// Discovers all local IP addresses (network interfaces + configured additional IPs).
   /// Thread-safe singleton initialization.
   /// </summary>
   private static void EnsureInitialized(SameHostPolicyOptions opts, ILogger logger)
   {
      if (_initialized) return;
      
      lock (_initLock)
      {
         if (_initialized) return;

         try
         {
            var addresses = new HashSet<IPAddress>(IPAddressComparer.Instance);

            // Discover all IP addresses from active network interfaces
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
                     logger.LogInformation("Added local address {Address} from NIC {Name}", addr, ni.Name);
                  }
               }
            }

            // Always include loopback addresses
            addresses.Add(IPAddress.Loopback);
            logger.LogInformation("Added IPAddress.Loopback {Loopback}", IPAddress.Loopback);
            addresses.Add(IPAddress.IPv6Loopback);
            logger.LogInformation("Added IPAddress.IPv6Loopback {IPv6Loopback}", IPAddress.IPv6Loopback);

            // Add configured additional IPs (e.g., for hairpin NAT scenarios)
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
            // Fallback to loopbacks only if enumeration fails
            _localAddresses = new HashSet<IPAddress>(IPAddressComparer.Instance)
            {
               IPAddress.Loopback,
               IPAddress.IPv6Loopback
            };
            logger.LogWarning("Network enumeration failed, using loopback addresses only");
            _initialized = true;
         }
      }
   }

   /// <summary>
   /// Normalizes IPv6-mapped IPv4 addresses to IPv4 for consistent comparison.
   /// </summary>
   private static IPAddress? Normalize(IPAddress? ip)
   {
      if (ip == null) return null;
      if (ip.IsIPv4MappedToIPv6) return ip.MapToIPv4();
      return ip;
   }

   /// <summary>
   /// Custom equality comparer for IPAddress in HashSet.
   /// </summary>
   private sealed class IPAddressComparer : IEqualityComparer<IPAddress>
   {
      public static readonly IPAddressComparer Instance = new();
      public bool Equals(IPAddress? x, IPAddress? y) => x?.Equals(y) ?? false;
      public int GetHashCode(IPAddress obj) => obj.GetHashCode();
   }
}
