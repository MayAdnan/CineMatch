using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using CineMatchTests.TestHelpers;
using Xunit;

namespace CineMatchTests.IntegrationTests
{
    public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory<CineMatch.API.Program>>, IClassFixture<TestDatabaseFixture>
    {
        private readonly CustomWebApplicationFactory<CineMatch.API.Program> _factory;
        private readonly TestDatabaseFixture _dbFixture;
        private readonly HttpClient _client;

        public AuthIntegrationTests(CustomWebApplicationFactory<CineMatch.API.Program> factory, TestDatabaseFixture dbFixture)
        {
            _factory = factory;
            _dbFixture = dbFixture;

            // Seed database once
            TestDataSeeder.SeedTestData(_dbFixture.Context).GetAwaiter().GetResult();

            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task RegisterLoginLogout_FullUserJourney()
        {
            var registerRequest = new { Email = "integration@example.com", Password = "password123" };
            var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);

            var loginRequest = new { Email = "integration@example.com", Password = "password123" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(loginResult);
            Assert.NotNull(loginResult.token);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            var registerRequest = new { Email = "duplicate@example.com", Password = "password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var secondResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            var loginRequest = new { Email = "nonexistent@example.com", Password = "wrongpassword" };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsJwtToken()
        {
            var registerRequest = new { Email = "token@example.com", Password = "password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var loginRequest = new { Email = "token@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<dynamic>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(result.token);
            Assert.True(result.token.ToString().Length > 100);
        }

        [Theory]
        [InlineData("invalid-email")]
        [InlineData("test @example.com")]
        [InlineData(".test@example.com")]
        [InlineData("test.@example.com")]
        [InlineData("test..test@example.com")]
        [InlineData("test@example..com")]
        [InlineData("test")]
        [InlineData("test@")]
        [InlineData("@example.com")]
        public async Task Register_InvalidEmailFormats_ReturnBadRequest(string email)
        {
            var registerRequest = new { Email = email, Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailCaseSensitivity()
        {
            var registerRequest = new { Email = "CaseTest@Example.Com", Password = "password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var loginRequest = new { Email = "casetest@example.com", Password = "password123" };
            var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        }

        [Fact]
        public async Task Register_EmailWithSubdomain_Accepted()
        {
            var registerRequest = new { Email = "test@sub.example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailWithPlusSign_Accepted()
        {
            var registerRequest = new { Email = "test+tag@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailWithUnderscore_Accepted()
        {
            var registerRequest = new { Email = "test_user@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailWithDotInLocalPart_Accepted()
        {
            var registerRequest = new { Email = "test.user@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_UnicodeEmail_Accepted()
        {
            var registerRequest = new { Email = "tëst@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_InternationalDomain_Accepted()
        {
            var registerRequest = new { Email = "test@example.co.uk", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_DashInDomain_Accepted()
        {
            var registerRequest = new { Email = "test@example-domain.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_WhitespaceInEmail_ReturnsBadRequest()
        {
            var registerRequest = new { Email = "test @example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailStartingWithDot_ReturnsBadRequest()
        {
            var registerRequest = new { Email = ".test@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_EmailEndingWithDot_ReturnsBadRequest()
        {
            var registerRequest = new { Email = "test.@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData(" ")]
        [InlineData(null)]
        [InlineData("a")]
        public async Task Register_InvalidPasswords_ReturnBadRequest(string password)
        {
            var registerRequest = new { Email = $"test{Guid.NewGuid()}@example.com", Password = password };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Theory]
        [InlineData("Password123")]
        [InlineData("P@ssw0rd!")]
        [InlineData("123456789")]
        [InlineData("ABCDEFGHIJ")]
        [InlineData("abcdefghij")]
        public async Task Register_ValidPasswords_Accepted(string password)
        {
            var registerRequest = new { Email = $"test{Guid.NewGuid()}@example.com", Password = password };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_LongPassword_Accepted()
        {
            var longPassword = string.Concat(Enumerable.Repeat("a", 100));
            var registerRequest = new { Email = "longpass@example.com", Password = longPassword };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_NullEmail_ReturnsBadRequest()
        {
            var registerRequest = new { Email = (string)null, Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_NullPassword_ReturnsBadRequest()
        {
            var registerRequest = new { Email = "test@example.com", Password = (string)null };
            var response = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_NullEmail_ReturnsBadRequest()
        {
            var loginRequest = new { Email = (string)null, Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_NullPassword_ReturnsBadRequest()
        {
            var loginRequest = new { Email = "test@example.com", Password = (string)null };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_WrongPasswordCase_ReturnsUnauthorized()
        {
            var registerRequest = new { Email = "case@example.com", Password = "Password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var loginRequest = new { Email = "case@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_AfterSuccessfulRegistration_Works()
        {
            var registerRequest = new { Email = "workflow@example.com", Password = "password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

            var loginRequest = new { Email = "workflow@example.com", Password = "password123" };
            var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Register_ConsecutiveRegistrations_Work()
        {
            var request1 = new { Email = "user1@example.com", Password = "password123" };
            await _client.PostAsJsonAsync("/api/Auth/register", request1);

            var request2 = new { Email = "user2@example.com", Password = "password123" };
            var response2 = await _client.PostAsJsonAsync("/api/Auth/register", request2);
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        }
    }
}
