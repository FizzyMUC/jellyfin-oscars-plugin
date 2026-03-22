using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Oscars.Configuration;
using Jellyfin.Plugin.Oscars.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Oscars.Services;

/// <summary>
/// Basic OMDb client for the MVP.
/// </summary>
public sealed class OmdbClient : IOmdbClient
{
    private const string ConnectivityTestImdbId = "tt0111161";
    private const string OmdbBaseUrl = "https://www.omdbapi.com/";
    private readonly HttpClient _httpClient;
    private readonly IPluginConfigurationService _configurationService;
    private readonly ILogger<OmdbClient> _logger;

    public OmdbClient(HttpClient httpClient, IPluginConfigurationService configurationService, ILogger<OmdbClient> logger)
    {
        _httpClient = httpClient;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<OmdbMovieData?> GetByImdbIdAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imdbId);
        var normalizedImdbId = imdbId.Trim();
        var apiKey = GetApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        // TODO: Confirm the final hosting/DI pattern for HttpClient in Jellyfin plugins.
        // TODO: Add retries/rate-limit handling once the integration path is verified.
        var requestUri = BuildRequestUri(normalizedImdbId, apiKey);

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            var payload = await ReadOmdbPayloadAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogLookupFailure(normalizedImdbId, payload, response.StatusCode, "http_error");
                return null;
            }

            if (payload is null || !payload.IsSuccess)
            {
                LogLookupFailure(normalizedImdbId, payload, response.StatusCode, "omdb_failure");
                return null;
            }

            var movie = MapPayload(payload, normalizedImdbId);
            if (movie is null)
            {
                _logger.LogWarning(
                    "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
                    normalizedImdbId,
                    "imdb_id_mismatch",
                    (int)response.StatusCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(movie.AwardsText))
            {
                _logger.LogInformation(
                    "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
                    normalizedImdbId,
                    "missing_awards_data",
                    (int)response.StatusCode);
            }

            return movie;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
                normalizedImdbId,
                "network_exception",
                0);
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
                normalizedImdbId,
                "request_timeout",
                0);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
                normalizedImdbId,
                "invalid_response",
                0);
            return null;
        }
    }

    public async Task<OmdbConnectionTestResult> TestConnectionAsync(string? apiKeyOverride = null, CancellationToken cancellationToken = default)
    {
        var apiKey = apiKeyOverride is null
            ? GetApiKey()
            : apiKeyOverride.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OMDb connection test aborted because no API key is configured.");
            return OmdbConnectionTestResult.Failure(
                "OMDb API key is missing. Enter your key and try again.",
                "missing_api_key");
        }

        var requestUri = BuildRequestUri(ConnectivityTestImdbId, apiKey);

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            var payload = await ReadOmdbPayloadAsync(response, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (TryBuildOmdbFailureResult(payload, out var httpFailure))
                {
                    _logger.LogWarning("OMDb connection test failed with OMDb error {ErrorCode}.", httpFailure.ErrorCode);
                    return httpFailure;
                }

                _logger.LogWarning("OMDb connection test failed with HTTP status code {StatusCode}.", (int)response.StatusCode);
                return OmdbConnectionTestResult.Failure(
                    $"OMDb request failed with HTTP {(int)response.StatusCode}.",
                    "http_error");
            }

            if (payload is null)
            {
                _logger.LogWarning("OMDb connection test failed because OMDb returned an empty response.");
                return OmdbConnectionTestResult.Failure(
                    "OMDb returned an empty response.",
                    "empty_response");
            }

            if (TryBuildOmdbFailureResult(payload, out var omdbFailure))
            {
                _logger.LogWarning("OMDb connection test failed with OMDb error {ErrorCode}.", omdbFailure.ErrorCode);
                return omdbFailure;
            }

            _logger.LogInformation("OMDb connection test completed successfully.");
            return OmdbConnectionTestResult.Success("Connected to OMDb successfully.");
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("OMDb connection test failed because the request could not reach OMDb.");
            return OmdbConnectionTestResult.Failure("Unable to reach OMDb. Check your network connection and try again.", "request_failure");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OMDb connection test failed because the request timed out.");
            return OmdbConnectionTestResult.Failure("The OMDb request timed out. Please try again.", "request_timeout");
        }
        catch (JsonException)
        {
            _logger.LogWarning("OMDb connection test failed because OMDb returned an unexpected response format.");
            return OmdbConnectionTestResult.Failure("OMDb returned an unexpected response format.", "invalid_response");
        }
    }

    private static async Task<OmdbMovieResponse?> ReadOmdbPayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OmdbMovieResponse>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private string BuildRequestUri(string imdbId, string apiKey)
    {
        return $"{OmdbBaseUrl}?apikey={Uri.EscapeDataString(apiKey)}&i={Uri.EscapeDataString(imdbId)}";
    }

    private string GetApiKey()
    {
        return _configurationService.GetCurrent().OmdbApiKey.Trim();
    }

    private static OmdbMovieData? MapPayload(OmdbMovieResponse payload, string requestedImdbId)
    {
        var responseImdbId = NormalizeOmdbValue(payload.ImdbId);
        if (responseImdbId is not null && !string.Equals(responseImdbId, requestedImdbId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new OmdbMovieData
        {
            ImdbId = responseImdbId ?? requestedImdbId,
            Title = NormalizeOmdbValue(payload.Title),
            AwardsText = NormalizeOmdbValue(payload.Awards)
        };
    }

    private static string? NormalizeOmdbValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return string.Equals(normalized, "N/A", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static bool TryBuildOmdbFailureResult(OmdbMovieResponse? payload, out OmdbConnectionTestResult result)
    {
        if (payload is not null && !payload.IsSuccess)
        {
            var message = NormalizeOmdbValue(payload.Error) ?? "OMDb rejected the request.";
            var errorCode = string.Equals(message, "Invalid API key!", StringComparison.OrdinalIgnoreCase)
                ? "invalid_api_key"
                : "omdb_error";
            result = OmdbConnectionTestResult.Failure(message, errorCode);
            return true;
        }

        result = null!;
        return false;
    }

    private void LogLookupFailure(string imdbId, OmdbMovieResponse? payload, HttpStatusCode statusCode, string fallbackReason)
    {
        _logger.LogWarning(
            "OMDb lookup issue. imdb_id={ImdbId}, reason={Reason}, http_status={HttpStatusCode}.",
            imdbId,
            ClassifyFailureReason(payload, statusCode, fallbackReason),
            (int)statusCode);
    }

    private static string ClassifyFailureReason(OmdbMovieResponse? payload, HttpStatusCode statusCode, string fallbackReason)
    {
        var error = NormalizeOmdbValue(payload?.Error);
        if (statusCode == HttpStatusCode.TooManyRequests || (error?.Contains("limit", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return "rate_limit";
        }

        if (statusCode == HttpStatusCode.Unauthorized || string.Equals(error, "Invalid API key!", StringComparison.OrdinalIgnoreCase))
        {
            return "invalid_api_key";
        }

        if (string.Equals(error, "Movie not found!", StringComparison.OrdinalIgnoreCase))
        {
            return "not_found";
        }

        return fallbackReason;
    }
}
