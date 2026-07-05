namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering the coordination backend
/// (<see cref="IReplayGuard"/> + <see cref="IRequestThrottle"/> + <see cref="ISignalBroadcaster"/>). Both
/// overloads are idempotent and safe to call from anywhere, any number of times — a consumer that
/// <em>needs</em> coordination calls the parameterless overload to pull a requirement, and the application
/// calls the configuring overload once to choose the backend. The order of those calls does not matter.
/// </summary>
public static class CoordinationServiceCollectionExtensions {

	/// <summary>
	/// Ensures the coordination machinery is present. <see cref="IReplayGuard"/>/<see cref="IRequestThrottle"/>
	/// get the fail-closed <see cref="RequiresBackendReplayGuard"/>/<see cref="RequiresBackendRequestThrottle"/>
	/// sentinels if no backend has been registered yet (idempotent <c>TryAdd</c>) — a component that requires
	/// one of them calls this to make the dependency explicit, so
	/// <see cref="CoordinationPostureValidator.Validate(IServiceCollection)"/> can fail fast at startup rather
	/// than letting the host run mis-configured. <see cref="ISignalBroadcaster"/> gets the safe
	/// <see cref="InMemorySignalBroadcaster"/> default directly (no sentinel): silently falling back to
	/// in-process-only delivery is not a security regression, so it never needs to fail-fast.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> for chaining.</returns>
	public static IServiceCollection AddCoordination(this IServiceCollection services) {
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IReplayGuard, RequiresBackendReplayGuard>();
		services.TryAddSingleton<IRequestThrottle, RequiresBackendRequestThrottle>();
		services.TryAddSingleton<ISignalBroadcaster, InMemorySignalBroadcaster>();
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
