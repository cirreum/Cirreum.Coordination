namespace Cirreum.Coordination;

/// <summary>
/// Fail-closed <see cref="IRequestThrottle"/> sentinel. Registered by <see cref="CoordinationServiceCollectionExtensions.AddCoordination(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// when a consumer <em>pulls</em> coordination but no backend has been chosen yet. It never records a hit —
/// every call throws — so a misconfigured host can never silently run unthrottled.
/// <see cref="CoordinationPostureValidator"/> detects this sentinel at startup and fails fast; this throw is
/// the failsafe if that boot check is somehow bypassed.
/// </summary>
internal sealed class RequiresBackendRequestThrottle : IRequestThrottle {

	/// <inheritdoc />
	public ValueTask<ThrottleOutcome> RecordAsync(string key, TimeSpan window, long limit, CancellationToken cancellationToken = default) =>
		throw CoordinationPostureValidator.NoBackendError();

}
