using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Web;
using Xunit;

namespace Tests
{
    public class WebTests
        : IClassFixture<WebApplicationFactory<Web.Startup>>
    {
        public WebTests(WebApplicationFactory<Web.Startup> factory)
        {
            Factory = factory;
        }

        public WebApplicationFactory<Startup> Factory { get; }

        [Fact]
        public async Task TryGetCachedResponse()
        {
            using var client = Factory.CreateClient();

            using var resp = await client.GetAsync("");
        }
    }
}