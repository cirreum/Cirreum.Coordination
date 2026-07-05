namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Configures the coordination backend (<see cref="IReplayGuard"/> + <see cref="IRequestThrottle"/> +
/// <see cref="ISignalBroadcaster"/>). The backend is registered once (via <c>AddCoordination</c>) and shared;
/// consumers resolve whichever interface they need. Distributed adapters (e.g. <c>Cirreum.Coordination.Redis</c>)
/// contribute their own <c>UseXxx()</c> extension methods on this type, keeping consumers blind to the backing
/// store.
/// </summary>
public sealed class CoordinationBuilder {

	/// <summary>The DI service collection the coordination backend registers into.</summary>
	public IServiceCollection Services { get; }

	/// <summary>Creates a builder over <paramref name="services"/>.</summary>
	/// <param name="services">The application's service collection.</param>
	public CoordinationBuilder(IServiceCollection services) {
		ArgumentNullException.ThrowIfNull(services);
		this.Services = services;
	}

	/// <summary>
	/// Uses the built-in single-instance in-memory backend (<see cref="InMemoryReplayGuard"/> +
	/// <see cref="InMemoryRequestThrottle"/> + <see cref="InMemorySignalBroadcaster"/>). Correct for
	/// single-instance and development deployments; it does NOT coordinate across instances. Replaces any
	/// previously-registered coordination backend so the last <c>UseXxx()</c> call wins.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public CoordinationBuilder UseInMemory() {
		this.Services.Replace(ServiceDescriptor.Singleton<IReplayGuard, InMemoryReplayGuard>());
		this.Services.Replace(ServiceDescriptor.Singleton<IRequestThrottle, InMemoryRequestThrottle>());
		this.Services.Replace(ServiceDescriptor.Singleton<ISignalBroadcaster, InMemorySignalBroadcaster>());
		return this;
	}

	/// <summary>
	/// Namespaces all coordination state under <paramref name="scope"/> (see <see cref="CoordinationScope"/>)
	/// so multiple applications and environments can safely share one distributed backend. Honored by
	/// distributed adapters; the in-memory backend ignores it — a process is already its own scope. Replaces
	/// any previously-registered scope (including a composition-provided default), so an explicit call always
	/// wins and the last call is authoritative.
	/// </summary>
	/// <param name="scope">The opaque scope value. Use <see cref="WithScope(string, string)"/> for the
	/// canonical application/environment composition.</param>
	/// <returns>The builder for chaining.</returns>
	public CoordinationBuilder WithScope(string scope) {
		this.Services.Replace(ServiceDescriptor.Singleton(new CoordinationScope(scope)));
		return this;
	}

	/// <summary>
	/// Namespaces all coordination state under the canonical application scope,
	/// <c>{applicationName}:{environmentName}</c> (see <see cref="CoordinationScope.For"/>).
	/// </summary>
	/// <param name="applicationName">The application's name.</param>
	/// <param name="environmentName">The runtime environment name (for example "Development" or "Production").</param>
	/// <returns>The builder for chaining.</returns>
	public CoordinationBuilder WithScope(string applicationName, string environmentName) {
		this.Services.Replace(ServiceDescriptor.Singleton(CoordinationScope.For(applicationName, environmentName)));
		return this;
	}

}
