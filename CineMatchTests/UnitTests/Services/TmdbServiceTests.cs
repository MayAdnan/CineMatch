using System.Net;
using System.Text.Json;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace CineMatchTests.UnitTests.Services
{
    public class TmdbServiceTests
    {
        private Mock<IConfiguration> _configMock;

        public TmdbServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["TMDB:ApiKey"]).Returns("test_api_key");
        }

        private HttpClient CreateHttpClient(string jsonResponse, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handler = new TestHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(jsonResponse)
            }));
            return new HttpClient(handler);
        }

        private TmdbService CreateService(HttpClient? client = null, IConfiguration? config = null)
        {
            return new TmdbService(client ?? new HttpClient(), config ?? _configMock.Object);
        }

        private string SerializeResponse(IEnumerable<Movie>? movies)
        {
            return JsonSerializer.Serialize(new TmdbResponse { Results = movies?.ToList() });
        }

        #region Success Tests

        [Fact]
        public async Task GetMoviesAsync_ReturnsMovies_WhenApiCallSucceeds()
        {
            var expectedMovies = new List<Movie>
            {
                new Movie { Id = 1, Title = "Movie 1" },
                new Movie { Id = 2, Title = "Movie 2" }
            };
            var client = CreateHttpClient(SerializeResponse(expectedMovies));
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            var movies = result.ToList();
            Assert.Equal(2, movies.Count);
            Assert.Equal("Movie 1", movies[0].Title);
            Assert.Equal("Movie 2", movies[1].Title);
        }

        [Fact]
        public async Task GetMoviesAsync_ReturnsEmpty_WhenApiReturnsNull()
        {
            var client = CreateHttpClient(SerializeResponse(null));
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMoviesAsync_ReturnsEmpty_WhenApiKeyMissingOrEmpty()
        {
            var client = CreateHttpClient(SerializeResponse(new List<Movie> { new Movie { Id = 1 } }));
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["TMDB:ApiKey"]).Returns<string?>(null!);
            var service = CreateService(client, config.Object);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("28", 90)]
        [InlineData("35", 150)]
        [InlineData("18", 200)]
        public async Task GetMoviesAsync_CorrectUrlParameters(string genre, int maxRuntime)
        {
            string actualUrl = null!;
            var handler = new TestHttpMessageHandler(req =>
            {
                actualUrl = req.RequestUri.ToString();
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(SerializeResponse(new List<Movie>()))
                });
            });
            var client = new HttpClient(handler);
            var service = CreateService(client);

            await service.GetMoviesAsync(genre, maxRuntime);

            Assert.Contains($"with_genres={genre}", actualUrl);
            Assert.Contains($"with_runtime.lte={maxRuntime}", actualUrl);
            Assert.Contains("language=en-US", actualUrl);
            Assert.Contains("api_key=test_api_key", actualUrl);
        }

        #endregion

        #region Error & Exception Tests

        [Fact]
        public async Task GetMoviesAsync_HandlesNetworkErrorGracefully()
        {
            var handler = new TestHttpMessageHandler(_ => throw new HttpRequestException("Network error"));
            var client = new HttpClient(handler);
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            // Service should handle network errors gracefully by returning empty collection
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMoviesAsync_ThrowsJsonException_OnInvalidJson()
        {
            var client = CreateHttpClient("{ invalid json }");
            var service = CreateService(client);

            await Assert.ThrowsAsync<JsonException>(() => service.GetMoviesAsync("28", 120));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData((HttpStatusCode)429)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task GetMoviesAsync_ReturnsEmpty_OnHttpError(HttpStatusCode statusCode)
        {
            var client = CreateHttpClient("Error", statusCode);
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10000)]
        [InlineData(int.MaxValue)]
        public async Task GetMoviesAsync_HandlesEdgeRuntimeParameters(int runtime)
        {
            var client = CreateHttpClient(SerializeResponse(null));
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", runtime);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region Data Integrity Tests

        [Fact]
        public async Task GetMoviesAsync_HandlesMoviesWithMissingOrNullFields()
        {
            var jsonResponse = @"{
                ""results"": [
                    { ""id"": 1, ""title"": null },
                    { ""id"": 2, ""overview"": ""Overview 2"" },
                    { ""id"": 3, ""genre_ids"": [28] }
                ]
            }";
            var client = CreateHttpClient(jsonResponse);
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public async Task GetMoviesAsync_HandlesLargeDataset()
        {
            var movies = Enumerable.Range(1, 1000)
                .Select(i => new Movie { Id = i, Title = $"Movie {i}", GenreIds = new List<int> { 28 } });
            var client = CreateHttpClient(SerializeResponse(movies));
            var service = CreateService(client);

            var result = await service.GetMoviesAsync("28", 120);

            Assert.NotNull(result);
            Assert.Equal(1000, result.Count());
        }

        #endregion
    }
}
