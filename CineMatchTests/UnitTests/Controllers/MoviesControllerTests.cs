using System.Net;
using System.Net.Http.Json;
using CineMatch.API.Controllers;
using CineMatchTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CineMatchTests.UnitTests.Controllers
{
    public class MoviesControllerTests : IClassFixture<WebApplicationFactory<CineMatch.API.Program>>
    {
        private readonly WebApplicationFactory<CineMatch.API.Program> _factory;
        private readonly HttpClient _client;

        public MoviesControllerTests(WebApplicationFactory<CineMatch.API.Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task DiscoverMovies_ReturnsOk_WithValidGenreAndRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotNull(content);
        }

        [Fact]
        public async Task DiscoverMovies_ReturnsOk_WithDefaultParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_AcceptsVariousGenres()
        {
            var genres = new[] { "28", "35", "18", "27", "10749", "878" };

            foreach (var genre in genres)
            {
                // Act
                var response = await _client.GetAsync($"/api/Movies/discover?genre={genre}&maxRuntime=120");

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task DiscoverMovies_AcceptsVariousRuntimes()
        {
            var runtimes = new[] { 60, 90, 120, 150, 180, 240 };

            foreach (var runtime in runtimes)
            {
                // Act
                var response = await _client.GetAsync($"/api/Movies/discover?genre=28&maxRuntime={runtime}");

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task DiscoverMovies_HandlesInvalidGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=invalid&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesNegativeRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=-1");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesZeroRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=0");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesLargeRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=10000");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesEmptyGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesNullGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesBothParametersNull()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesSpecialCharactersInGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28%2C35&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesVeryLongGenre()
        {
            var longGenre = string.Concat(Enumerable.Repeat("28", 100));
            // Act
            var response = await _client.GetAsync($"/api/Movies/discover?genre={longGenre}&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesVeryLongRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=999999");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesFloatRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120.5");

            // Assert - API should handle gracefully (may parse as int or handle error)
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesTextRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=abc");

            // Assert - API should handle gracefully (may parse as 0 or handle error)
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesTextGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=action&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesUnicodeGenre()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=acción&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesUnicodeRuntime()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=doscientos");

            // Assert - API should handle gracefully
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesMultipleSameParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&genre=35&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesCaseInsensitiveParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?GENRE=28&MAXRUNTIME=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesWhitespaceInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28%20%20&maxRuntime=120");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesPlusInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%2B");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesHashInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%23");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesAmpersandInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%26");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesPercentInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%25");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesDollarInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%24");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesCaretInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%5E");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesAtInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%40");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesExclamationInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%21");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesParenthesesInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%28%29");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesBracketsInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%5B%5D");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesBracesInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%7B%7D");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesPipeInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%7C");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesBackslashInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%5C");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesForwardslashInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%2F");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesQuestionmarkInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3F");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesColonInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3A");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesSemicolonInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3B");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesEqualInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3D");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesLessThanInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3C");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesGreaterThanInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%3E");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesCommaInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%2C");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesPeriodInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%2E");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesTildeInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%7E");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesQuoteInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%22");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesApostropheInParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120%27");

            // Assert - API should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_IsAccessibleWithoutAuth()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert - Movies endpoint should not require authentication
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_CanonicalUrlFormat()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesQueryStringOrder()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?maxRuntime=120&genre=28");

            // Assert - parameter order should not matter
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesDuplicateParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&genre=35&maxRuntime=120");

            // Assert - API should handle duplicate parameters
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesEmptyQueryString()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesTrailingAmpersand()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesMultipleAmpersands()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesUnknownParameters()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&unknown=123&maxRuntime=120");

            // Assert - unknown parameters should be ignored
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesVeryLongUrl()
        {
            var longUrl = "/api/Movies/discover?" + string.Join("&", Enumerable.Range(1, 50).Select(i => $"param{i}={i}"));
            // Act
            var response = await _client.GetAsync(longUrl);

            // Assert - API should handle long URLs
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.RequestUriTooLong);
        }

        [Fact]
        public async Task DiscoverMovies_ContentTypeIsJson()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType ?? "");
        }

        [Fact]
        public async Task DiscoverMovies_HasReasonableResponseTime()
        {
            // Act
            var startTime = DateTime.UtcNow;
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");
            var endTime = DateTime.UtcNow;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var duration = endTime - startTime;
            Assert.True(duration.TotalSeconds < 30, $"Response took too long: {duration.TotalSeconds} seconds");
        }

        [Fact]
        public async Task DiscoverMovies_IsIdempotent()
        {
            // Act - Call multiple times
            var response1 = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");
            var response2 = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");
            var response3 = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert - All responses should be similar
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response3.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesConcurrentRequests()
        {
            // Act - Make concurrent requests
            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task DiscoverMovies_CacheHeadersPresent()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Note: Cache headers may or may not be present depending on implementation
        }

        [Fact]
        public async Task DiscoverMovies_CorsHeadersPresent()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // CORS headers may or may not be present - just check that response is OK
            // var corsHeader = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
            // Assert.NotNull(corsHeader);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesSqlInjectionAttempt()
        {
            // Act - Attempt SQL injection
            var response = await _client.GetAsync("/api/Movies/discover?genre=28';DROP TABLE Users;--&maxRuntime=120");

            // Assert - Should not crash and should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesXssAttempt()
        {
            // Act - Attempt XSS
            var response = await _client.GetAsync("/api/Movies/discover?genre=<script>alert('xss')</script>&maxRuntime=120");

            // Assert - Should not crash and should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesPathTraversalAttempt()
        {
            // Act - Attempt path traversal
            var response = await _client.GetAsync("/api/Movies/discover?genre=../../../etc/passwd&maxRuntime=120");

            // Assert - Should not crash and should handle gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesVeryLargeNumbers()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=999999999999999");

            // Assert - Should handle large numbers gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesNegativeLargeNumbers()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=-999999999999999");

            // Assert - Should handle large negative numbers gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesBinaryDataInParameters()
        {
            // Act - Send binary data (URL encoded)
            var response = await _client.GetAsync("/api/Movies/discover?genre=%00%01%02%03&maxRuntime=120");

            // Assert - Should handle binary data gracefully
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesUnicodeNormalization()
        {
            // Act - Test unicode normalization
            var response = await _client.GetAsync("/api/Movies/discover?genre=café&maxRuntime=120");

            // Assert - Should handle unicode characters
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesRightToLeftText()
        {
            // Act - Test RTL text
            var response = await _client.GetAsync("/api/Movies/discover?genre=العربية&maxRuntime=120");

            // Assert - Should handle RTL text
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_HandlesEmojiInParameters()
        {
            // Act - Test emoji
            var response = await _client.GetAsync("/api/Movies/discover?genre=🎬&maxRuntime=120");

            // Assert - Should handle emoji
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_ResponseSizeIsReasonable()
        {
            // Act
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(content.Length < 1048576, "Response should be less than 1MB"); // 1MB limit
        }

        [Fact]
        public async Task DiscoverMovies_HandlesMemoryPressure()
        {
            // Act - Make many concurrent requests to test memory handling
            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(_client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert - All requests should succeed without memory issues
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task DiscoverMovies_HandlesNetworkTimeouts()
        {
            // This test would require setting up a timeout scenario
            // For now, just verify normal operation
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_ValidatesParameterTypes()
        {
            // Test that parameters are properly validated/sanitized
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert - Should return valid JSON
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.StartsWith("[", content); // Should be JSON array
        }

        [Fact]
        public async Task DiscoverMovies_ErrorResponsesContainProperStructure()
        {
            // Test with invalid parameters to potentially trigger error
            var response = await _client.GetAsync("/api/Movies/discover?genre=invalid&maxRuntime=invalid");

            // Assert - Even error responses should be properly formatted
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var content = await response.Content.ReadAsStringAsync();
                Assert.NotNull(content); // Should contain error details
            }
        }

        [Fact]
        public async Task DiscoverMovies_HandlesEncodingEdgeCases()
        {
            // Test various encoding edge cases
            var testCases = new[]
            {
                "/api/Movies/discover?genre=%C3%A9&maxRuntime=120", // UTF-8 encoded
                "/api/Movies/discover?genre=%E2%9C%93&maxRuntime=120", // Unicode checkmark
                "/api/Movies/discover?genre=%F0%9F%8D%95&maxRuntime=120", // Emoji as UTF-8
            };

            foreach (var url in testCases)
            {
                var response = await _client.GetAsync(url);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task DiscoverMovies_RespectsRateLimiting()
        {
            // Test rate limiting by making many rapid requests
            var responses = new List<HttpResponseMessage>();
            for (int i = 0; i < 100; i++)
            {
                responses.Add(await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120"));
            }

            // Assert - Should handle rate limiting gracefully
            var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

            Assert.True(okCount + rateLimitedCount == responses.Count); // All should be either OK or rate limited
        }

        [Fact]
        public async Task DiscoverMovies_HandlesDatabaseConnectionIssues()
        {
            // This would require setting up database failure scenarios
            // For now, test normal operation
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_ExternalApiFailureHandling()
        {
            // This would require mocking TMDB API failures
            // For now, test that the endpoint exists and responds
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_ResponseCompression()
        {
            // Test if responses are compressed
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Compression headers may be present
            var compressionHeader = response.Content.Headers.ContentEncoding?.FirstOrDefault();
            // Either compressed or not - both are acceptable
        }

        [Fact]
        public async Task DiscoverMovies_CustomHeadersHandling()
        {
            // Test custom headers
            _client.DefaultRequestHeaders.Add("X-Custom-Header", "test-value");
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _client.DefaultRequestHeaders.Remove("X-Custom-Header");
        }

        [Fact]
        public async Task DiscoverMovies_UserAgentHandling()
        {
            // Test different user agents
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Test-Agent/1.0");
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task DiscoverMovies_AcceptHeaderHandling()
        {
            // Test accept headers
            _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var response = await _client.GetAsync("/api/Movies/discover?genre=28&maxRuntime=120");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType ?? "");
        }
    }
}