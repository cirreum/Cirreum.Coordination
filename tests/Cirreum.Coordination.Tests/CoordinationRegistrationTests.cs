namespace Cirreum.Coordination.Tests;

using Cirreum.Coordination;
using Microsoft.Extensions.DependencyInjection;

public sealed class CoordinationRegistrationTests {

	[Fact]
	public void Choosing_in_memory_registers_both_primitives_as_singletons() {
		var services = new ServiceCollection();

		services.AddCoordination(c => c.UseInMemory());

		using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<IReplayGuard>().Should().BeOfType<InMemoryReplayGuard>();
		provider.GetRequiredService<IRequestThrottle>().Should().BeOfType<InMemoryRequestThrottle>();
		services.Single(d => d.ServiceType == typeof(IReplayGuard)).Lifetime.Should().Be(ServiceLifetime.Singleton);
		services.Single(d => d.ServiceType == typeof(IRequestThrottle)).Lifetime.Should().Be(ServiceLifetime.Singleton);
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
