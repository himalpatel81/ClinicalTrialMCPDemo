using System.Net.Http;

namespace ClinicalTrials.Tests.Testing;

internal sealed class TestHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => httpClient;
}
