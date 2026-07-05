namespace Cirreum.Coordination;

/// <summary>
/// Ephemeral publish/subscribe primitive for notifying other instances of the same running fact — a live
/// signal, not a durable message. Backed by any store capable of pub/sub: the built-in single-instance
/// <see cref="InMemorySignalBroadcaster"/>, or a distributed adapter (e.g. <c>Cirreum.Coordination.Redis</c>)
/// selected via <c>AddCoordination</c>.
/// </summary>
/// <remarks>
/// <para>
/// A third primitive alongside <see cref="IReplayGuard"/> (atomic claim) and <see cref="IRequestThrottle"/>
/// (atomic counter) — all three are atomic-or-ephemeral operations over a shared backend that keep independent
/// process instances behaviorally consistent, the same charter classic coordination services (ZooKeeper, etcd,
/// Consul) bundle as locks, leases, and watch/notify.
/// </para>
/// <para>
/// Delivery is at-most-once and unbuffered: a subscriber that isn't currently listening does not receive a
/// signal published while it was gone, and there is no replay. This is deliberate — for durable, ordered,
/// replayable distribution, use <c>Cirreum.Messaging.Distributed</c> instead. This primitive exists for the
/// opposite case: a best-effort, low-latency nudge to whichever instances happen to be connected right now.
/// </para>
/// <para>
/// Unlike <see cref="IReplayGuard"/>/<see cref="IRequestThrottle"/>, this primitive does not participate in
/// <see cref="CoordinationPostureValidator"/>'s fail-closed posture check: silently falling back to
/// in-process-only delivery is a safe degradation (today's baseline, not a security regression), so an
/// application that never chooses a distributed backend is never fail-fast blocked.
/// </para>
/// </remarks>
public interface ISignalBroadcaster {

	/// <summary>
	/// Publishes <paramref name="payload"/> to every current subscriber of <paramref name="channel"/>.
	/// Fire-and-forget from the caller's perspective — there is no acknowledgment, no persistence, and no
	/// guarantee a subscriber that isn't currently listening will ever see this signal.
	/// </summary>
	/// <param name="channel">The channel name.</param>
	/// <param name="payload">The signal payload. Format is caller-defined.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	ValueTask PublishAsync(string channel, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

	/// <summary>
	/// Registers <paramref name="handler"/> to be invoked for every signal published to
	/// <paramref name="channel"/> from the point of subscription onward. There is no unsubscribe — a
	/// subscription is intended to live for the application's lifetime.
	/// </summary>
	/// <param name="channel">The channel name.</param>
	/// <param name="handler">Invoked once per received signal.</param>
	/// <param name="cancellationToken">Cancellation token for the subscribe operation itself, not the
	/// subscription's lifetime.</param>
	ValueTask SubscribeAsync(string channel, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler, CancellationToken cancellationToken = default);

}
