namespace Cirreum.Coordination;

/// <summary>
/// Fail-closed <see cref="IReplayGuard"/> sentinel. Registered by <see cref="CoordinationServiceCollectionExtensions.AddCoordination(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// when a consumer <em>pulls</em> coordination but no backend has been chosen yet. It never claims a token —
/// every call throws — so a misconfigured host can never silently run replay-exposed.
/// <see cref="CoordinationPostureValidator"/> detects this sentinel at startup and fails fast; this throw is
/// the failsafe if that boot check is somehow bypassed.
/// </summary>
internal sealed class RequiresBackendReplayGuard : IReplayGuard {

	/// <inheritdoc />
	public ValueTask<bool> TryClaimAsync(string token, TimeSpan ttl, CancellationToken cancellationToken = default) =>
		throw CoordinationPostureValidator.NoBackendError();

}
