namespace Cirreum.Coordination;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A backend-contributed boot-time check run by
/// <see cref="CoordinationPostureValidator.Validate(IServiceCollection)"/> after the
/// application's composition is complete. Lets a chosen backend verify that the
/// registrations it depends on are actually present — turning a dependency that would
/// otherwise fail on first use into a clear startup error.
/// </summary>
/// <remarks>
/// <para>
/// Register implementations as <b>singleton instances</b>
/// (<c>services.AddSingleton&lt;ICoordinationPostureCheck&gt;(new …)</c>). The validator runs
/// before a service provider exists, so it inspects the service collection's descriptors and
/// can only invoke checks whose instances are on the descriptors themselves — a
/// factory-registered check is silently invisible to it.
/// </para>
/// <para>
/// Checks must be pure inspection: examine <see cref="IServiceCollection"/> descriptors,
/// build nothing, connect to nothing.
/// </para>
/// </remarks>
public interface ICoordinationPostureCheck {

	/// <summary>
	/// Inspects the fully-composed service collection and returns an error message when the
	/// posture is invalid, or <see langword="null"/> when it is satisfied.
	/// </summary>
	/// <param name="services">The fully-composed service collection.</param>
	/// <returns>
	/// A human-readable description of the mis-configuration, used as the startup error;
	/// <see langword="null"/> when the check passes.
	/// </returns>
	string? Check(IServiceCollection services);

}
