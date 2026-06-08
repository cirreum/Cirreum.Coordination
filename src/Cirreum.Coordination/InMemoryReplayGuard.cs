namespace Cirreum.Coordination;

using System.Collections.Concurrent;

/// <summary>
/// Single-instance in-memory <see cref="IReplayGuard"/>. Lock-free set-if-absent via a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> compare-and-swap, with monotonic
/// <see cref="Environment.TickCount64"/> deadlines and single-sweeper opportunistic eviction so a flood of
/// unique tokens cannot grow memory unbounded. The built-in default backend; correct for single-instance
/// and development deployments. For multi-instance deployments register a distributed adapter (e.g. Redis),
/// since a per-process claim set does not coordinate replay across instances.
/// </summary>
internal sealed class InMemoryReplayGuard : IReplayGuard {

	private readonly ConcurrentDictionary<string, Claim> _claims = new(StringComparer.Ordinal);

	// Opportunistic eviction: tokens (nonces) are high-cardinality and frequently never re-touched, so
	// without a sweep an attacker flooding unique tokens could grow memory unbounded. Sweeping every Nth op
	// keeps the cost amortized and bounded.
	private const int SweepThreshold = 1000;
	private int _opsSinceSweep;
	private int _sweeping; // 0 = idle, 1 = a sweep is in progress (single-sweeper gate).

	/// <inheritdoc />
	public ValueTask<bool> TryClaimAsync(string token, TimeSpan ttl, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(token);

		var claim = new Claim(CoordinationDeadline.From(ttl, nameof(ttl)));
		this.MaybeSweep();

		// Lock-free claim: TryAdd wins outright when absent; if a claim exists we either reject (still live)
		// or compare-and-swap over the expired one (TryUpdate compares by reference, so only one racer can
		// replace a given expired claim — the rest loop and re-observe the live winner).
		while (true) {
			if (this._claims.TryAdd(token, claim)) {
				return ValueTask.FromResult(true);
			}

			if (!this._claims.TryGetValue(token, out var existing)) {
				continue; // Vanished between TryAdd and read — retry the add.
			}

			if (!existing.IsExpired) {
				return ValueTask.FromResult(false); // Live claim already held → replay.
			}

			if (this._claims.TryUpdate(token, claim, existing)) {
				return ValueTask.FromResult(true); // Reclaimed the expired slot.
			}

			// Lost the CAS to a concurrent claimer/sweeper — retry.
		}
	}

	private void MaybeSweep() {
		if (Interlocked.Increment(ref this._opsSinceSweep) < SweepThreshold) {
			return;
		}

		// Single-sweeper: only the thread that flips _sweeping 0->1 performs the O(N) scan; concurrent
		// threads that also crossed the threshold skip it (avoids a thundering herd of simultaneous scans).
		if (Interlocked.CompareExchange(ref this._sweeping, 1, 0) != 0) {
			return;
		}

		try {
			Interlocked.Exchange(ref this._opsSinceSweep, 0);

			foreach (var pair in this._claims) {
				if (pair.Value.IsExpired) {
					// KeyValuePair overload removes only if the entry is unchanged — never evicts a fresh reclaim.
					this._claims.TryRemove(pair);
				}
			}
		} finally {
			Interlocked.Exchange(ref this._sweeping, 0);
		}
	}

	// Immutable presence flag. Reference identity is the compare-and-swap token; the deadline is a monotonic
	// Environment.TickCount64 value (clock-step-immune).
	private sealed class Claim(long deadlineTicks) {

		public long DeadlineTicks { get; } = deadlineTicks;

		public bool IsExpired => Environment.TickCount64 >= this.DeadlineTicks;

	}

}
