using System.ComponentModel.DataAnnotations;

namespace CineMatch.API.Models
{
    public class User
    {
        [Key]
        public required string Id { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}