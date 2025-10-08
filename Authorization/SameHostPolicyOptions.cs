namespace WebApiWeatherDemoAuthnAutnzSwaggerUI.Authorization;

/// <summary>
/// Configuration options for SameHostPolicy.
/// Allows adding additional IP addresses to treat as "same host" (e.g., for hairpin NAT scenarios).
/// </summary>
public sealed class SameHostPolicyOptions
{
   /// <summary>
   /// Additional IP addresses to treat as same-host.
   /// Use for cloud scenarios where public IP appears due to hairpin NAT (AWS EC2, Azure VM).
   /// </summary>
   public string[] AdditionalSameHostIPs { get; set; } = [];
}
