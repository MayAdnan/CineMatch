using CineMatch.API.Data;
using CineMatch.API.Models;
using CineMatch.API.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace CineMatchTests.TestHelpers
{
    public static class TestDataSeeder
    {
        private static readonly SemaphoreSlim _seedingSemaphore = new SemaphoreSlim(1, 1);
        private static bool _seeded = false;

        public static async Task SeedTestData(AppDbContext context)
        {
            if (_seeded) return;

            await _seedingSemaphore.WaitAsync();
            try
            {
                if (_seeded) return;

                // Clear EF Core change tracker to avoid tracking conflicts
                context.ChangeTracker.Clear();

                // Make seeding idempotent: if test data already exists, skip seeding but clear dynamic data
                if (await context.Users.AnyAsync(u => u.Id == "user1"))
                {
                    // Remove existing dynamic entities
                    context.MovieSwipes.RemoveRange(await context.MovieSwipes.ToListAsync());
                    context.MatchSessions.RemoveRange(await context.MatchSessions.ToListAsync());
                    context.Friends.RemoveRange(await context.Friends.ToListAsync());
                    await context.SaveChangesAsync();
                    _seeded = true;
                    return;
                }

                // Remove existing entities
                context.MovieSwipes.RemoveRange(await context.MovieSwipes.ToListAsync());
                context.MatchSessions.RemoveRange(await context.MatchSessions.ToListAsync());
                context.Friends.RemoveRange(await context.Friends.ToListAsync());
                context.Users.RemoveRange(await context.Users.ToListAsync());
                await context.SaveChangesAsync();

                // --- USERS ---
                var users = new[]
                {
                    new User { Id = "user1", Email = "user1@test.com", Password = "hashedpassword1" },
                    new User { Id = "user2", Email = "user2@test.com", Password = "hashedpassword2" },
                    new User { Id = "user3", Email = "user3@test.com", Password = "hashedpassword3" },
                    new User { Id = "user4", Email = "user4@test.com", Password = "hashedpassword4" },
                    new User { Id = "user5", Email = "user5@test.com", Password = "hashedpassword5" }
                };
                context.Users.AddRange(users);
                await context.SaveChangesAsync();

                // --- MATCH SESSIONS ---
                var sessions = new[]
                {
                    new MatchSession
                    {
                        Id = "session1",
                        User1Id = "user1",
                        User2Id = "user2",
                        IsFriendSession = true,
                        MatchedMovieId = 0,
                        CreatedAt = DateTime.UtcNow.AddHours(-1)
                    },
                    new MatchSession
                    {
                        Id = "session2",
                        User1Id = "user3",
                        User2Id = null,
                        IsFriendSession = false,
                        MatchedMovieId = 0,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-30)
                    },
                    new MatchSession
                    {
                        Id = "session3",
                        User1Id = "user1",
                        User2Id = null,
                        IsFriendSession = false,
                        MatchedMovieId = 123,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-15)
                    }
                };
                context.MatchSessions.AddRange(sessions);
                await context.SaveChangesAsync();

                // --- MOVIE SWIPES ---
                var swipes = new List<MovieSwipe>
                {
                    // Friend session (session1)
                    new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 1, IsLiked = true },
                    new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 2, IsLiked = false },
                    new MovieSwipe { SessionId = "session1", UserId = "user1", MovieId = 3, IsLiked = true },
                    new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 1, IsLiked = true },
                    new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 2, IsLiked = true },
                    new MovieSwipe { SessionId = "session1", UserId = "user2", MovieId = 4, IsLiked = false },

                    // Regular session (session2)
                    new MovieSwipe { SessionId = "session2", UserId = "user3", MovieId = 10, IsLiked = true },
                    new MovieSwipe { SessionId = "session2", UserId = "user3", MovieId = 11, IsLiked = true },
                    new MovieSwipe { SessionId = "session2", UserId = "user3", MovieId = 12, IsLiked = false },
                    new MovieSwipe { SessionId = "session2", UserId = "user3", MovieId = 13, IsLiked = true },
                    new MovieSwipe { SessionId = "session2", UserId = "user3", MovieId = 14, IsLiked = true },

                    // Completed session (session3)
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 100, IsLiked = true },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 101, IsLiked = false },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 102, IsLiked = true },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 103, IsLiked = true },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 104, IsLiked = true },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 123, IsLiked = true },
                    new MovieSwipe { SessionId = "session3", UserId = "user1", MovieId = 124, IsLiked = false }
                };
                context.MovieSwipes.AddRange(swipes);
                await context.SaveChangesAsync();

                _seeded = true;
            }
            finally
            {
                _seedingSemaphore.Release();
            }
        }

        public static async Task<MatchSession> CreateMatchSession(AppDbContext context, string sessionId, string user1Id, string? user2Id = null, bool isFriendSession = false)
        {
            var session = new MatchSession
            {
                Id = sessionId,
                User1Id = user1Id,
                User2Id = user2Id,
                IsFriendSession = isFriendSession,
                MatchedMovieId = 0,
                CreatedAt = DateTime.UtcNow
            };

            context.MatchSessions.Update(session); // Safe: Add or attach
            await context.SaveChangesAsync();
            return session;
        }

        public static async Task<User> CreateTestUser(AppDbContext context, string userId, string email)
        {
            var user = new User { Id = userId, Email = email, Password = $"password{userId}" };
            context.Users.Update(user);
            await context.SaveChangesAsync();
            return user;
        }

        public static async Task CreateFriendship(AppDbContext context, string user1Id, string user2Id, FriendStatus status = FriendStatus.Accepted)
        {
            var friendship = new Friend { RequesterId = user1Id, AddresseeId = user2Id, Status = status };
            context.Friends.Update(friendship);
            await context.SaveChangesAsync();
        }

        public static async Task CreateMovieSwipes(AppDbContext context, string sessionId, string userId, IEnumerable<int> movieIds, bool isLiked = true)
        {
            var swipes = movieIds.Select(movieId => new MovieSwipe
            {
                SessionId = sessionId,
                UserId = userId,
                MovieId = movieId,
                IsLiked = isLiked
            });
            context.MovieSwipes.UpdateRange(swipes);
            await context.SaveChangesAsync();
        }
    }
}
