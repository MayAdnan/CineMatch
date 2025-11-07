using System;
using System.ComponentModel.DataAnnotations;

namespace CineMatch.API.Models
{
    public class MatchSession
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string User1Id { get; set; }
        public string? User2Id { get; set; }
        public int? MatchedMovieId { get; set; } = null;
        public bool IsFriendSession { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}   