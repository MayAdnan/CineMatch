using CineMatch.API.Data;
using CineMatch.API.Enums;
using CineMatch.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CineMatch.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FriendsController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FriendsController> _logger;

        public FriendsController(AppDbContext context, ILogger<FriendsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            var friends = await _context.Friends
                .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) &&
                            f.Status == FriendStatus.Accepted)
                .Include(f => f.Requester)
                .Include(f => f.Addressee)
                .Select(f => new
                {
                    friendId = f.RequesterId == userId ? f.AddresseeId : f.RequesterId,
                    friendEmail = f.RequesterId == userId ? (f.Addressee != null ? f.Addressee.Email : "Unknown") : (f.Requester != null ? f.Requester.Email : "Unknown"),
                    friendshipId = f.Id,
                    since = f.AcceptedAt
                })
                .ToListAsync();

            return Ok(friends);
        }

        [HttpPost("request/{friendEmail}")]
        public async Task<IActionResult> SendFriendRequest(string friendEmail)
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("SendFriendRequest called by userId: {userId}, friendEmail: {friendEmail}", userId, friendEmail);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            friendEmail = friendEmail?.Trim().ToLowerInvariant();
            _logger.LogInformation("Normalized friendEmail: {friendEmail}", friendEmail);

            if (string.IsNullOrWhiteSpace(friendEmail))
                return BadRequest("Email cannot be empty");

            var friend = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == friendEmail);
            _logger.LogInformation("Friend found: {found}, friendId: {friendId}", friend != null, friend?.Id);
            if (friend == null)
                return BadRequest("User not found");

            if (friend.Id == userId)
                return BadRequest("Cannot add yourself as a friend");

            var existingFriendship = await _context.Friends
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == userId && f.AddresseeId == friend.Id) ||
                    (f.RequesterId == friend.Id && f.AddresseeId == userId));
            _logger.LogInformation("Existing friendship: {existing}", existingFriendship != null);

            if (existingFriendship != null)
            {
                if (existingFriendship.Status == FriendStatus.Accepted)
                    return BadRequest("Already friends");

                if (existingFriendship.Status == FriendStatus.Pending)
                    return BadRequest("Friend request already sent");
            }

            var friendship = new Friend
            {
                Id = Guid.NewGuid().ToString(),
                RequesterId = userId,
                AddresseeId = friend.Id,
                Status = FriendStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };
            _logger.LogInformation("Creating friendship with Id: {id}", friendship.Id);

            _context.Friends.Add(friendship);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Friendship saved successfully");

            return Ok(new { message = "Friend request sent successfully" });
        }

        [HttpPost("accept/{friendshipId}")]
        public async Task<IActionResult> AcceptFriendRequest(string friendshipId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            if (string.IsNullOrWhiteSpace(friendshipId) || !Guid.TryParse(friendshipId, out _))
                return NotFound("Friend request not found");

            var friendship = await _context.Friends.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Friend request not found");

            if (friendship.AddresseeId != userId)
                return BadRequest("You can only accept requests sent to you");

            if (friendship.Status != FriendStatus.Pending)
                return BadRequest("Request is not pending");

            friendship.Status = FriendStatus.Accepted;
            friendship.AcceptedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend request accepted" });
        }

        [HttpPost("decline/{friendshipId}")]
        public async Task<IActionResult> DeclineFriendRequest(string friendshipId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            if (string.IsNullOrWhiteSpace(friendshipId))
                return BadRequest("Invalid friendship ID");

            var friendship = await _context.Friends.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Friend request not found");

            if (friendship.AddresseeId != userId)
                return BadRequest("You can only decline requests sent to you");

            if (friendship.Status != FriendStatus.Pending)
                return BadRequest("Request is not pending");

            friendship.Status = FriendStatus.Declined;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend request declined" });
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetPendingRequests()
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("GetPendingRequests called by userId: {userId}", userId);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            var requests = await _context.Friends
                .Where(f => f.AddresseeId == userId && f.Status == FriendStatus.Pending)
                .Include(f => f.Requester)
                .Select(f => new
                {
                    friendshipId = f.Id,
                    requesterEmail = f.Requester != null ? f.Requester.Email : "Unknown",
                    requestedAt = f.RequestedAt
                })
                .ToListAsync();
            _logger.LogInformation("Found {count} pending requests for userId: {userId}", requests.Count, userId);

            return Ok(requests);
        }

        [HttpDelete("{friendshipId}")]
        public async Task<IActionResult> RemoveFriend(string friendshipId)
        {
            var userId = GetCurrentUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Authentication required");

            if (string.IsNullOrWhiteSpace(friendshipId))
                return BadRequest("Invalid friendship ID");

            var friendship = await _context.Friends.FindAsync(friendshipId);
            if (friendship == null)
                return NotFound("Friendship not found");

            if (friendship.RequesterId != userId && friendship.AddresseeId != userId)
                return BadRequest("You can only remove your own friendships");

            _context.Friends.Remove(friendship);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Friend removed successfully" });
        }
    }
}
