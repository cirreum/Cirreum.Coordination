namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering the atomic-coordination backend
/// (<see cref="IReplayGuard"/> + <see cref="IRequestThrottle"/>). Both overloads are idempotent and safe to
/// call from anywhere, any number of times — a consumer that <em>needs</em> coordination calls the
/// parameterless overload to pull a (fail-closed) requirement, and the application calls the configuring
/// overload once to choose the backend. The order of those calls does not matter.
/// </summary>
public static class CoordinationServiceCollectionExtensions {

	/// <summary>
	/// Ensures the coordination machinery is present, registering the fail-closed
	/// <see cref="RequiresBackendReplayGuard"/> / <see cref="RequiresBackendRequestThrottle"/> sentinels if no
	/// backend has been registered yet (idempotent <c>TryAdd</c>). A component that requires coordination calls
	/// this to make the dependency explicit: if the application never chooses a real backend,
	/// <see cref="CoordinationPostureValidator.Validate(IServiceCollection)"/> fails fast at startup rather than
	/// letting the host run mis-configured.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> for chaining.</returns>
	public static IServiceCollection AddCoordination(this IServiceCollection services) {
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IReplayGuard, RequiresBackendReplayGuard>();
		services.TryAddSingleton<IRequestThrottle, RequiresBackendRequestThrottle>();
		return services;
	}

	/// <summary>
	/// Ensures the coordination machinery is present and selects the backend via <paramref name="configure"/>
	/// (e.g. <c>c =&gt; c.UseInMemory()</c> for single-node/development, or <c>c =&gt; c.UseRedis()</c> with the
	/// <c>Cirreum.Coordination.Redis</c> package referenced). The chosen backend <em>replaces</em> the
	/// fail-closed sentinel, so the last call wins regardless of registration order.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Selects the coordination backend.</param>
	/// <returns>The same <paramref name="services"/> for chaining.</returns>
	public static IServiceCollection AddCoordination(this IServiceCollection services, Action<CoordinationBuilder> configure) {
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		services.AddCoordination();
		configure(new CoordinationBuilder(services));
		return services;
	}

}
