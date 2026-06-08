namespace Cirreum.Coordination.Tests;

using System.Collections.Concurrent;
using Cirreum.Coordination;

public sealed class InMemoryReplayGuardTests {

	private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

	[Fact]
	public async Task First_claim_of_a_token_succeeds() {
		var guard = new InMemoryReplayGuard();

		(await guard.TryClaimAsync("nonce-1", Ttl)).Should().BeTrue();
	}

	[Fact]
	public async Task Repeat_claim_of_a_live_token_is_rejected() {
		var guard = new InMemoryReplayGuard();

		(await guard.TryClaimAsync("nonce-1", Ttl)).Should().BeTrue();
		(await guard.TryClaimAsync("nonce-1", Ttl)).Should().BeFalse();
	}

	[Fact]
	public async Task Distinct_tokens_each_claim_independently() {
		var guard = new InMemoryReplayGuard();

		(await guard.TryClaimAsync("a", Ttl)).Should().BeTrue();
		(await guard.TryClaimAsync("b", Ttl)).Should().BeTrue();
	}

	[Fact]
	public async Task A_token_can_be_reclaimed_once_its_ttl_expires() {
		var guard = new InMemoryReplayGuard();
		// Generous margins: the two quick claims must land inside the window even under parallel-test load,
		// and the delay must comfortably exceed it (the in-memory guard reads wall-clock TickCount64 directly,
		// with no TimeProvider seam to fake).
		var shortTtl = TimeSpan.FromMilliseconds(200);

		(await guard.TryClaimAsync("nonce-1", shortTtl)).Should().BeTrue();
		(await guard.TryClaimAsync("nonce-1", shortTtl)).Should().BeFalse();

		await Task.Delay(500);

		(await guard.TryClaimAsync("nonce-1", shortTtl)).Should().BeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task A_non_positive_ttl_is_rejected_fail_closed(int milliseconds) {
		var guard = new InMemoryReplayGuard();

		var act = async () => await guard.TryClaimAsync("x", TimeSpan.FromMilliseconds(milliseconds));

		await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task A_null_or_blank_token_is_rejected(string? token) {
		var guard = new InMemoryReplayGuard();

		var act = async () => await guard.TryClaimAsync(token!, Ttl);

		await act.Should().ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task Concurrent_claims_of_the_same_token_yield_exactly_one_winner() {
		var guard = new InMemoryReplayGuard();
		const int racers = 200;
		var results = new ConcurrentBag<bool>();

		await Parallel.ForEachAsync(
			Enumerable.Range(0, racers),
			async (_, ct) => results.Add(await guard.TryClaimAsync("contended", Ttl, ct)));

		results.Count(won => won).Should().Be(1, "a single-use claim must admit exactly one racer");
		results.Should().HaveCount(racers);
	}

}
