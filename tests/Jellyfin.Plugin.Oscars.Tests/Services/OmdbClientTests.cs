using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Jellyfin.Plugin.Oscars.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Oscars.Tests.Services;

public sealed class OmdbClientTests
{
    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenApiKeyIsMissing()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(new PluginConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsMappedPayload_WhenOmdbRespondsWithSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "imdbID": "tt0111161",
                  "Title": "The Shawshank Redemption",
                  "Awards": "Nominated for 7 Oscars. Another 21 wins & 43 nominations.",
                  "Response": "True"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.NotNull(result);
        Assert.Equal("tt0111161", result.ImdbId);
        Assert.Equal("The Shawshank Redemption", result.Title);
        Assert.Equal("Nominated for 7 Oscars. Another 21 wins & 43 nominations.", result.AwardsText);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenOmdbRespondsWithFailurePayload()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "Response": "False",
                  "Error": "Movie not found!"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenStatusCodeIsNotSuccessful()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenResponseJsonIsInvalid()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenTransportThrowsHttpRequestException()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_PropagatesCallerCancellation()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("canceled"));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetByImdbIdAsync("tt0111161", cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenRequestTimesOutWithoutCallerCancellation()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("timeout"));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByImdbIdAsync_EncodesApiKeyAndImdbIdInRequestUri()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent(
                    """
                    {
                      "imdbID": "tt0111161/extended",
                      "Response": "True"
                    }
                    """)
            });
        });
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(new PluginConfiguration
        {
            OmdbApiKey = "key with spaces",
        }), NullLogger<OmdbClient>.Instance);

        await client.GetByImdbIdAsync("tt0111161/extended");

        Assert.NotNull(requestUri);
        Assert.Equal("https://www.omdbapi.com/", requestUri!.GetLeftPart(UriPartial.Path));

        var query = HttpUtility.ParseQueryString(requestUri.Query);
        Assert.Equal("key with spaces", query["apikey"]);
        Assert.Equal("tt0111161/extended", query["i"]);
    }

    [Fact]
    public async Task GetByImdbIdAsync_NormalizesWhitespaceAndNaValues()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "imdbID": " tt0111161 ",
                  "Title": "  The Matrix  ",
                  "Awards": " N/A ",
                  "Response": "True"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync(" tt0111161 ");

        Assert.NotNull(result);
        Assert.Equal("tt0111161", result.ImdbId);
        Assert.Equal("The Matrix", result.Title);
        Assert.Null(result.AwardsText);
    }

    [Fact]
    public async Task GetByImdbIdAsync_ReturnsNull_WhenResponseImdbIdDoesNotMatchRequest()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "imdbID": "tt0133094",
                  "Response": "True"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.GetByImdbIdAsync("tt0111161");

        Assert.Null(result);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFailure_WhenApiKeyIsMissing()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(new PluginConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_api_key", result.ErrorCode);
        Assert.Equal("OMDb API key is missing. Enter your key and try again.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFailure_WhenApiKeyOverrideIsExplicitlyBlank()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_api_key", result.ErrorCode);
        Assert.Equal("OMDb API key is missing. Enter your key and try again.", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsSuccess_WhenOmdbRespondsWithSuccess()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "imdbID": "tt0111161",
                  "Title": "The Shawshank Redemption",
                  "Response": "True"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Connected to OMDb successfully.", result.Message);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsOmdbErrorMessage_WhenOmdbRejectsKey()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(
                """
                {
                  "Response": "False",
                  "Error": "Invalid API key!"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync("bad-key");

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_api_key", result.ErrorCode);
        Assert.Equal("Invalid API key!", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsOmdbErrorMessage_WhenOmdbRejectsKeyWithHttp401()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = CreateJsonContent(
                """
                {
                  "Response": "False",
                  "Error": "Invalid API key!"
                }
                """)
        }));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync("bad-key");

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_api_key", result.ErrorCode);
        Assert.Equal("Invalid API key!", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsRequestFailure_WhenTransportThrows()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        using var httpClient = new HttpClient(handler);
        var client = new OmdbClient(httpClient, CreateConfigurationService(CreateConfiguration()), NullLogger<OmdbClient>.Instance);

        var result = await client.TestConnectionAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("request_failure", result.ErrorCode);
        Assert.Equal("Unable to reach OMDb. Check your network connection and try again.", result.Message);
    }

    private static PluginConfiguration CreateConfiguration()
    {
        return new PluginConfiguration
        {
            OmdbApiKey = "test-key"
        };
    }

    private static IPluginConfigurationService CreateConfigurationService(PluginConfiguration configuration)
    {
        return new StubPluginConfigurationService(configuration);
    }

    private static StringContent CreateJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }

    private sealed class StubPluginConfigurationService : IPluginConfigurationService
    {
        private readonly PluginConfiguration _configuration;

        public StubPluginConfigurationService(PluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public PluginConfiguration GetCurrent() => _configuration;

        public PluginConfiguration Save(PluginConfiguration configuration)
        {
            throw new NotSupportedException();
        }
    }
}
