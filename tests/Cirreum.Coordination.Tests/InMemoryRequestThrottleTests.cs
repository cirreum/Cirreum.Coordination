namespace Cirreum.Coordination.Tests;

using System.Collections.Concurrent;
using Cirreum.Coordination;

public sealed class InMemoryRequestThrottleTests {

	private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

	[Fact]
	public async Task Hits_within_the_limit_are_allowed_with_increasing_count() {
		var throttle = new InMemoryRequestThrottle();

		var first = await throttle.RecordAsync("k", Window, 3);
		var second = await throttle.RecordAsync("k", Window, 3);
		var third = await throttle.RecordAsync("k", Window, 3);

		first.Should().Be(new ThrottleOutcome(1, Allowed: true, RetryAfter: null));
		second.Should().Be(new ThrottleOutcome(2, Allowed: true, RetryAfter: null));
		third.Should().Be(new ThrottleOutcome(3, Allowed: true, RetryAfter: null));
	}

	[Fact]
	public async Task A_hit_over_the_limit_is_throttled_with_a_retry_after_hint() {
		var throttle = new InMemoryRequestThrottle();

		await throttle.RecordAsync("k", Window, 1);
		var blocked = await throttle.RecordAsync("k", Window, 1);

		blocked.Allowed.Should().BeFalse();
		blocked.Count.Should().Be(2);
		blocked.RetryAfter.Should().NotBeNull();
		blocked.RetryAfter!.Value.Should().BeGreaterThan(TimeSpan.Zero);
		blocked.RetryAfter!.Value.Should().BeLessThanOrEqualTo(Window);
	}

	[Fact]
	public async Task The_window_resets_after_it_expires() {
		var throttle = new InMemoryRequestThrottle();
		// Generous margins: the two quick hits must land inside the window even under parallel-test load, and
		// the delay must comfortably exceed it (the in-memory throttle reads wall-clock TickCount64 directly,
		// with no TimeProvider seam to fake).
		var shortWindow = TimeSpan.FromMilliseconds(200);

		(await throttle.RecordAsync("k", shortWindow, 1)).Allowed.Should().BeTrue();
		(await throttle.RecordAsync("k", shortWindow, 1)).Allowed.Should().BeFalse();

		await Task.Delay(500);

		(await throttle.RecordAsync("k", shortWindow, 1)).Should().Be(new ThrottleOutcome(1, Allowed: true, RetryAfter: null));
	}

	[Fact]
	public async Task Distinct_keys_have_independent_windows() {
		var throttle = new InMemoryRequestThrottle();

		(await throttle.RecordAsync("a", Window, 1)).Allowed.Should().BeTrue();
		(await throttle.RecordAsync("b", Window, 1)).Allowed.Should().BeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	public async Task A_non_positive_window_is_rejected_fail_closed(int milliseconds) {
		var throttle = new InMemoryRequestThrottle();

		var act = async () => await throttle.RecordAsync("k", TimeSpan.FromMilliseconds(milliseconds), 1);

		await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task A_non_positive_limit_is_rejected_fail_closed(long limit) {
		var throttle = new InMemoryRequestThrottle();

		var act = async () => await throttle.RecordAsync("k", Window, limit);

		await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task A_null_or_blank_key_is_rejected(string? key) {
		var throttle = new InMemoryRequestThrottle();

		var act = async () => await throttle.RecordAsync(key!, Window, 1);

		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task Concurrent_hits_produce_contiguous_lost_update_free_counts() {
		var throttle = new InMemoryRequestThrottle();
		const int hits = 500;
		const long limit = 100;
		var counts = new ConcurrentBag<long>();
		var allowed = 0;

		await Parallel.ForEachAsync(
			Enumerable.Range(0, hits),
			async (_, ct) => {
				var outcome = await throttle.RecordAsync("contended", Window, limit, ct);
				counts.Add(outcome.Count);
				if (outcome.Allowed) {
					Interlocked.Increment(ref allowed);
				}
			});

		// Lost-update-free: every increment produced a distinct value, exactly 1..hits.
		counts.OrderBy(c => c).Should().Equal(Enumerable.Range(1, hits).Select(i => (long)i));
		// Exactly `limit` hits fell within the allowance.
		allowed.Should().Be((int)limit);
	}

}
