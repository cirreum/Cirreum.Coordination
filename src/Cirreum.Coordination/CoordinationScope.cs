namespace Cirreum.Coordination;

/// <summary>
/// Namespaces every piece of coordination state a distributed backend stores or transmits — replay claims,
/// throttle windows, and signal channels — so multiple applications and environments can safely share one
/// backend instance. Without a scope, everything sharing a backend shares one keyspace: two applications
/// (or a Test and a Production deployment of the same application) with colliding throttle keys count
/// against each other's windows, and a signal published by one is delivered to the other.
/// </summary>
/// <remarks>
/// <para>
/// The value is an opaque string as far as coordination is concerned — this package never computes one.
/// Register it explicitly via <see cref="CoordinationBuilder.WithScope(string)"/> (an explicit registration
/// always wins), or let a composition surface that knows the application's identity provide a default —
/// Cirreum's authentication composition registers the canonical <see cref="For"/> scope when none is present.
/// </para>
/// <para>
/// Distributed adapters (e.g. <c>Cirreum.Coordination.Redis</c>) resolve this from DI and, when present,
/// fold it into every key and channel they touch, so a backend-side access rule can pin an identity to its
/// own application and environment. The in-memory backend ignores it: a process is already its own scope.
/// </para>
/// </remarks>
public sealed record CoordinationScope {

	/// <summary>The scope value distributed backends fold into every key and channel.</summary>
	public string Value { get; }

	/// <summary>Creates a scope from an opaque value.</summary>
	/// <param name="value">The scope value. Must be non-blank.</param>
	public CoordinationScope(string value) {
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		this.Value = value;
	}

	/// <summary>
	/// Composes the canonical application scope, <c>{applicationName}:{environmentName}</c> — one
	/// glob-friendly segment per axis, so a backend-side access rule can pin an identity to one application,
	/// one environment, or both.
	/// </summary>
	/// <param name="applicationName">The application's name.</param>
	/// <param name="environmentName">The runtime environment name (for example "Development" or "Production").</param>
	/// <returns>The composed scope.</returns>
	public static CoordinationScope For(string applicationName, string environmentName) {
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
		ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
		return new($"{applicationName}:{environmentName}");
	}

}
