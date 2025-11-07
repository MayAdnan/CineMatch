using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CineMatch.API.Controllers
{
    [Route("api/[controller]")]
    public class SwipeController : BaseController
    {
        private readonly MatchService _matchService;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public SwipeController(MatchService matchService, AppDbContext context, IConfiguration config)
        {
            _matchService = matchService;
            _context = context;
            _config = config;
        }

        // Skapar en session
        [HttpPost("session")]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "User ID not found" });

            bool isFriendSession = request.IsFriendSession;
            string? friendId = request.FriendId;

            // Support both FriendId and FriendEmail for backward compatibility
            if (string.IsNullOrEmpty(friendId) && !string.IsNullOrEmpty(request.FriendEmail))
            {
                // Look up friend by email if FriendId not provided
                var friendUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.FriendEmail);
                friendId = friendUser?.Id;
            }

            string sessionId = Guid.NewGuid().ToString();

            try
            {
                if (isFriendSession)
                {
                    if (string.IsNullOrEmpty(friendId))
                        return BadRequest(new { message = "FriendId must be provided for friend sessions." });

                    // Hämta vän-användare baserat på id
                    var friendUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == friendId);
                    if (friendUser == null)
                    {
                        // Log the friend id that was not found for debugging
                        Console.WriteLine($"Friend not found for id: {friendId}, userId: {userId}");
                        return BadRequest(new { message = "Friend not found." });
                    }

                    Console.WriteLine($"Friend found: {friendId} for email: {friendUser.Email}");

                    // Kontrollera att de är vänner
                    var areFriends = await _context.Friends.AnyAsync(f =>
                        ((f.RequesterId == userId && f.AddresseeId == friendId) ||
                         (f.RequesterId == friendId && f.AddresseeId == userId)) &&
                         f.Status == FriendStatus.Accepted);

                    if (!areFriends)
                        return BadRequest(new { message = "You can only create sessions with accepted friends" });

                    // Kolla om session redan finns
                    var existingSession = await _context.MatchSessions
                        .FirstOrDefaultAsync(m =>
                            m.IsFriendSession &&
                            ((m.User1Id == userId && m.User2Id == friendId) ||
                             (m.User1Id == friendId && m.User2Id == userId)));

                    if (existingSession != null)
                    {
                        existingSession.MatchedMovieId = null;

                        var oldSwipes = await _context.MovieSwipes
                            .Where(s => s.SessionId == existingSession.Id)
                            .ToListAsync();

                        if (oldSwipes.Any())
                            _context.MovieSwipes.RemoveRange(oldSwipes);

                        await _context.SaveChangesAsync();
                        sessionId = existingSession.Id;
                    }
                    else
                    {
                        var userIds = new[] { userId, friendId }.OrderBy(id => id).ToArray();
                        sessionId = $"{userIds[0]}_{userIds[1]}";

                        var matchSession = new MatchSession
                        {
                            Id = sessionId,
                            User1Id = userIds[0],
                            User2Id = userIds[1],
                            MatchedMovieId = null,
                            IsFriendSession = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.MatchSessions.Add(matchSession);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    // For regular sessions, create a new MatchSession record
                    var matchSession = new MatchSession
                    {
                        Id = sessionId,
                        User1Id = userId,
                        User2Id = userId, // Same user for single-user sessions
                        MatchedMovieId = null,
                        IsFriendSession = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.MatchSessions.Add(matchSession);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    sessionId,
                    friendId,
                    isFriendSession
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to create session", error = ex.Message });
            }
        }

        // Hämta session info
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionInfo(string sessionId)
        {
            var session = await _context.MatchSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return NotFound(new { message = "Session not found." });

            return Ok(new
            {
                session.Id,
                session.IsFriendSession,
                session.MatchedMovieId,
                session.User1Id,
                session.User2Id
            });
        }

        // Sparar swipe
        [HttpPost]
        public async Task<IActionResult> SaveSwipe([FromBody] MovieSwipe swipe)
        {
            Console.WriteLine($"[SaveSwipe] Starting swipe save for user {swipe.UserId}, movie {swipe.MovieId}, session {swipe.SessionId}, liked {swipe.IsLiked}");

            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("[SaveSwipe] User ID not found in token");
                    return BadRequest(new { message = "User ID not found" });
                }

                // Override UserId with authenticated user ID from JWT token
                swipe.UserId = userId;
                swipe.CreatedAt = DateTime.UtcNow;

                // Use the original injected context but be very careful about tracking
                // Validate session exists and user has access
                var session = await _context.MatchSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == swipe.SessionId);

                if (session == null)
                {
                    Console.WriteLine($"[SaveSwipe] Session not found: {swipe.SessionId}");
                    return BadRequest(new { message = "Session not found." });
                }

                // Check if user has access to this session
                if (session.IsFriendSession)
                {
                    // For friend sessions, user must be one of the participants
                    if (session.User1Id != userId && session.User2Id != userId)
                    {
                        Console.WriteLine($"[SaveSwipe] Access denied for user {userId} to friend session {swipe.SessionId}. Participants: {session.User1Id}, {session.User2Id}");
                        return BadRequest(new { message = "You don't have access to this friend session." });
                    }
                }
                else
                {
                    // For regular sessions, only the creator (User1Id) can swipe
                    if (session.User1Id != userId)
                    {
                        Console.WriteLine($"[SaveSwipe] Access denied for user {userId} to regular session {swipe.SessionId}. Creator: {session.User1Id}");
                        return BadRequest(new { message = "You don't have access to this regular session." });
                    }
                }

                // Save swipe using EF to maintain compatibility with both SQL Server and SQLite
                var existingSwipe = await _context.MovieSwipes
                    .FirstOrDefaultAsync(s => s.UserId == swipe.UserId && s.MovieId == swipe.MovieId && s.SessionId == swipe.SessionId);

                if (existingSwipe != null)
                {
                    // Update existing swipe
                    existingSwipe.IsLiked = swipe.IsLiked;
                    existingSwipe.CreatedAt = swipe.CreatedAt;
                }
                else
                {
                    // Add new swipe
                    _context.MovieSwipes.Add(swipe);
                }

                await _context.SaveChangesAsync();

                // Check for matches - use the same context but ensure no tracking issues
                _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                // Debug: log all unique users who swiped in this session
                var uniqueUsersInSession = await _context.MovieSwipes
                    .AsNoTracking()
                    .Where(s => s.SessionId == swipe.SessionId)
                    .Select(s => s.UserId)
                    .Distinct()
                    .ToListAsync();

                Console.WriteLine($"[DEBUG] Match detection attempt for session {swipe.SessionId}");
                Console.WriteLine($"[DEBUG] Unique users in session: {string.Join(", ", uniqueUsersInSession)}");

                // Debug: log movie swipes grouped by movie for this session
                var movieSwipeGroups = await _context.MovieSwipes
                    .AsNoTracking()
                    .Where(s => s.SessionId == swipe.SessionId)
                    .GroupBy(s => s.MovieId)
                    .Select(g => new {
                        MovieId = g.Key,
                        TotalSwipes = g.Count(),
                        LikedCount = g.Count(s => s.IsLiked),
                        DislikedCount = g.Count(s => !s.IsLiked),
                        UsersWhoLiked = g.Where(s => s.IsLiked).Select(s => s.UserId).ToList()
                    })
                    .ToListAsync();

                foreach (var group in movieSwipeGroups)
                {
                    Console.WriteLine($"[DEBUG] Movie {group.MovieId}: {group.LikedCount} likes from users: {string.Join(", ", group.UsersWhoLiked)}");
                }

                int likedCount;
                int matchedMovieId;

                likedCount = await _context.MovieSwipes
                    .Where(s => s.SessionId == swipe.SessionId && s.IsLiked)
                    .CountAsync();

                matchedMovieId = await _context.MovieSwipes
                    .Where(s => s.SessionId == swipe.SessionId && s.IsLiked)
                    .GroupBy(s => s.MovieId)
                    .Where(g => g.Select(s => s.UserId).Distinct().Count() >= 2)
                    .Select(g => g.Key)
                    .OrderBy(id => id)
                    .FirstOrDefaultAsync();

                Console.WriteLine($"Match check: found {likedCount} liked swipes, matched movie: {matchedMovieId}");

                // Debug: log all liked swipes in this session
                var allLiked = await _context.MovieSwipes
                    .AsNoTracking()
                    .Where(s => s.SessionId == swipe.SessionId && s.IsLiked)
                    .ToListAsync();

                Console.WriteLine("All liked swipes in session:");
                foreach (var likedSwipe in allLiked)
                {
                    Console.WriteLine($"  User {likedSwipe.UserId} liked movie {likedSwipe.MovieId}");
                }

                bool isMatch = matchedMovieId > 0;
                MovieSwipe? matchedSwipe = null;

                if (isMatch)
                {
                    // Get the matched swipe from the context
                    matchedSwipe = await _context.MovieSwipes
                        .Where(s => s.SessionId == swipe.SessionId && s.MovieId == matchedMovieId)
                        .FirstOrDefaultAsync();

                    // Persist the match to the MatchSession
                    var sessionToUpdate = await _context.MatchSessions
                        .FirstOrDefaultAsync(s => s.Id == swipe.SessionId);
                    if (sessionToUpdate != null)
                    {
                        sessionToUpdate.MatchedMovieId = matchedMovieId;
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"[DEBUG] Persisted match for session {swipe.SessionId}: movie {matchedMovieId}");
                    }

                    Console.WriteLine($"Match found for movie {matchedMovieId}");
                }

                object? matchedMovie = null;
                if (isMatch && matchedSwipe != null)
                {
                    try
                    {
                        var httpClient = new HttpClient();
                        var apiKey = _config["TMDB:ApiKey"];
                        var url = $"https://api.themoviedb.org/3/movie/{matchedSwipe.MovieId}?api_key={apiKey}";
                        var response = await httpClient.GetStringAsync(url);
                        var movieData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);

                        matchedMovie = new
                        {
                            id = matchedSwipe.MovieId,
                            title = movieData.GetProperty("title").GetString() ?? "Matched Movie",
                            overview = movieData.GetProperty("overview").GetString() ?? "You both liked this movie!",
                            poster_path = movieData.GetProperty("poster_path").GetString() ?? "/placeholder.jpg",
                            vote_average = movieData.GetProperty("vote_average").GetDouble(),
                            release_date = movieData.GetProperty("release_date").GetString() ?? "",
                            backdrop_path = movieData.GetProperty("backdrop_path").GetString() ?? "",
                            genre_ids = new int[0]
                        };
                    }
                    catch
                    {
                        matchedMovie = new
                        {
                            id = matchedSwipe.MovieId,
                            title = matchedSwipe.MovieId.ToString(),
                            overview = "You both liked this movie!",
                            poster_path = "/placeholder.jpg",
                            vote_average = 0.0,
                            release_date = "",
                            backdrop_path = "",
                            genre_ids = new int[0]
                        };
                    }
                }

                return Ok(new
                {
                    message = "Swipe saved successfully",
                    isMatch,
                    matchedMovie
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Swipe error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return BadRequest(new { message = "Failed to save swipe", error = ex.Message });
            }
        }
    }
}
