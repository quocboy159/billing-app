namespace ArchitectureTests.Layers;

public sealed class LayerTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Domain.Bills.Bill).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly WebApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNot_DependOn_Application_Infrastructure_WebApi()
    {
        Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Application", "Infrastructure", "Web.Api")
            .GetResult()
            .IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Application_ShouldNot_DependOn_Infrastructure_WebApi()
    {
        Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Infrastructure", "Web.Api")
            .GetResult()
            .IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Infrastructure_ShouldNot_DependOn_WebApi()
    {
        Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Web.Api")
            .GetResult()
            .IsSuccessful.ShouldBeTrue();
    }
}
