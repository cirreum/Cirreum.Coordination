namespace Cirreum.Coordination.Tests;

using Cirreum.Coordination;

public sealed class CoordinationScopeTests {

	[Fact]
	public void A_scope_carries_its_value() {
		new CoordinationScope("MyApp:Production").Value.Should().Be("MyApp:Production");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void A_null_or_blank_value_is_rejected(string? value) {
		var act = () => new CoordinationScope(value!);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void For_composes_the_canonical_application_environment_scope() {
		CoordinationScope.For("MyApp", "Production").Value.Should().Be("MyApp:Production");
	}

	[Theory]
	[InlineData(null, "Production")]
	[InlineData("", "Production")]
	[InlineData("   ", "Production")]
	[InlineData("MyApp", null)]
	[InlineData("MyApp", "")]
	[InlineData("MyApp", "   ")]
	public void For_rejects_a_null_or_blank_application_or_environment_name(string? applicationName, string? environmentName) {
		var act = () => CoordinationScope.For(applicationName!, environmentName!);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Scopes_compare_by_value() {
		CoordinationScope.For("MyApp", "Production").Should().Be(new CoordinationScope("MyApp:Production"));
	}

}
