namespace Cirreum.Coordination;

using System.Collections.Concurrent;
using System.Collections.Immutable;

/// <summary>
/// Single-instance in-memory <see cref="ISignalBroadcaster"/>. A genuine, correct in-process publish/subscribe
/// bus — <see cref="PublishAsync"/> invokes every handler currently subscribed to the channel, in-process,
/// awaited in registration order. The built-in default backend; correct for single-instance deployments and
/// for signals that only ever need to reach subscribers within the same process. For multi-instance
/// deployments register a distributed adapter (e.g. Redis), since a per-process subscriber list does not
/// reach subscribers in other instances.
/// </summary>
internal sealed class InMemorySignalBroadcaster : ISignalBroadcaster {

	private readonly ConcurrentDictionary<string, ImmutableArray<Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>>> _subscribers =
		new(StringComparer.Ordinal);

	/// <inheritdoc />
	public async ValueTask PublishAsync(string channel, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(channel);

		if (!this._subscribers.TryGetValue(channel, out var handlers)) {
			return;
		}

		foreach (var handler in handlers) {
			await handler(payload, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public ValueTask SubscribeAsync(string channel, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(channel);
		ArgumentNullException.ThrowIfNull(handler);

		this._subscribers.AddOrUpdate(
			channel,
			[handler],
			(_, existing) => existing.Add(handler));

		return ValueTask.CompletedTask;
	}

}
