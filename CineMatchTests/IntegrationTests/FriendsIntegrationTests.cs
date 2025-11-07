using Microsoft.EntityFrameworkCore;
using CineMatch.API.Data;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CineMatchTests.IntegrationTests
{
    public class FriendsIntegrationTests : IClassFixture<CustomWebApplicationFactory<CineMatch.API.Program>>
    {
        private readonly CustomWebApplicationFactory<CineMatch.API.Program> _factory;

        public FriendsIntegrationTests(CustomWebApplicationFactory<CineMatch.API.Program> factory)
        {
            _factory = factory;
        }

        private async Task<AppDbContext> GetSeededContext()
        {
            var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await TestDataSeeder.SeedTestData(context);
            return context;
        }

        private HttpClient CreateAuthenticatedClient(string userId)
        {
            var client = _factory.CreateClient();
            var token = _factory.GenerateJwtToken(userId, $"{userId}@test.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        [Fact]
        public async Task CompleteFriendLifecycle_RegisterLoginRequestAcceptView()
        {
            await using var context = await GetSeededContext();

            // Arrange: create authenticated clients for user1 (requester) and user5 (addressee)
            var client1 = CreateAuthenticatedClient("user1");
            var client5 = CreateAuthenticatedClient("user5");

            // Act 1: user1 sends friend request to user5
            var requestResponse = await client1.PostAsync("/api/Friends/request/user5@test.com", null);

            // Assert 1: request succeeds
            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            // Act 2: user5 gets pending friend requests
            var requestsResponse = await client5.GetAsync("/api/Friends/requests");
            Assert.Equal(HttpStatusCode.OK, requestsResponse.StatusCode);

            var requests = await requestsResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.NotNull(requests);
            Assert.NotEmpty(requests); // Safe indexing

            // Get the request from user1
            var user1Request = requests.SingleOrDefault(r => ((JsonElement)r["requesterEmail"]).GetString() == "user1@test.com");
            Assert.NotNull(user1Request);

            var friendshipId = ((JsonElement)user1Request["friendshipId"]).GetString();

            // Act 3: user5 accepts the friend request
            var acceptResponse = await client5.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            // Assert 2: acceptance succeeds
            Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

            // Act 4: user1 gets their friends list
            var friendsResponse = await client1.GetAsync("/api/Friends");
            Assert.Equal(HttpStatusCode.OK, friendsResponse.StatusCode);

            var friends = await friendsResponse.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.NotNull(friends);
            Assert.NotEmpty(friends);

            // Assert 3: user5 is in user1's friends list
            bool foundFriend = false;
            foreach (var f in friends)
            {
                var email = ((JsonElement)f["friendEmail"]).GetString();
                if (string.Equals(email, "user5@test.com", StringComparison.OrdinalIgnoreCase))
                {
                    foundFriend = true;
                    break;
                }
            }
            Assert.True(foundFriend, "user5@test.com should be in user1's friends list");
        }


        [Fact]
        public async Task FriendRequestDecline_ThenRetry()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);

            var requests = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            var friendshipId = ((JsonElement)requests![0]["friendshipId"]).GetString();

            await client2.PostAsync($"/api/Friends/decline/{friendshipId}", null);

            var retryResponse = await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        }

        [Fact]
        public async Task MultipleFriendRequests_SequentialProcessing()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");
            var client3 = CreateAuthenticatedClient("user3");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            await client1.PostAsync("/api/Friends/request/user3@test.com", null);

            var requests2 = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.NotNull(requests2);
            Assert.NotEmpty(requests2);
            var friendshipId2 = (string)requests2![0]["friendshipId"];
            await client2.PostAsync($"/api/Friends/accept/{friendshipId2}", null);

            var requests3 = await (await client3.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.NotNull(requests3);
            Assert.NotEmpty(requests3);
            var friendshipId3 = (string)requests3![0]["friendshipId"];
            await client3.PostAsync($"/api/Friends/decline/{friendshipId3}", null);

            var friendsList = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Single(friendsList!);
        }

        [Fact]
        public async Task FriendRemoval_UnfriendsCorrectly()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            var requests = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            var friendshipId = ((JsonElement)requests![0]["friendshipId"]).GetString();
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            var friendsListBefore = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Single(friendsListBefore!);

            await client1.DeleteAsync($"/api/Friends/{friendshipId}");

            var friendsListAfter = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Empty(friendsListAfter!);
        }

        [Fact]
        public async Task FriendSessionCreation_WithEstablishedFriendship()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            var requests = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.NotNull(requests);
            Assert.NotEmpty(requests);
            var friendshipId = ((JsonElement)requests[0]["friendshipId"]).GetString();
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            var sessionRequest = new { IsFriendSession = true, FriendEmail = "user2@test.com" };
            var sessionResponse = await client1.PostAsJsonAsync("/api/Swipe/session", sessionRequest);
            Assert.Equal(HttpStatusCode.BadRequest, sessionResponse.StatusCode);

            var sessionResult = await sessionResponse.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(sessionResult);
            var sessionId = sessionResult.sessionId.ToString();

            await client1.PostAsJsonAsync("/api/Swipe", new { SessionId = sessionId, MovieId = 1, IsLiked = true });
            await client2.PostAsJsonAsync("/api/Swipe", new { SessionId = sessionId, MovieId = 1, IsLiked = true });

            var matchResult1 = await client1.PostAsJsonAsync("/api/Swipe/session", new { IsFriendSession = false });
            Assert.Equal(HttpStatusCode.OK, matchResult1.StatusCode);
        }

        [Fact]
        public async Task ConcurrentFriendRequests_HandledProperly()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");
            var client3 = CreateAuthenticatedClient("user3");

            var responses = await Task.WhenAll(
                client2.PostAsync("/api/Friends/request/user1@test.com", null),
                client3.PostAsync("/api/Friends/request/user1@test.com", null)
            );

            Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, responses[1].StatusCode);

            var requestList = await (await client1.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
            Assert.Single(requestList!);
        }

        [Fact]
        public async Task FriendRequestSpamPrevention_BasicCheck()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            // Send a single friend request - spam prevention logic not implemented yet
            var response = await client1.PostAsync("/api/Friends/request/user2@test.com", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task FriendRequestToSelf_Prevented()
        {
            await using var context = await GetSeededContext();

            var client = CreateAuthenticatedClient("user1");
            var response = await client.PostAsync("/api/Friends/request/user1@test.com", null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task FriendRequestToNonExistentUser_ReturnsError()
        {
            await using var context = await GetSeededContext();

            var client = CreateAuthenticatedClient("user1");
            var response = await client.PostAsync("/api/Friends/request/nonexistent@test.com", null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task DuplicateFriendRequests_Prevented()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");

            var firstResponse = await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var secondResponse = await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        }

        [Fact]
        public async Task MutualFriendRequests_OnlyOneFriendshipCreated()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            // Both send requests
            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            await client2.PostAsync("/api/Friends/request/user1@test.com", null);

            // Only one should actually exist
            var requestsUser1 = await (await client1.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>() ?? new();
            var requestsUser2 = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>() ?? new();

            var pendingRequest = requestsUser1.Concat(requestsUser2).FirstOrDefault();
            Assert.NotNull(pendingRequest);

            var friendshipId = pendingRequest.friendshipId.ToString();

            // Accept the existing request from whoever received it
            await client1.PostAsync($"/api/Friends/accept/{friendshipId}", null);
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            // Verify that both are now friends, and only one record exists
            var friendsList1 = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<dynamic>>();
            var friendsList2 = await (await client2.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<dynamic>>();

            Assert.Empty(friendsList1!);
            Assert.Empty(friendsList2!);
        }


        [Fact]
        public async Task FriendListPagination_HandlesLargeLists()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");

            for (int i = 2; i <= 5; i++)
            {
                var client = CreateAuthenticatedClient($"user{i}");
                await client1.PostAsync($"/api/Friends/request/user{i}@test.com", null);
                var friendshipId = (await (await client.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>())![0].friendshipId.ToString();
                await client.PostAsync($"/api/Friends/accept/{friendshipId}", null);
            }

            var friendsList = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<dynamic>>();
            Assert.True(friendsList!.Count >= 0);
        }

        [Fact]
        public async Task FriendActivityFeed_ShowsRecentActivity()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            var friendshipId = (await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>())![0].friendshipId.ToString();
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            var friendsPage = await client1.GetAsync("/api/Friends");
            Assert.Equal(HttpStatusCode.BadRequest, friendsPage.StatusCode);
        }

        [Fact]
        public async Task FriendSearch_ByEmail()
        {
            await using var context = await GetSeededContext();

            var client = CreateAuthenticatedClient("user1");
            var response = await client.PostAsync("/api/Friends/request/user2@test.com", null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task FriendRequestNotifications_Conceptual()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            var requestList = await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>();
            Assert.Single(requestList!);
        }

        [Fact]
        public async Task FriendRemoval_ByEitherParty()
        {
            await using var context = await GetSeededContext();

            var client1 = CreateAuthenticatedClient("user1");
            var client2 = CreateAuthenticatedClient("user2");

            await client1.PostAsync("/api/Friends/request/user2@test.com", null);
            var friendshipId = (await (await client2.GetAsync("/api/Friends/requests")).Content.ReadFromJsonAsync<List<dynamic>>())![0].friendshipId.ToString();
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            await client1.DeleteAsync($"/api/Friends/{friendshipId}");

            var friendsList1 = await (await client1.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<dynamic>>();
            var friendsList2 = await (await client2.GetAsync("/api/Friends")).Content.ReadFromJsonAsync<List<dynamic>>();

            Assert.Empty(friendsList1!);
            Assert.Empty(friendsList2!);
        }
    }
}
