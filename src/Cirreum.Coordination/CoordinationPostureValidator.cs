namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Boot-time validator that fails fast when a component pulled coordination (via
/// <see cref="CoordinationServiceCollectionExtensions.AddCoordination(IServiceCollection)"/>) but the
/// application never chose a backend — i.e. a fail-closed sentinel is still registered. Host composers call
/// <see cref="Validate(IServiceCollection)"/> once after all registrations have run (e.g. the authentication
/// umbrella invokes it at the end of its composition), turning a silent mis-configuration into a clear
/// startup error instead of a runtime failure on the first coordinated request.
/// </summary>
public static class CoordinationPostureValidator {

	/// <summary>
	/// Throws <see cref="InvalidOperationException"/> if either coordination primitive is still backed by its
	/// fail-closed sentinel (coordination was required but no backend was chosen). Order-independent: call it
	/// once after the composition callback has run, so it sees every <c>AddCoordination</c> contribution.
	/// </summary>
	/// <param name="services">The fully-composed service collection.</param>
	public static void Validate(IServiceCollection services) {
		ArgumentNullException.ThrowIfNull(services);

		var unconfigured =
			services.Any(d => d.ImplementationType == typeof(RequiresBackendReplayGuard)) ||
			services.Any(d => d.ImplementationType == typeof(RequiresBackendRequestThrottle));

		if (unconfigured) {
			throw NoBackendError();
		}
	}

	/// <summary>The shared "no backend chosen" error, used by both the validator and the sentinels.</summary>
	internal static InvalidOperationException NoBackendError() =>
		new("A coordination-requiring posture is active (e.g. SignedRequest strict-nonce, or ApiKey " +
			"SelfContained), but no coordination backend was chosen. Call " +
			"services.AddCoordination(c => c.UseInMemory()) for single-node / development, or " +
			"c => c.UseRedis() (reference Cirreum.Coordination.Redis) for distributed, multi-instance deployments.");

}
