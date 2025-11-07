using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CineMatch.API.Controllers;
using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CineMatchTests.UnitTests.Controllers
{
    public class FriendsControllerTests : IClassFixture<CustomWebApplicationFactory<FriendsController>>
    {
        private readonly CustomWebApplicationFactory<FriendsController> _factory;
        private readonly HttpClient _client;

        public FriendsControllerTests(CustomWebApplicationFactory<FriendsController> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        private async Task CleanDatabase()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Friends.ExecuteDeleteAsync();
            await context.Users.ExecuteDeleteAsync();
        }

        private async Task<string> RegisterAndLoginUser(string email, string password)
        {
            // Ensure database is clean for this user
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (existingUser != null)
            {
                // Remove any existing friendships for this user
                var friendships = await context.Friends
                    .Where(f => f.RequesterId == existingUser.Id || f.AddresseeId == existingUser.Id)
                    .ToListAsync();
                context.Friends.RemoveRange(friendships);
                context.Users.Remove(existingUser);
                await context.SaveChangesAsync();
            }

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
        public async Task GetFriends_ReturnsEmptyList_WhenNoFriends()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Act
            var response = await client.GetAsync("/api/Friends");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var friends = await response.Content.ReadFromJsonAsync<List<dynamic>>();
            Assert.NotNull(friends);
            Assert.Empty(friends);
        }

        [Fact]
        public async Task RequestFriend_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            await RegisterAndLoginUser("user2@example.com", "password123");
            var client = CreateAuthenticatedClient(token1);

            // Act
            var response = await client.PostAsync("/api/Friends/request/user2@example.com", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task RequestFriend_SelfRequest_ReturnsBadRequest()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Act
            var response = await client.PostAsync("/api/Friends/request/user1@example.com", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RequestFriend_NonExistentUser_ReturnsBadRequest()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Act
            var response = await client.PostAsync("/api/Friends/request/nonexistent@example.com", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RequestFriend_DuplicateRequest_ReturnsBadRequest()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            await RegisterAndLoginUser("user2@example.com", "password123");
            var client = CreateAuthenticatedClient(token1);

            // First request
            await client.PostAsync("/api/Friends/request/user2@example.com", null);

            // Second request (duplicate)
            var response = await client.PostAsync("/api/Friends/request/user2@example.com", null);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AcceptFriend_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            // User1 requests friendship with User2
            var client1 = CreateAuthenticatedClient(token1);
            var requestResponse = await client1.PostAsync("/api/Friends/request/user2@example.com", null);
            Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

            // Get the friendship ID from User2's pending requests
            var client2 = CreateAuthenticatedClient(token2);
            var requests = await client2.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            Assert.NotNull(requestList);
            Assert.Single(requestList);
            var friendshipId = requestList[0].friendshipId;

            // User2 accepts the request
            var response = await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AcceptFriend_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var token = await RegisterAndLoginUser("user1@example.com", "password123");
            var client = CreateAuthenticatedClient(token);

            // Act
            var response = await client.PostAsync("/api/Friends/accept/invalid-id", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeclineFriend_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            // User1 requests friendship with User2
            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);

            // Get the friendship ID
            var requests = await client1.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            var friendshipId = requestList![0].friendshipId;

            // User2 declines the request
            var client2 = CreateAuthenticatedClient(token2);
            var response = await client2.PostAsync($"/api/Friends/decline/{friendshipId}", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetFriendRequests_ReturnsRequests_ForCurrentUser()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");
            var token3 = await RegisterAndLoginUser("user3@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);
            await client1.PostAsync("/api/Friends/request/user3@example.com", null);

            // Act
            var response = await client1.GetAsync("/api/Friends/requests");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var requests = await response.Content.ReadFromJsonAsync<List<FriendRequest>>();
            Assert.NotNull(requests);
            Assert.Equal(2, requests.Count);
        }

        [Fact]
        public async Task GetFriends_ReturnsAcceptedFriends()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);
            var requests = await client2.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            var friendshipId = requestList![0].friendshipId;
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            // Act
            var response = await client1.GetAsync("/api/Friends");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var friends = await response.Content.ReadFromJsonAsync<List<dynamic>>();
            Assert.NotNull(friends);
            Assert.Single(friends);
        }

        [Fact]
        public async Task DeleteFriend_ValidRequest_ReturnsOk()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);
            var requests = await client2.GetAsync("/api/Friends/requests");
            var requestList = await requests.Content.ReadFromJsonAsync<List<FriendRequest>>();
            var friendshipId = requestList![0].friendshipId;
            await client2.PostAsync($"/api/Friends/accept/{friendshipId}", null);

            // Act - Delete the friendship
            var response = await client1.DeleteAsync($"/api/Friends/{friendshipId}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetFriends_Unauthorized_Returns401()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/Friends");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RequestFriend_Unauthorized_Returns401()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsync("/api/Friends/request/user@example.com", null);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AcceptFriend_Unauthorized_Returns401()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.PostAsync("/api/Friends/accept/123", null);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("user1@example.com", "user2@example.com")]
        [InlineData("test.user@domain.com", "another@test.org")]
        public async Task RequestFriend_VariousValidEmails_Accepted(string requesterEmail, string addresseeEmail)
        {
            // Arrange
            var token = await RegisterAndLoginUser(requesterEmail, "password123");
            await RegisterAndLoginUser(addresseeEmail, "password123");
            var client = CreateAuthenticatedClient(token);

            // Act
            var response = await client.PostAsync($"/api/Friends/request/{addresseeEmail}", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetFriendRequests_IncludesRequesterInfo()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("requester@example.com", "password123");
            var token2 = await RegisterAndLoginUser("addressee@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/addressee@example.com", null);

            var client2 = CreateAuthenticatedClient(token2);

            // Act
            var response = await client2.GetAsync("/api/Friends/requests");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var requests = await response.Content.ReadFromJsonAsync<List<FriendRequest>>();
            Assert.NotNull(requests);
            Assert.Single(requests);
            Assert.Equal("requester@example.com", requests[0].requesterEmail);
        }

        [Fact]
        public async Task MultipleFriendRequests_HandledCorrectly()
        {
            // Arrange
            var token1 = await RegisterAndLoginUser("user1@example.com", "password123");
            var token2 = await RegisterAndLoginUser("user2@example.com", "password123");
            var token3 = await RegisterAndLoginUser("user3@example.com", "password123");
            var token4 = await RegisterAndLoginUser("user4@example.com", "password123");

            var client1 = CreateAuthenticatedClient(token1);
            await client1.PostAsync("/api/Friends/request/user2@example.com", null);
            await client1.PostAsync("/api/Friends/request/user3@example.com", null);
            await client1.PostAsync("/api/Friends/request/user4@example.com", null);

            // Act
            var response = await client1.GetAsync("/api/Friends/requests");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var requests = await response.Content.ReadFromJsonAsync<List<FriendRequest>>();
            Assert.NotNull(requests);
            Assert.Equal(3, requests.Count);
        }
    }
}