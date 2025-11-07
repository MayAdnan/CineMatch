using System;
using System.ComponentModel.DataAnnotations;
using CineMatch.API.Enums;

namespace CineMatch.API.Models
{
    public class Friend
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RequesterId { get; set; }
        public string AddresseeId { get; set; }
        public FriendStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public virtual User? Requester { get; set; }
        public virtual User? Addressee { get; set; }
    }
}