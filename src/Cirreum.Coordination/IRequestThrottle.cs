namespace Cirreum.Coordination;

/// <summary>
/// Fixed-window rate-limit primitive (atomic windowed counter). Backed by any store with an atomic counter +
/// expiry: the built-in single-instance <see cref="InMemoryRequestThrottle"/>, or a distributed adapter
/// (e.g. <c>Cirreum.Coordination.Redis</c>) selected via <c>AddCoordination</c>.
/// </summary>
/// <remarks>
/// Split from <see cref="IReplayGuard"/> because an atomic windowed counter is harder to provide than a
/// set-if-absent — a backend may implement replay protection without throttling.
/// </remarks>
public interface IRequestThrottle {

	/// <summary>
	/// Atomically records one hit against <paramref name="key"/> inside a fixed window of
	/// <paramref name="window"/> and reports whether the caller is still within <paramref name="limit"/>.
	/// The window is anchored at the first hit and does NOT slide; it resets once expired. Fail-closed: a
	/// non-positive <paramref name="window"/> or <paramref name="limit"/> is rejected.
	/// </summary>
	/// <param name="key">The throttle subject (e.g. a client id or derived source key). Must be non-empty.</param>
	/// <param name="window">The fixed window length; must be positive.</param>
	/// <param name="limit">The maximum allowed hits per window; must be positive.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The window <see cref="ThrottleOutcome.Count"/> (this hit included), whether it is
	/// <see cref="ThrottleOutcome.Allowed"/>, and a <see cref="ThrottleOutcome.RetryAfter"/> hint when throttled.</returns>
	ValueTask<ThrottleOutcome> RecordAsync(string key, TimeSpan window, long limit, CancellationToken cancellationToken = default);

}
