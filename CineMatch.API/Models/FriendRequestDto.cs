namespace CineMatch.API.Models
{
    public class FriendRequestDto
    {
        public string Id { get; set; }
        public string RequesterEmail { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
