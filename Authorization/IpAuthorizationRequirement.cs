using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;

namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Authorization requirement for IP-based access control.
/// Can include same-host IPs (auto-populated) and/or specific IPs from configuration.
/// Works independently of authentication - no token required.
/// </summary>
public class IpAuthorizationRequirement : IAuthorizationRequirement
{
   /// <summary>
   /// Name of the IP policy configuration (e.g., "OpenApi", "Partner1").
   /// </summary>
   public string PolicyName { get; }

   public IpAuthorizationRequirement(string policyName)
   {
      PolicyName = policyName;
   }
}

/// <summary>
/// Configuration for individual IP policy.
/// </summary>
public class IpPolicyConfiguration
{
   /// <summary>
   /// Include same-host IPs (loopback + local NICs + additional configured IPs).
   /// </summary>
   public bool IncludeSameHost { get; set; }

   /// <summary>
   /// Additional specific IPs to allow.
   /// </summary>
   public string[] AllowedIPs { get; set; } = [];
}

/// <summary>
/// Configuration for all IP policies.
/// </summary>
public class IpAuthorizationOptions
{
   /// <summary>
   /// Additional same-host IPs for cloud/NAT scenarios (e.g., AWS EIP).
   /// Applied globally when IncludeSameHost is true.
   /// </summary>
   public string[] AdditionalSameHostIPs { get; set; } = [];

   /// <summary>
   /// Dictionary of policy name -> IP policy configuration.
   /// </summary>
   public Dictionary<string, IpPolicyConfiguration> Policies { get; set; } = new();
}

/// <summary>
/// Validates that requests come from allowed IP addresses.
/// Supports same-host detection and/or specific IP whitelisting.
/// </summary>
public class IpAuthorizationHandler : AuthorizationHandler<IpAuthorizationRequirement>
{
   private readonly IHttpContextAccessor _httpContextAccessor;
   private readonly ILogger<IpAuthorizationHandler> _logger;
   private readonly IpAuthorizationOptions _options;

   // Thread-safe lazy initialization of same-host IP addresses
   private static readonly object _initLock = new();
   private static volatile bool _initialized;
   private static HashSet<IPAddress> _sameHostAddresses = new(IPAddressComparer.Instance);

   public IpAuthorizationHandler(
       IHttpContextAccessor httpContextAccessor,
       ILogger<IpAuthorizationHandler> logger,
       IOptions<IpAuthorizationOptions> options)
   {
      _httpContextAccessor = httpContextAccessor;
      _logger = logger;
      _options = options.Value;

      // Initialize same-host IP addresses once at startup
      EnsureInitialized(_options, _logger);
   }

