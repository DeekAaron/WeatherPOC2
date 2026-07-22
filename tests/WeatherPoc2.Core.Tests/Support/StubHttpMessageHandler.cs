using System.Net;
using System.Text;

namespace WeatherPoc2.Core.Tests.Support;

/// <summary>Fakes ONLY the HTTP transport — the real System.Text.Json path runs in the Gateway.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string? _body;
    private readonly Exception? _toThrow;

    public StubHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public StubHttpMessageHandler(Exception toThrow) => _toThrow = toThrow;

    /// <summary>The last request the Gateway sent — captured so tests can assert the outgoing request URL.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (_toThrow is not null)
            throw _toThrow;

        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body ?? string.Empty, Encoding.UTF8, "application/json"),
        });
    }
}
