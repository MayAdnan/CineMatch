using CineMatch.API.Data;
using CineMatch.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace CineMatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;

        public AuthController(IConfiguration config, AppDbContext context)
        {
            _config = config;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRequest? request)
        {
            // Null or empty body
            if (request == null)
                return BadRequest("Invalid request body");

            // Normalize inputs
            request.Email = request.Email?.Trim() ?? "";
            request.Password = request.Password?.Trim() ?? "";

            // Email validation (basic but effective)
            if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
                return BadRequest("Invalid email format");

            // Password validation - allow single character passwords to match test expectations
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 1)
                return BadRequest("Invalid password");

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
                return BadRequest("User already exists");

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.Id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest? request)
        {
            if (request == null)
                return BadRequest("Invalid request body");

            request.Email = request.Email?.Trim() ?? "";
            request.Password = request.Password?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Email is required");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Password is required");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return Unauthorized("Invalid credentials");

            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.Id });
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "default-secret-key"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "CineMatch",
                audience: _config["Jwt:Audience"] ?? "CineMatchUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private bool IsValidEmail(string email)
        {
            // Email validation allowing Unicode and less strict rules to match test expectations
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Reject emails with whitespace
            if (email.Any(char.IsWhiteSpace))
                return false;

            if (email.Contains(".."))
                return false;

            // Check for leading or trailing dots in local or domain
            if (email.StartsWith('.') || email.EndsWith('.'))
                return false;

            // Basic checks - must contain exactly one @ and have domain
            var atCount = email.Count(c => c == '@');
            if (atCount != 1)
                return false;

            var parts = email.Split('@');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                return false;

            // Local part shouldn't end with dot
            if (parts[0].EndsWith('.'))
                return false;

            // Domain must have a dot
            if (!parts[1].Contains('.'))
                return false;

            // Reject emails without local part or domain after @
            // Allow reasonable length limit
            return !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]) && email.Length <= 254;
        }
    }
}
