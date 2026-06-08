namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Configures the atomic-coordination backend (<see cref="IReplayGuard"/> + <see cref="IRequestThrottle"/>).
/// The backend is registered once (via <c>AddCoordination</c>) and shared; consumers resolve whichever
/// interface they need. Distributed adapters (e.g. <c>Cirreum.Coordination.Redis</c>) contribute their own
/// <c>UseXxx()</c> extension methods on this type, keeping consumers blind to the backing store.
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
	/// <see cref="InMemoryRequestThrottle"/>). Correct for single-instance and development deployments; it
	/// does NOT coordinate across instances. Replaces any previously-registered coordination backend so the
	/// last <c>UseXxx()</c> call wins.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public CoordinationBuilder UseInMemory() {
		this.Services.Replace(ServiceDescriptor.Singleton<IReplayGuard, InMemoryReplayGuard>());
		this.Services.Replace(ServiceDescriptor.Singleton<IRequestThrottle, InMemoryRequestThrottle>());
		return this;
	}

}
