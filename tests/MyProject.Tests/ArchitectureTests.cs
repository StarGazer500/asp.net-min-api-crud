using NetArchTest.Rules;

namespace MyProject.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Features_do_not_depend_on_adapters_or_vendor_sdks()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace("MyProject.Features")
            .ShouldNot().HaveDependencyOnAny("MyProject.Adapters", "Amazon.S3")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Shared_does_not_depend_on_features_or_adapters()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace("MyProject.Shared")
            .ShouldNot().HaveDependencyOnAny("MyProject.Features", "MyProject.Adapters")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
