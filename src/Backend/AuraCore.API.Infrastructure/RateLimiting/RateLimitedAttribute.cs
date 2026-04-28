using System;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Phase 6.15.3 — endpoint metadata declaring which rate-limit policy applies.
/// Read by RateLimiterMiddleware via Endpoint.Metadata.GetMetadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitedAttribute : Attribute
{
    public string PolicyName { get; }
    public RateLimitedAttribute(string policyName) { PolicyName = policyName; }
}
