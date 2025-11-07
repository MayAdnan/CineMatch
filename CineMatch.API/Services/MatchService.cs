using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using Microsoft.EntityFrameworkCore;

public class MatchService
{
    private readonly AppDbContext _context;

    public MatchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool isMatch, MovieSwipe? matchedSwipe)> GetMatchForSession(string sessionId, string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return (false, null);

        // Fetch session
        var session = await _context.MatchSessions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == sessionId);
        if (session == null)
            return (false, null);

        bool isFriendSession = session.IsFriendSession;

        // Get liked swipes for current session
        var likedSwipes = await _context.MovieSwipes.AsNoTracking()
            .Where(s => s.SessionId == sessionId && s.IsLiked)
            .ToListAsync();

        // If already matched
        if (session.MatchedMovieId != null && session.MatchedMovieId > 0)
        {
            var matchedSwipe = likedSwipes.FirstOrDefault(s => s.MovieId == session.MatchedMovieId);
            return (true, matchedSwipe);
        }

        // Get friends
        var friendIds = isFriendSession
            ? GetFriendIdsFromSession(session, userId)
            : await GetFriendIdsAsync(userId);

        // Friend session logic
        if (isFriendSession)
        {
            return await HandleFriendSessionOptimized(session);
        }

        // Regular session logic
        if (!isFriendSession && likedSwipes.Count < 5)
            return (false, null);

        return await HandleRegularSessionOptimized(session, likedSwipes, userId, friendIds);
    }

    private async Task<(bool, MovieSwipe?)> HandleFriendSessionOptimized(MatchSession session)
    {
        Console.WriteLine($"[HandleFriendSessionOptimized] Checking session {session.Id}");

        // Find movies liked by at least 2 users in this session using DB query
        var likedSwipes = await _context.MovieSwipes.AsNoTracking()
            .Where(s => s.SessionId == session.Id && s.IsLiked)
            .ToListAsync();

        Console.WriteLine($"[HandleFriendSessionOptimized] Found {likedSwipes.Count} liked swipes in session {session.Id}");
        foreach (var swipe in likedSwipes)
        {
            Console.WriteLine($"  User {swipe.UserId} liked movie {swipe.MovieId}");
        }

        var matchedMovie = await _context.MovieSwipes.AsNoTracking()
            .Where(s => s.SessionId == session.Id && s.IsLiked)
            .GroupBy(s => s.MovieId)
            .Where(g => g.Select(s => s.UserId).Distinct().Count() >= 2)
            .Select(g => g.Key)
            .OrderBy(id => id)
            .FirstOrDefaultAsync();

        Console.WriteLine($"[HandleFriendSessionOptimized] Matched movie: {matchedMovie}");
        Console.WriteLine($"[DEBUG] Friend session match check result: {matchedMovie > 0}");

        if (matchedMovie == 0) return (false, null);

        // Get one swipe as matchedSwipe
        var matchedSwipe = await _context.MovieSwipes.AsNoTracking()
            .Where(s => s.SessionId == session.Id && s.MovieId == matchedMovie)
            .FirstOrDefaultAsync();

        if (matchedSwipe == null) return (false, null);

        Console.WriteLine($"[HandleFriendSessionOptimized] Found match for movie {matchedMovie}, updating session");

        // Update session - create a new context to avoid tracking conflicts
        var updateContext = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_context.Database.GetDbConnection().ConnectionString)
                .Options);

        var sessionToUpdate = await updateContext.MatchSessions.FindAsync(session.Id);
        if (sessionToUpdate != null)
        {
            sessionToUpdate.MatchedMovieId = matchedMovie;
            await updateContext.SaveChangesAsync();
        }
        await updateContext.DisposeAsync();

        return (true, matchedSwipe);
    }

    private async Task<(bool, MovieSwipe?)> HandleRegularSessionOptimized(MatchSession session, List<MovieSwipe> likedSwipes, string userId, List<string> friendIds)
    {
        using var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();

        var matchQuery = from swipe in _context.MovieSwipes.AsNoTracking()
                          where swipe.IsLiked
                          && swipe.UserId != userId
                          && likedSwipes.Select(ls => ls.MovieId).Contains(swipe.MovieId)
                          && friendIds.Contains(swipe.UserId)
                          select swipe;

        var friendMatch = await matchQuery.FirstOrDefaultAsync();
        if (friendMatch != null)
        {
            var matchedSwipe = likedSwipes.FirstOrDefault(ls => ls.MovieId == friendMatch.MovieId);
            if (matchedSwipe == null)
                return (false, null);

            command.CommandText = "UPDATE MatchSessions SET MatchedMovieId = @matchedMovieId WHERE Id = @sessionId";
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@matchedMovieId", matchedSwipe.MovieId));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@sessionId", session.Id));
            await command.ExecuteNonQueryAsync();
            return (true, matchedSwipe);
        }

        var otherMatchQuery = from swipe in _context.MovieSwipes.AsNoTracking()
                               where swipe.IsLiked
                               && swipe.UserId != userId
                               && likedSwipes.Select(ls => ls.MovieId).Contains(swipe.MovieId)
                               select swipe;

        var otherMatch = await otherMatchQuery.FirstOrDefaultAsync();
        if (otherMatch != null)
        {
            var matchedSwipe = likedSwipes.FirstOrDefault(ls => ls.MovieId == otherMatch.MovieId);
            if (matchedSwipe == null)
                return (false, null);

            command.CommandText = "UPDATE MatchSessions SET User2Id = @user2Id, MatchedMovieId = @matchedMovieId, IsFriendSession = 0, CreatedAt = @createdAt WHERE Id = @sessionId";
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@user2Id", otherMatch.UserId));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@matchedMovieId", matchedSwipe.MovieId));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@createdAt", DateTime.UtcNow));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@sessionId", session.Id));
            await command.ExecuteNonQueryAsync();

            return (true, matchedSwipe);
        }

        var random = new Random();
        if (random.Next(10) < 1 && likedSwipes.Count > 0)
        {
            var fallbackSwipe = likedSwipes[random.Next(likedSwipes.Count)];

            command.CommandText = "UPDATE MatchSessions SET User2Id = @user2Id, MatchedMovieId = @matchedMovieId, IsFriendSession = 0, CreatedAt = @createdAt WHERE Id = @sessionId";
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@user2Id", "fallback_match"));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@matchedMovieId", fallbackSwipe.MovieId));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@createdAt", DateTime.UtcNow));
            command.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@sessionId", session.Id));
            await command.ExecuteNonQueryAsync();
            return (true, fallbackSwipe);
        }

        return (false, null);
    }

    private async Task<List<string>> GetFriendIdsAsync(string userId)
    {
        return await _context.Friends
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId)
                        && f.Status == FriendStatus.Accepted)
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .ToListAsync();
    }

    private List<string> GetFriendIdsFromSession(MatchSession session, string userId)
    {
        var friends = new List<string>();
        if (session.User1Id == userId) friends.Add(session.User2Id);
        else if (session.User2Id == userId) friends.Add(session.User1Id);
        return friends;
    }
}
