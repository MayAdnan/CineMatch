using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace CineMatchTests.UnitTests.Services
{
    public class MatchServiceTests : IClassFixture<TestDatabaseFixture>
    {
        private readonly TestDatabaseFixture _fixture;
        private readonly AppDbContext _context;
        private readonly MatchService _service;

        public MatchServiceTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
            _context = _fixture.Context;
            _service = new MatchService(_context);
        }

        private async Task SetupTestData()
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Clear change tracker to avoid tracking conflicts
            _context.ChangeTracker.Clear();

            // Clear existing data
            _context.MatchSessions.RemoveRange(_context.MatchSessions);
            _context.MovieSwipes.RemoveRange(_context.MovieSwipes);
            _context.Friends.RemoveRange(_context.Friends);
            _context.Users.RemoveRange(_context.Users);
            await _context.SaveChangesAsync();

            // Create test users
            var user1 = new User { Id = "user1", Email = "user1@test.com", Password = "hash1" };
            var user2 = new User { Id = "user2", Email = "user2@test.com", Password = "hash2" };
            var user3 = new User { Id = "user3", Email = "user3@test.com", Password = "hash3" };
            await _context.Users.AddRangeAsync(user1, user2, user3);
            await _context.SaveChangesAsync();
        }

        [Fact]
        public async Task GetMatchForSession_ShouldReturnFalse_WhenNoSessionExists()
        {
            await SetupTestData();

            var result = await _service.GetMatchForSession("nonexistent", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldReturnFalse_WhenRegularSessionHasLessThan5Swipes()
        {
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldReturnTrue_WhenSessionAlreadyHasMatch()
        {
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", MatchedMovieId = 1, IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipe = new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true };
            await _context.MovieSwipes.AddAsync(swipe);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(1, result.matchedSwipe.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldMatchWithFriend_WhenRegularSessionHasEnoughSwipes()
        {
            await SetupTestData();

            var friendship = new Friend { RequesterId = "user1", AddresseeId = "user2", Status = FriendStatus.Accepted };
            await _context.Friends.AddAsync(friendship);

            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var user1Swipes = Enumerable.Range(1, 5)
                .Select(i => new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = i, IsLiked = true })
                .ToArray();

            var user2Swipe = new MovieSwipe { SessionId = "sessionX", UserId = "user2", MovieId = 3, IsLiked = true };

            await _context.MovieSwipes.AddRangeAsync(user1Swipes);
            await _context.MovieSwipes.AddAsync(user2Swipe);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(3, result.matchedSwipe.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldMatchWithAnyUser_WhenNoFriendMatch()
        {
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var user1Swipes = Enumerable.Range(1, 5)
                .Select(i => new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = i, IsLiked = true })
                .ToArray();

            var user3Swipe = new MovieSwipe { SessionId = "sessionX", UserId = "user3", MovieId = 4, IsLiked = true };

            await _context.MovieSwipes.AddRangeAsync(user1Swipes);
            await _context.MovieSwipes.AddAsync(user3Swipe);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(4, result.matchedSwipe.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldMatchFriendSession_WhenTwoUsersLikeSameMovie()
        {
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 3, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(2, result.matchedSwipe.MovieId);

            var updatedSession = await _context.MatchSessions.FirstAsync(s => s.Id == "session1");
            Assert.Equal(2, updatedSession.MatchedMovieId);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldReturnFalse_WhenFriendSessionHasNoMutualLikes()
        {
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 4, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(20)]
        [InlineData(50)]
        [InlineData(100)]
        public async Task GetMatchForSession_ShouldHandleLargeNumberOfSwipes(int swipeCount)
        {
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = Enumerable.Range(1, swipeCount)
                .Select(i => new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = i, IsLiked = true })
                .ToList();

            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldHandleEmptyLikedSwipes()
        {
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldHandleNullSession()
        {
            await SetupTestData();

            var result = await _service.GetMatchForSession("nonexistent", "user1");

            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_ShouldPrioritizeFriendMatchesOverAnyMatches()
        {
            await SetupTestData();

            var friendship = new Friend { RequesterId = "user1", AddresseeId = "user2", Status = FriendStatus.Accepted };
            await _context.Friends.AddAsync(friendship);

            var session = new MatchSession { Id = "session1", User1Id = "user1", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var user1Swipes = Enumerable.Range(1, 5)
                .Select(i => new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = i, IsLiked = true })
                .ToArray();

            var user2Swipe = new MovieSwipe { SessionId = "sessionX", UserId = "user2", MovieId = 4, IsLiked = true };
            var user3Swipe = new MovieSwipe { SessionId = "sessionY", UserId = "user3", MovieId = 3, IsLiked = true };

            await _context.MovieSwipes.AddRangeAsync(user1Swipes);
            await _context.MovieSwipes.AddRangeAsync(user2Swipe, user3Swipe);
            await _context.SaveChangesAsync();

            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(4, result.matchedSwipe.MovieId);
        }
    }
}
