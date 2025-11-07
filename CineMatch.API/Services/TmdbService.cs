using System.Net.Http;
using System.Text.Json;
using CineMatch.API.Models;

public class TmdbService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public TmdbService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<Movie>> GetMoviesAsync(string genre, int maxRuntime)
    {
        var apiKey = _config["TMDB:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("TMDB API key is missing in configuration.");
            return new List<Movie>();
        }

        var url = $"https://api.themoviedb.org/3/discover/movie?api_key={apiKey}&with_genres={genre}&with_runtime.lte={maxRuntime}&language=en-US";

        int maxRetries = 3;
        int delayMilliseconds = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Set a timeout for the request
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                var response = await _httpClient.GetAsync(url, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    var tmdbResponse = JsonSerializer.Deserialize<TmdbResponse>(json, _jsonOptions);

                    return tmdbResponse?.Results ?? new List<Movie>();
                }

                Console.WriteLine($"TMDB request failed (attempt {attempt}): {response.StatusCode}");

                // Retry only for server-side errors (5xx)
                if ((int)response.StatusCode < 500 || attempt == maxRetries)
                    break;

            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request exception (attempt {attempt}): {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine($"Request timed out (attempt {attempt}): {ex.Message}");
            }

            // Wait before retrying
            await Task.Delay(delayMilliseconds);
        }

        // All attempts failed
        Console.WriteLine("Failed to get movies from TMDB after multiple attempts.");
        return new List<Movie>();
    }
}
