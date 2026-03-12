using System.Net;
using DAL.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Short_termApartmentAPI.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = "01234567890123456789012345678901",
                        ["Jwt:Issuer"] = "integration-tests",
                        ["Jwt:Audience"] = "integration-tests",
                        ["ConnectionStrings:DefaultConnection"] = "server=localhost;database=ApartmentDb;user=root;password=123456",
                    }
                );
            }
        );

        builder.ConfigureServices(
            services =>
            {
                // The production `Program` registers MySQL using ServerVersion.AutoDetect(), which attempts a live DB
                // connection during startup. For integration tests, replace it with InMemory.
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("IntegrationTests");
                });
            }
        );
    }
}

public class ApiSmokeTests
{
    [Fact]
    public async Task Swagger_json_endpoint_is_available()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            }
        );

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
