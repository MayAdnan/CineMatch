using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using CineMatchTests.TestHelpers;
using Xunit;

namespace CineMatchTests.IntegrationTests
{
    public class SwipeIntegrationTests : IClassFixture<CustomWebApplicationFactory<CineMatch.API.Program>>
    {
        private readonly CustomWebApplicationFactory<CineMatch.API.Program> _factory;
        private readonly HttpClient _client;

        public SwipeIntegrationTests(CustomWebApplicationFactory<CineMatch.API.Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private async Task<string> RegisterAndLoginUser(string email, string password)
        {
            var registerRequest = new { Email = email, Password = password };
            var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            registerResponse.EnsureSuccessStatusCode();

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

        private class SessionResponse
        {
            public string sessionId { get; set; } = null!;
            public string? friendId { get; set; }
            public bool isFriendSession { get; set; }
        }

        private class FriendRequest
        {
            public string friendshipId { get; set; } = null!;
            public string requesterEmail { get; set; } = null!;
            public DateTime requestedAt { get; set; }
        }

        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task CreateRegularSession_AndSwipe_FullWorkflow()
        {
            var token = await RegisterAndLoginUser("swipeuser@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResponse = await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false });
            Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);

            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            for (int i = 1; i <= 6; i++)
            {
                var swipeRequest = new { SessionId = sessionId, MovieId = i, IsLiked = i % 2 == 0 };
                var swipeResponse = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);
                Assert.Equal(HttpStatusCode.OK, swipeResponse.StatusCode);
            }
        }

        [Fact]
        public async Task CreateSession_MultipleSessionsForSameUser_Accepted()
        {
            var token = await RegisterAndLoginUser("multiplesessions@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var response1 = await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false });
            var response2 = await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false });

            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }

        [Fact]
        public async Task Swipe_MultipleTimesOnSameMovie_UpdatesPreference()
        {
            var token = await RegisterAndLoginUser("multiple@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest1 = new { SessionId = sessionId, MovieId = 1, IsLiked = true };
            var swipeRequest2 = new { SessionId = sessionId, MovieId = 1, IsLiked = false };

            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest1)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest2)).StatusCode);
        }

        [Fact]
        public async Task Swipe_InvalidMovieId_ReturnsOk()
        {
            var token = await RegisterAndLoginUser("invalidmovie@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest = new { SessionId = sessionId, MovieId = -1, IsLiked = true };
            var response = await client.PostAsJsonAsync("/api/Swipe", swipeRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_ZeroMovieId_Accepted()
        {
            var token = await RegisterAndLoginUser("zeromovie@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest = new { SessionId = sessionId, MovieId = 0, IsLiked = true };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task Swipe_WithLargeMovieId_Accepted()
        {
            var token = await RegisterAndLoginUser("largemovie@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipeRequest = new { SessionId = sessionId, MovieId = int.MaxValue, IsLiked = true };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task Swipe_BatchSwipes_Work()
        {
            var token = await RegisterAndLoginUser("batch@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 1; i <= 10; i++)
                tasks.Add(client.PostAsJsonAsync("/api/Swipe", new { SessionId = sessionId, MovieId = i, IsLiked = true }));

            var responses = await Task.WhenAll(tasks);
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        }

        [Fact]
        public async Task Swipe_AfterSessionTimeout_StillWorks()
        {
            var token = await RegisterAndLoginUser("timeout@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            await Task.Delay(100);

            var swipeRequest = new { SessionId = sessionId, MovieId = 1, IsLiked = true };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task CreateSession_RegularSession_NoFriendEmail_Accepted()
        {
            var token = await RegisterAndLoginUser("nofriendemail@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var response = await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false, FriendEmail = (string)null });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateFriendSession_AndSwipe_FullWorkflow()
        {
            var token1 = await RegisterAndLoginUser("friend1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("friend2@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/friend2@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);
            var requestList = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<FriendRequest>>();
            var friendshipId = requestList![0].friendshipId;
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "friend2@example.com" };
            var sessionResponse = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var swipes = new[]
            {
                (client: client1, movieId: 1, liked: true),
                (client: client1, movieId: 2, liked: true),
                (client: client1, movieId: 3, liked: false),
                (client: client2, movieId: 1, liked: true),
                (client: client2, movieId: 2, liked: true),
                (client: client2, movieId: 4, liked: true)
            };

            foreach (var (client, movieId, liked) in swipes)
            {
                var swipeRequest = new { SessionId = sessionId, MovieId = movieId, IsLiked = liked };
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
            }
        }

        [Fact]
        public async Task CreateSession_FriendSession_NoFriendship_ReturnsBadRequest()
        {
            var token = await RegisterAndLoginUser("nofriend@example.com", "password123");
            await RegisterAndLoginUser("stranger@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "stranger@example.com" };
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_FriendSession_WithSelf_ReturnsBadRequest()
        {
            var token = await RegisterAndLoginUser("self@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "self@example.com" };
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_FriendSession_CaseInsensitiveEmail_Accepted()
        {
            var token1 = await RegisterAndLoginUser("case@example.com", "password123");
            var token2 = await RegisterAndLoginUser("case2@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/case2@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);
            var requestList = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<FriendRequest>>();
            var friendshipId = requestList![0].friendshipId;
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "case2@example.com" };
            var response = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateSession_FriendSession_NonExistentFriend_ReturnsBadRequest()
        {
            var token = await RegisterAndLoginUser("existent@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "nonexistent@example.com" };
            var response = await client.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_WithoutSession_ReturnsBadRequest()
        {
            var token = await RegisterAndLoginUser("nosession@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var swipeRequest = new { SessionId = "nonexistent", MovieId = 1, IsLiked = true };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task Swipe_InWrongSession_ReturnsBadRequest()
        {
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            var sessionResult = await (await client1.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId = sessionResult!.sessionId;

            var client2 = CreateAuthenticatedClient(token2);
            var swipeRequest = new { SessionId = sessionId, MovieId = 1, IsLiked = true };
            Assert.Equal(HttpStatusCode.BadRequest, (await client2.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task Swipe_Unauthenticated_Returns401()
        {
            var client = _factory.CreateClient();
            var swipeRequest = new { SessionId = "session1", MovieId = 1, IsLiked = true };
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }

        [Fact]
        public async Task CreateSession_Unauthenticated_Returns401()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Swipe_InOldSession_AfterNewSessionCreated_Works()
        {
            var token = await RegisterAndLoginUser("oldsession@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            var sessionResult1 = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId1 = sessionResult1!.sessionId;

            var sessionResult2 = await (await client.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false }))
                .Content.ReadFromJsonAsync<SessionResponse>();
            var sessionId2 = sessionResult2!.sessionId;

            var swipeRequest = new { SessionId = sessionId1, MovieId = 1, IsLiked = true };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Swipe", swipeRequest)).StatusCode);
        }
    }
}
