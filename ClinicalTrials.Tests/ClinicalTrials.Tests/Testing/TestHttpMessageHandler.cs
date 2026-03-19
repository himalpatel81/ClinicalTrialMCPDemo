using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ClinicalTrials.Tests.Testing;

internal sealed class TestHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return handler(request, cancellationToken);
    }

    public static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        new(statusCode)
        {
            Content = new StringContent(body)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue(mediaType)
                }
            }
        };
}
