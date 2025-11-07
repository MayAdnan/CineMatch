namespace CineMatch.API.Models
{
    public class CreateSessionRequest
    {
        public bool IsFriendSession { get; set; }
        public string? FriendId { get; set; }
        public string? FriendEmail { get; set; }
    }
}