namespace Cirreum.Coordination.Tests;

using System.Collections.Concurrent;
using System.Text;
using Cirreum.Coordination;

public sealed class InMemorySignalBroadcasterTests {

	private static ReadOnlyMemory<byte> Payload(string text) => Encoding.UTF8.GetBytes(text);

	[Fact]
	public async Task Publish_with_no_subscribers_is_a_no_op() {
		var broadcaster = new InMemorySignalBroadcaster();

		var act = async () => await broadcaster.PublishAsync("channel", Payload("x"));

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task A_subscriber_receives_a_published_payload() {
		var broadcaster = new InMemorySignalBroadcaster();
		string? received = null;

		await broadcaster.SubscribeAsync("channel", (payload, _) => {
			received = Encoding.UTF8.GetString(payload.Span);
			return ValueTask.CompletedTask;
		});
		await broadcaster.PublishAsync("channel", Payload("hello"));

		received.Should().Be("hello");
	}

	[Fact]
	public async Task Every_subscriber_to_a_channel_is_invoked() {
		var broadcaster = new InMemorySignalBroadcaster();
		var invocations = new ConcurrentBag<int>();

		await broadcaster.SubscribeAsync("channel", (_, _) => { invocations.Add(1); return ValueTask.CompletedTask; });
		await broadcaster.SubscribeAsync("channel", (_, _) => { invocations.Add(2); return ValueTask.CompletedTask; });
		await broadcaster.PublishAsync("channel", Payload("x"));

		invocations.Should().BeEquivalentTo([1, 2]);
	}

	[Fact]
	public async Task Channels_are_isolated() {
		var broadcaster = new InMemorySignalBroadcaster();
		var otherChannelInvoked = false;

		await broadcaster.SubscribeAsync("other", (_, _) => { otherChannelInvoked = true; return ValueTask.CompletedTask; });
		await broadcaster.PublishAsync("channel", Payload("x"));

		otherChannelInvoked.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task A_null_or_blank_channel_is_rejected_on_publish(string? channel) {
		var broadcaster = new InMemorySignalBroadcaster();

		var act = async () => await broadcaster.PublishAsync(channel!, Payload("x"));

		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task A_null_or_blank_channel_is_rejected_on_subscribe(string? channel) {
		var broadcaster = new InMemorySignalBroadcaster();

		var act = async () => await broadcaster.SubscribeAsync(channel!, (_, _) => ValueTask.CompletedTask);

		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task A_null_handler_is_rejected() {
		var broadcaster = new InMemorySignalBroadcaster();

		var act = async () => await broadcaster.SubscribeAsync("channel", null!);

		await act.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task Concurrent_subscribes_to_the_same_channel_are_all_retained() {
		var broadcaster = new InMemorySignalBroadcaster();
		const int subscribers = 100;
		var invocations = new ConcurrentBag<int>();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, subscribers),
			async (i, ct) => await broadcaster.SubscribeAsync("channel", (_, _) => { invocations.Add(i); return ValueTask.CompletedTask; }, ct));

		await broadcaster.PublishAsync("channel", Payload("x"));

		invocations.Should().HaveCount(subscribers, "no concurrent subscribe should be lost");
	}

}
