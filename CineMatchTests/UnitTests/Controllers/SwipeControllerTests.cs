using CineMatch.API.Controllers;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CineMatchTests.UnitTests.Controllers
{
    public class SwipeControllerTests : IClassFixture<CustomWebApplicationFactory<SwipeController>>
    {
        private readonly CustomWebApplicationFactory<SwipeController> _factory;
        private readonly HttpClient _client;

        public SwipeControllerTests(CustomWebApplicationFactory<SwipeController> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private async Task<string> RegisterAndLoginUser(string email, string password)
        {
            var registerRequest = new { Email = email, Password = password };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var loginRequest = new { Email = email, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return loginResult!.token;
        }

        private class LoginResponse
        {
            public string token { get; set; } = null!;
            public string userId { get; set; } = null!;
        }

        private class FriendRequest
        {
            public string friendshipId { get; set; } = null!;
            public string requesterEmail { get; set; } = null!;
            public DateTime requestedAt { get; set; }
        }

        private class SessionResponse
        {
            public string sessionId { get; set; } = null!;
            public string? friendId { get; set; }
            public bool isFriendSession { get; set; }
        }

        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task CreateSession_FriendSession_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            // Create friendship
            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);
            var requests = await client2.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            if (requestList != null && requestList.Count > 0)
            {
                var friendshipId = requestList[0].friendshipId;
                await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);
            }

            // Create session request
            var sessionRequest = new
            {
                IsFriendSession = true,
                FriendEmail = "user2@example.com"
            };

            // Act
            var response = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SessionResponse>();
            Assert.NotNull(result);
            Assert.NotNull(result.sessionId);
        }

        [Fact]
        public async Task CreateSession_RegularSession_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionRequest = new
            {
                IsFriendSession = false,
                FriendEmail = (string)null
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SessionResponse>();
            Assert.NotNull(result);
            Assert.NotNull(result.sessionId);
        }

        [Fact]
        public async Task CreateSession_Unauthorized_Returns401()
        {
            // Arrange
            var client = _factory.CreateClient();
            var sessionRequest = new { IsFriendSession = false };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_FriendSession_NoFriendship_ReturnsBadRequest()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionRequest = new
            {
                IsFriendSession = true,
                FriendEmail = "nonfriend@example.com"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create session first
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            // Swipe request
            var swipeRequest = new MovieSwipe
            {
                SessionId = sessionId,
                MovieId = 123,
                IsLiked = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_InvalidSessionId_ReturnsBadRequest()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var swipeRequest = new
            {
                SessionId = "invalid-session-id",
                MovieId = 123,
                IsLiked = true
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_Unauthorized_Returns401()
        {
            // Arrange
            var client = _factory.CreateClient();
            var swipeRequest = new { SessionId = "session1", MovieId = 123, IsLiked = true };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_DuplicateMovieInSession_ReturnsOk()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create session
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            // First swipe
            var swipeRequest1 = new { SessionId = sessionId, MovieId = 123, IsLiked = true };
            await client.PostAsJsonAsync("/api/Swipe", swipeRequest1);

            // Second swipe on same movie (should be allowed, just updates)
            var swipeRequest2 = new { SessionId = sessionId, MovieId = 123, IsLiked = false };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest2);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_SessionBelongsToDifferentUser_ReturnsBadRequest()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            // User1 creates session
            var client1 = CreateAuthenticatedClient(token1);
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            // User2 tries to swipe in User1's session
            var client2 = CreateAuthenticatedClient(token2);
            var swipeRequest = new { SessionId = sessionId, MovieId = 123, IsLiked = true };

            // Act
            var response = await client2.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Swipe_VariousLikeValues_Accepted(bool isLiked)
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create session
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest = new { SessionId = sessionId, MovieId = 123, IsLiked = isLiked };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(999999)]
        public async Task Swipe_VariousMovieIds_Accepted(int movieId)
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create session
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest = new { SessionId = sessionId, MovieId = movieId, IsLiked = true };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_MultipleSessionsForUser_Accepted()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create first session
            var sessionRequest1 = new { IsFriendSession = false };
            var response1 = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest1);
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

            // Create second session
            var sessionRequest2 = new { IsFriendSession = false };
            var response2 = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest2);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }

        [Fact]
        public async Task Swipe_AfterSessionMatched_ReturnsOk()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Create session
            var sessionRequest = new { IsFriendSession = false };
            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            // Add many swipes to potentially trigger a match
            for (int i = 1; i <= 6; i++)
            {
                var swipeRequest = new { SessionId = sessionId, MovieId = i, IsLiked = true };
                await client.PostAsJsonAsync("/api/Swipe", swipeRequest);
            }

            // Additional swipe after potential match
            var additionalSwipeRequest = new { SessionId = sessionId, MovieId = 7, IsLiked = true };

            // Act
            var response = await client.PostAsJsonAsync("/api/Swipe", additionalSwipeRequest);

            // Assert - Should still work even if matched
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_FriendSession_UserNotRequester_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            // Create friendship where user1 is not the requester
            var client2 = CreateAuthenticatedClient(token2);
            await client2.PostAsync("/api/Friends/request/user1@example.com", null);

            var client1 = CreateAuthenticatedClient(token1);
            var requests = await client1.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            if (requestList != null && requestList.Count > 0)
            {
                var friendshipId = requestList[0].friendshipId;
                await client1.PostAsync($"/api/Friends/accept/{friendshipId}", null);
            }

            // Now user1 tries to create friend session with user2 (should work since they're friends)
            var sessionRequest = new
            {
                IsFriendSession = true,
                FriendEmail = "user2@example.com"
            };

            // Act
            var response = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}