   protected override Task HandleRequirementAsync(
       AuthorizationHandlerContext context,
       IpAuthorizationRequirement requirement)
   {
      _logger.LogInformation("=== IP Authorization Handler Started for policy '{PolicyName}' ===", requirement.PolicyName);
      
      var httpContext = _httpContextAccessor.HttpContext;
      if (httpContext == null)
      {
         _logger.LogWarning("HttpContext is null in IpAuthorizationHandler - authorization denied");
         return Task.CompletedTask;
      }

      _logger.LogInformation("Request path: {Path}, Method: {Method}", 
         httpContext.Request.Path, httpContext.Request.Method);

      var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
      if (remoteIpAddress == null)
      {
         _logger.LogWarning("Remote IP address is null - authorization denied");
         return Task.CompletedTask;
      }

      _logger.LogInformation("Remote IP address (raw): {RemoteIp}, Address family: {AddressFamily}", 
         remoteIpAddress, remoteIpAddress.AddressFamily);

      var remote = Normalize(remoteIpAddress);
      if (remote == null)
      {
         _logger.LogWarning("Unable to normalize remote IP address {RemoteIp} - authorization denied", remoteIpAddress);
         return Task.CompletedTask;
      }

      _logger.LogInformation("Remote IP address (normalized): {NormalizedRemoteIp}", remote);

      // Get policy configuration
      if (!_options.Policies.TryGetValue(requirement.PolicyName, out var policyConfig))
      {
         _logger.LogWarning("IP policy '{PolicyName}' not found in configuration - authorization denied. Available policies: {Policies}", 
            requirement.PolicyName, string.Join(", ", _options.Policies.Keys));
         return Task.CompletedTask;
      }

      _logger.LogInformation("Policy '{PolicyName}' configuration - IncludeSameHost: {IncludeSameHost}, ConfiguredIPs: [{AllowedIPs}]",
         requirement.PolicyName, policyConfig.IncludeSameHost, string.Join(", ", policyConfig.AllowedIPs));

      var allowedIps = new HashSet<IPAddress>(IPAddressComparer.Instance);

      // Add same-host IPs if configured
      if (policyConfig.IncludeSameHost)
      {
         _logger.LogInformation("Including same-host IPs for policy '{PolicyName}'. Total same-host IPs: {Count}", 
            requirement.PolicyName, _sameHostAddresses.Count);
         
         foreach (var ip in _sameHostAddresses)
         {
            allowedIps.Add(ip);
            _logger.LogDebug("Added same-host IP: {Ip}", ip);
         }
      }
      else
      {
         _logger.LogInformation("Same-host IPs NOT included for policy '{PolicyName}'", requirement.PolicyName);
      }

      // Add specific IPs from configuration
      _logger.LogInformation("Adding {Count} specific IPs from configuration", policyConfig.AllowedIPs.Length);
      foreach (var ipString in policyConfig.AllowedIPs)
      {
         if (IPAddress.TryParse(ipString.Trim(), out var ip))
         {
            var normalized = Normalize(ip)!;
            allowedIps.Add(normalized);
            _logger.LogInformation("Added configured IP: {IpString} (normalized: {Normalized})", ipString, normalized);
         }
         else
         {
            _logger.LogWarning("Failed to parse configured IP: '{IpString}'", ipString);
         }
      }

      _logger.LogInformation("Total allowed IPs for policy '{PolicyName}': {Count}. List: [{IpList}]",
         requirement.PolicyName, allowedIps.Count, string.Join(", ", allowedIps));

      // Check if remote IP is in allowed set
      _logger.LogInformation("Checking if remote IP {RemoteIp} is in allowed set...", remote);
      
      if (allowedIps.Contains(remote))
      {
         _logger.LogInformation("? IP authorization GRANTED for policy '{PolicyName}' from {RemoteIp}", 
            requirement.PolicyName, remote);
         context.Succeed(requirement);
         return Task.CompletedTask;
      }

      // IP not allowed
      _logger.LogWarning("? IP authorization DENIED for policy '{PolicyName}' from {RemoteIp}. Remote IP not found in allowed list. Policy includes same-host: {IncludeSameHost}, specific IPs: [{AllowedIPs}], total allowed IPs: {Count}",
          requirement.PolicyName, remote, policyConfig.IncludeSameHost, string.Join(", ", policyConfig.AllowedIPs), allowedIps.Count);

      _logger.LogInformation("=== IP Authorization Handler Completed (DENIED) ===");
      
      return Task.CompletedTask;
   }

   /// <summary>
   /// Discovers all same-host IP addresses (loopback + NICs + configured additional IPs).
   /// Thread-safe singleton initialization.
   /// </summary>
   private static void EnsureInitialized(IpAuthorizationOptions opts, ILogger logger)
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
                     logger.LogInformation("Added same-host IP {Address} from NIC {Name}", addr, ni.Name);
                  }
               }
            }

            // Always include loopback addresses
            addresses.Add(IPAddress.Loopback);
            logger.LogInformation("Added loopback IP {Loopback}", IPAddress.Loopback);
            addresses.Add(IPAddress.IPv6Loopback);
            logger.LogInformation("Added IPv6 loopback {IPv6Loopback}", IPAddress.IPv6Loopback);

            // Add configured additional same-host IPs (e.g., for hairpin NAT scenarios)
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

            _sameHostAddresses = addresses;
            _initialized = true;
         }
         catch (Exception ex)
         {
            // Fallback to loopbacks only if enumeration fails
            _sameHostAddresses = new HashSet<IPAddress>(IPAddressComparer.Instance)
            {
               IPAddress.Loopback,
               IPAddress.IPv6Loopback
            };
            logger.LogWarning(ex, "Network enumeration failed, using loopback addresses only");
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
