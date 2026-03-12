using BLL.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BLL.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddBLLDependencies_does_not_throw_and_service_provider_can_be_built()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddBLLDependencies(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider);
    }
}
