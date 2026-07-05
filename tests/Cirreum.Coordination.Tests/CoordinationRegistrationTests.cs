namespace Cirreum.Coordination.Tests;

using Cirreum.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class CoordinationRegistrationTests {

	[Fact]
	public void Choosing_in_memory_registers_all_three_primitives_as_singletons() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory());

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IReplayGuard>().Should().BeOfType<InMemoryReplayGuard>();
		provider.GetRequiredService<IRequestThrottle>().Should().BeOfType<InMemoryRequestThrottle>();
		provider.GetRequiredService<ISignalBroadcaster>().Should().BeOfType<InMemorySignalBroadcaster>();
		services.Single(d => d.ServiceType == typeof(IReplayGuard)).Lifetime.Should().Be(ServiceLifetime.Singleton);
		services.Single(d => d.ServiceType == typeof(IRequestThrottle)).Lifetime.Should().Be(ServiceLifetime.Singleton);
		services.Single(d => d.ServiceType == typeof(ISignalBroadcaster)).Lifetime.Should().Be(ServiceLifetime.Singleton);
	}

	[Fact]
	public void Pulling_coordination_registers_the_fail_closed_sentinels() {
		var services = new ServiceCollection();

		services.AddCoordination();

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IReplayGuard>().Should().BeOfType<RequiresBackendReplayGuard>();
		provider.GetRequiredService<IRequestThrottle>().Should().BeOfType<RequiresBackendRequestThrottle>();
	}

	[Fact]
	public void Pulling_coordination_gives_ISignalBroadcaster_the_safe_default_directly_not_a_sentinel() {
		var services = new ServiceCollection();

		services.AddCoordination();

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<ISignalBroadcaster>().Should().BeOfType<InMemorySignalBroadcaster>();
	}

	[Fact]
	public async Task The_sentinel_replay_guard_fails_closed_by_throwing() {
		var services = new ServiceCollection();
		services.AddCoordination();
		using var provider = services.BuildServiceProvider();
		var guard = provider.GetRequiredService<IReplayGuard>();

		var act = async () => await guard.TryClaimAsync("x", TimeSpan.FromMinutes(1));

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*backend*");
	}

	[Fact]
	public async Task The_sentinel_throttle_fails_closed_by_throwing() {
		var services = new ServiceCollection();
		services.AddCoordination();
		using var provider = services.BuildServiceProvider();
		var throttle = provider.GetRequiredService<IRequestThrottle>();

		var act = async () => await throttle.RecordAsync("x", TimeSpan.FromMinutes(1), 1);

		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*backend*");
	}

	[Fact]
	public void Pull_then_choose_replaces_the_sentinel_with_the_backend() {
		var services = new ServiceCollection();

		services.AddCoordination();                     // pull (sentinel)
		services.AddCoordination(c => c.UseInMemory()); // choose

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IReplayGuard>().Should().BeOfType<InMemoryReplayGuard>();
		provider.GetRequiredService<IRequestThrottle>().Should().BeOfType<InMemoryRequestThrottle>();
		services.Count(d => d.ServiceType == typeof(IReplayGuard)).Should().Be(1);
	}

	[Fact]
	public void Choose_then_pull_keeps_the_backend() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory()); // choose
		services.AddCoordination();                     // pull (no-op: backend already registered)

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IReplayGuard>().Should().BeOfType<InMemoryReplayGuard>();
		services.Count(d => d.ServiceType == typeof(IReplayGuard)).Should().Be(1);
	}

	[Fact]
	public void Pulling_is_idempotent() {
		var services = new ServiceCollection();

		services.AddCoordination();
		services.AddCoordination();

		services.Count(d => d.ServiceType == typeof(IReplayGuard)).Should().Be(1);
		services.Count(d => d.ServiceType == typeof(IRequestThrottle)).Should().Be(1);
	}

	[Fact]
	public void UseInMemory_is_idempotent_leaving_a_single_registration_each() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory().UseInMemory());

		services.Count(d => d.ServiceType == typeof(IReplayGuard)).Should().Be(1);
		services.Count(d => d.ServiceType == typeof(IRequestThrottle)).Should().Be(1);
	}

	[Fact]
	public void Validate_throws_when_coordination_was_pulled_but_no_backend_chosen() {
		var services = new ServiceCollection();
		services.AddCoordination(); // sentinel only

		var act = () => CoordinationPostureValidator.Validate(services);

		act.Should().Throw<InvalidOperationException>().WithMessage("*backend*");
	}

	[Fact]
	public void Validate_passes_when_a_backend_was_chosen() {
		var services = new ServiceCollection();
		services.AddCoordination(c => c.UseInMemory());

		var act = () => CoordinationPostureValidator.Validate(services);

		act.Should().NotThrow();
	}

	[Fact]
	public void Validate_passes_when_coordination_was_never_pulled() {
		var services = new ServiceCollection();

		var act = () => CoordinationPostureValidator.Validate(services);

		act.Should().NotThrow();
	}

	[Fact]
	public void Validate_ignores_ISignalBroadcaster_entirely() {
		// ISignalBroadcaster never joins the fail-closed check: it resolves to its safe in-memory default
		// straight off the pull, with no distributed backend ever chosen, while Validate() still only reacts
		// to whether IReplayGuard/IRequestThrottle were chosen — proving ISignalBroadcaster's own posture is
		// never what's being validated.
		var services = new ServiceCollection();
		services.AddCoordination(); // pull only — no backend chosen for anything

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<ISignalBroadcaster>().Should().BeOfType<InMemorySignalBroadcaster>();

		var act = () => CoordinationPostureValidator.Validate(services);
		act.Should().Throw<InvalidOperationException>("only because IReplayGuard/IRequestThrottle are still sentinels — never because of ISignalBroadcaster");
	}

	[Fact]
	public void WithScope_registers_the_scope_as_a_singleton() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory().WithScope("MyApp:Production"));

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<CoordinationScope>().Value.Should().Be("MyApp:Production");
		services.Single(d => d.ServiceType == typeof(CoordinationScope)).Lifetime.Should().Be(ServiceLifetime.Singleton);
	}

	[Fact]
	public void WithScope_application_environment_overload_composes_the_canonical_scope() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory().WithScope("MyApp", "Production"));

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<CoordinationScope>().Value.Should().Be("MyApp:Production");
	}

	[Fact]
	public void WithScope_replaces_a_prior_registration_so_an_explicit_call_wins() {
		var services = new ServiceCollection();
		// A composition surface offered a default first (TryAdd semantics, e.g. from IDomainEnvironment).
		services.TryAddSingleton(new CoordinationScope("Default:Composed"));

		services.AddCoordination(c => c.UseInMemory().WithScope("Explicit:Choice"));

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<CoordinationScope>().Value.Should().Be("Explicit:Choice");
		services.Count(d => d.ServiceType == typeof(CoordinationScope)).Should().Be(1);
	}

	[Fact]
	public void WithScope_last_call_is_authoritative() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory().WithScope("First:Scope").WithScope("Second:Scope"));

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<CoordinationScope>().Value.Should().Be("Second:Scope");
		services.Count(d => d.ServiceType == typeof(CoordinationScope)).Should().Be(1);
	}

	[Fact]
	public void No_scope_is_registered_unless_asked_for() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory());

		using var provider = services.BuildServiceProvider();
		provider.GetService<CoordinationScope>().Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithScope_rejects_a_null_or_blank_scope(string? scope) {
		var services = new ServiceCollection();

		var act = () => services.AddCoordination(c => c.UseInMemory().WithScope(scope!));

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void AddCoordination_with_null_services_throws() {
		var act = () => ((IServiceCollection)null!).AddCoordination(c => c.UseInMemory());

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void AddCoordination_with_a_null_configure_throws() {
		var services = new ServiceCollection();

		var act = () => services.AddCoordination(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void CoordinationBuilder_with_null_services_throws() {
		var act = () => new CoordinationBuilder(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Validate_with_null_services_throws() {
		var act = () => CoordinationPostureValidator.Validate(null!);

		act.Should().Throw<ArgumentNullException>();
	}

}
