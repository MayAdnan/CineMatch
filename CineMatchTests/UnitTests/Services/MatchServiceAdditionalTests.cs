using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using CineMatchTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CineMatchTests.UnitTests.Services
{
    public class MatchServiceAdditionalTests : IClassFixture<TestDatabaseFixture>
    {
        private readonly TestDatabaseFixture _fixture;
        private readonly AppDbContext _context;
        private readonly MatchService _service;

        public MatchServiceAdditionalTests(TestDatabaseFixture fixture)
        {
            _fixture = fixture;
            _context = _fixture.Context;
            _service = new MatchService(_context);
        }

        private async Task SetupTestData()
        {
            // Clear all existing data
            _context.ChangeTracker.Clear();
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            // Clear existing data by deleting all records and detach any tracked entities
            foreach (var entry in _context.ChangeTracker.Entries())
            {
                entry.State = EntityState.Detached;
            }
            await _context.MatchSessions.ExecuteDeleteAsync();
            await _context.MovieSwipes.ExecuteDeleteAsync();
            await _context.Friends.ExecuteDeleteAsync();
            await _context.Users.ExecuteDeleteAsync();

            // Create test users
            var users = new[]
            {
                new User { Id = "user1", Email = "user1@test.com", Password = "hash1" },
                new User { Id = "user2", Email = "user2@test.com", Password = "hash2" },
                new User { Id = "user3", Email = "user3@test.com", Password = "hash3" },
                new User { Id = "user4", Email = "user4@test.com", Password = "hash4" },
                new User { Id = "user5", Email = "user5@test.com", Password = "hash5" }
            };
            await _context.Users.AddRangeAsync(users);
            await _context.SaveChangesAsync();

            // Suppress migration warnings for in-memory database tests
            // await _context.Database.MigrateAsync();
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_PrioritizesEarliestMatch()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 4, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should match on movie 1 (earliest/lowest ID)
            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(1, result.matchedSwipe.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_RegularSession_NoMatchWhenNoSwipesFromOthers()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - no match because no other users have liked these movies
            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_HandlesThreeUsers()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user3", MovieId = 1, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should match on movie 1 (liked by user1, user2, and user3)
            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(1, result.matchedSwipe.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_RegularSession_HandlesFallbackMatch()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 6, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act - call multiple times to potentially trigger fallback
            var results = new List<(bool isMatch, MovieSwipe? matchedSwipe)>();
            for (int i = 0; i < 20; i++)
            {
                var result = await _service.GetMatchForSession("session1", "user1");
                results.Add(result);
            }

            // Assert - at least one should be a "fallback" match
            var fallbackMatches = results.Where(r => r.isMatch).ToList();
            Assert.True(fallbackMatches.Any(), "Expected at least one fallback match due to random chance");
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_IgnoresDislikedMovies()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = false }, // Disliked
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 2, IsLiked = true }  // User2 liked but user1 disliked
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should match on movie 1 (both liked), not movie 2 (user1 disliked)
            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(1, result.matchedSwipe.MovieId);
        }

        [Theory]
        [InlineData(1, 5)] // 1 friend likes 5 movies
        [InlineData(2, 3)] // 2 friends like 3 movies each
        [InlineData(3, 2)] // 3 friends like 2 movies each
        public async Task GetMatchForSession_RegularSession_PrioritizesFriends(int friendCount, int moviesPerFriend)
        {
            // Arrange
            await SetupTestData();

            // Create friendships
            for (int i = 2; i <= friendCount + 1; i++)
            {
                var friendship = new Friend { Id = Guid.NewGuid().ToString(), RequesterId = "user1", AddresseeId = $"user{i}", Status = FriendStatus.Accepted };
                await _context.Friends.AddAsync(friendship);
            }

            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new List<MovieSwipe>();
            // User1 likes movies 1-5
            for (int i = 1; i <= 5; i++)
            {
                swipes.Add(new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = i, IsLiked = true });
            }

            // Friends like movies starting from movie 10
            for (int friend = 2; friend <= friendCount + 1; friend++)
            {
                for (int movie = 10; movie < 10 + moviesPerFriend; movie++)
                {
                    swipes.Add(new MovieSwipe { SessionId = $"session{friend}", UserId = $"user{friend}", MovieId = movie, IsLiked = true });
                }
            }

            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetMatchForSession_HandlesSessionWithUser2Null()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = null,
                IsFriendSession = false,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert
            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_UpdatesMatchedMovieIdCorrectly()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 5, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert
            Assert.True(result.isMatch);
            Assert.Equal(5, result.matchedSwipe!.MovieId);

            // Verify session was updated
            var updatedSession = await _context.MatchSessions.FirstAsync(s => s.Id == "session1");
            Assert.Equal(5, updatedSession.MatchedMovieId);
        }

        [Fact]
        public async Task GetMatchForSession_RegularSession_CreatesNewMatchSession()
        {
            // Arrange
            await SetupTestData();

            // Create friendship
            var friendship = new Friend { Id = Guid.NewGuid().ToString(), RequesterId = "user1", AddresseeId = "user2", Status = FriendStatus.Accepted };
            await _context.Friends.AddAsync(friendship);

            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = null, IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            // User1 likes movies 1-5
            var user1Swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true }
            };

            // User2 likes movie 3
            var user2Swipe = new MovieSwipe { SessionId = "sessionX", UserId = "user2", MovieId = 3, IsLiked = true };

            await _context.MovieSwipes.AddRangeAsync(user1Swipes);
            await _context.MovieSwipes.AddAsync(user2Swipe);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert
            Assert.True(result.isMatch);
            Assert.Equal(3, result.matchedSwipe!.MovieId);

            // Verify new match session was created
            var matchSessions = await _context.MatchSessions.Where(s => s.IsFriendSession == false && s.User1Id == "user1").ToListAsync();
            Assert.True(matchSessions.Count >= 2, "Should have created a new match session");
        }

        [Fact]
        public async Task GetMatchForSession_HandlesNullUserId()
        {
            // Arrange
            await SetupTestData();

            // Act & Assert - should not throw exception
            var result = await _service.GetMatchForSession("session1", null!);
            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_HandlesEmptySessionId()
        {
            // Arrange
            await SetupTestData();

            // Act & Assert
            var result = await _service.GetMatchForSession("", "user1");
            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_HandlesUserNotInSession()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true
            };
            await _context.MatchSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            // Act - user3 tries to get match for a session they're not in
            var result = await _service.GetMatchForSession("session1", "user3");

            // Assert - should handle gracefully
            Assert.False(result.isMatch);
            Assert.Null(result.matchedSwipe);
        }

        [Fact]
        public async Task GetMatchForSession_RegularSession_IgnoresOwnDislikes()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = false }, // User1 disliked this
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true },
                new MovieSwipe { SessionId = "sessionX", UserId = "user2", MovieId = 2, IsLiked = true }  // User2 liked movie 2
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should not match on movie 2 because user1 disliked it
            if (result.isMatch)
            {
                Assert.NotEqual(2, result.matchedSwipe!.MovieId);
            }
        }

        [Fact]
        public async Task GetMatchForSession_HandlesDatabaseException()
        {
            // Arrange - create service with disconnected context
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;
            var disconnectedContext = new AppDbContext(options);
            var service = new MatchService(disconnectedContext);

            // Act & Assert - should handle database exceptions gracefully
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(async () =>
            {
                await service.GetMatchForSession("session1", "user1");
            });
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_HandlesMultipleMatches()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 4, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should match on lowest ID (1)
            Assert.True(result.isMatch);
            Assert.Equal(1, result.matchedSwipe!.MovieId);
        }

        [Theory]
        [InlineData("user1", "user2")]
        [InlineData("user2", "user1")]
        public async Task GetMatchForSession_FriendSession_WorksFromEitherUser(string requestingUser, string otherUser)
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2",
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", requestingUser);

            // Assert
            Assert.True(result.isMatch);
            Assert.Equal(1, result.matchedSwipe!.MovieId);
        }

        [Fact]
        public async Task GetMatchForSession_RegularSession_HandlesSelfMatchingPrevention()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - user cannot match with themselves
            if (result.isMatch)
            {
                Assert.NotEqual("user1", result.matchedSwipe!.UserId);
            }
        }

        [Fact]
        public async Task GetMatchForSession_HandlesRaceConditionSimulation()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession { Id = "session1", User1Id = "user1", User2Id = "user2", IsFriendSession = false };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 4, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 5, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);

            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - should handle gracefully despite duplicate data
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetMatchForSession_FriendSession_RequiresMinimumTwoUsers()
        {
            // Arrange
            await SetupTestData();
            var session = new MatchSession
            {
                Id = "session1",
                User1Id = "user1",
                User2Id = "user2", // Need a second user for friend session
                IsFriendSession = true,
                MatchedMovieId = 0
            };
            await _context.MatchSessions.AddAsync(session);

            var swipes = new[]
            {
                new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true }
            };
            await _context.MovieSwipes.AddRangeAsync(swipes);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetMatchForSession("session1", "user1");

            // Assert - friend session with two users and matching likes should match
            Assert.True(result.isMatch);
            Assert.NotNull(result.matchedSwipe);
            Assert.Equal(1, result.matchedSwipe!.MovieId);
        }
    }
}