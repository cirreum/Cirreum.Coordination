namespace Cirreum.Coordination;

/// <summary>
/// Shared deadline math for the in-memory coordination primitives. Deadlines use the monotonic
/// <see cref="Environment.TickCount64"/> so an NTP / VM wall-clock step cannot prematurely expire a live
/// entry or extend it past its window.
/// </summary>
internal static class CoordinationDeadline {

	/// <summary>
	/// Computes a monotonic deadline <paramref name="window"/> from now. Rounds up so a positive
	/// sub-millisecond window cannot collapse to a zero-length (instantly-expired) deadline — that would
	/// silently re-open replay protection or reset a throttle on the first call. A non-positive window is
	/// rejected outright rather than silently treated as "infinite" (which would create an entry the sweep
	/// can never evict). No overflow guard is needed: <see cref="TimeSpan.MaxValue"/> is ~9.2e14 ms, four
	/// orders of magnitude below <see cref="long.MaxValue"/> even after adding <see cref="Environment.TickCount64"/>.
	/// </summary>
	internal static long From(TimeSpan window, string paramName) {
		var milliseconds = (long)Math.Ceiling(window.TotalMilliseconds);
		if (milliseconds <= 0) {
			throw new ArgumentOutOfRangeException(
				paramName, window,
				"Coordination windows require a positive duration; a non-positive value would create an entry that never expires.");
		}

		return Environment.TickCount64 + milliseconds;
	}

}
