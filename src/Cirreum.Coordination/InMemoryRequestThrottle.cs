namespace Cirreum.Coordination;

using System.Collections.Concurrent;

/// <summary>
/// Single-instance in-memory <see cref="IRequestThrottle"/>. Fixed-window counter: the window deadline is
/// fixed at creation and never extended (so the window does not slide), the count is mutated only via
/// <see cref="Interlocked.Increment(ref long)"/> (lost-update-free), and deadlines use the monotonic
/// <see cref="Environment.TickCount64"/>. Single-sweeper opportunistic eviction bounds memory under
/// high-cardinality keys. The built-in default backend; correct for single-instance and development
/// deployments. For multi-instance deployments register a distributed adapter (e.g. Redis), since a
/// per-process counter does not enforce a shared limit across instances.
/// </summary>
internal sealed class InMemoryRequestThrottle : IRequestThrottle {

	private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

	private const int SweepThreshold = 1000;
	private int _opsSinceSweep;
	private int _sweeping;

	/// <inheritdoc />
	public ValueTask<ThrottleOutcome> RecordAsync(string key, TimeSpan window, long limit, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		if (limit <= 0) {
			throw new ArgumentOutOfRangeException(nameof(limit), limit, "Throttle limit must be positive.");
		}

		var deadlineTicks = CoordinationDeadline.From(window, nameof(window));
		this.MaybeSweep();

		// Fixed-window counter: the deadline is fixed when the window is created and never extended.
		// Interlocked.Increment on the shared Window guarantees distinct, lost-update-free counts even when a
		// racer increments between our create and our first Increment.
		while (true) {
			if (this._windows.TryGetValue(key, out var existing)) {
				if (!existing.IsExpired) {
					return ValueTask.FromResult(Evaluate(existing.Increment(), limit, existing.DeadlineTicks));
				}

				var replacement = new Window(deadlineTicks);
				if (this._windows.TryUpdate(key, replacement, existing)) {
					return ValueTask.FromResult(Evaluate(replacement.Increment(), limit, replacement.DeadlineTicks)); // Fresh window starts at 1.
				}

				continue; // Lost the window-reset CAS — retry and observe the winner.
			}

			var created = new Window(deadlineTicks);
			if (this._windows.TryAdd(key, created)) {
				return ValueTask.FromResult(Evaluate(created.Increment(), limit, created.DeadlineTicks)); // First hit in a new window.
			}

			// Another caller created it first — retry to increment theirs.
		}
	}

	private static ThrottleOutcome Evaluate(long count, long limit, long deadlineTicks) {
		if (count <= limit) {
			return new ThrottleOutcome(count, Allowed: true, RetryAfter: null);
		}

		var remainingMilliseconds = deadlineTicks - Environment.TickCount64;
		var retryAfter = remainingMilliseconds > 0 ? TimeSpan.FromMilliseconds(remainingMilliseconds) : TimeSpan.Zero;
		return new ThrottleOutcome(count, Allowed: false, retryAfter);
	}

	private void MaybeSweep() {
		if (Interlocked.Increment(ref this._opsSinceSweep) < SweepThreshold) {
			return;
		}

		if (Interlocked.CompareExchange(ref this._sweeping, 1, 0) != 0) {
			return;
		}

		try {
			Interlocked.Exchange(ref this._opsSinceSweep, 0);

			foreach (var pair in this._windows) {
				if (pair.Value.IsExpired) {
					this._windows.TryRemove(pair);
				}
			}
		} finally {
			Interlocked.Exchange(ref this._sweeping, 0);
		}
	}

	// Fixed-window counter. The deadline (monotonic Environment.TickCount64) is set at creation and never
	// extended; the value is mutated only via Interlocked. Reference identity is the window-reset CAS token.
	private sealed class Window(long deadlineTicks) {

		private long _value;

		public long DeadlineTicks { get; } = deadlineTicks;

		public bool IsExpired => Environment.TickCount64 >= this.DeadlineTicks;

		public long Increment() => Interlocked.Increment(ref this._value);

	}

}
