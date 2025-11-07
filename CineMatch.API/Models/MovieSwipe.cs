using System;
using System.ComponentModel.DataAnnotations;

namespace CineMatch.API.Models
{
    public class MovieSwipe
    {
        public string UserId { get; set; } = "";
        public int MovieId { get; set; }
        public bool IsLiked { get; set; }
        public string SessionId { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}