using CineMatch.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CineMatchTests.TestHelpers
{
    public class CustomWebApplicationFactory<TProgram>
        : WebApplicationFactory<TProgram> where TProgram : class
    {
        private SqliteConnection _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                config.AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: false);
            });

            builder.ConfigureServices(services =>
            {
                // Open in-memory SQLite
                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                // Register DbContext with SQLite
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Build a temporary provider to ensure DB is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureDeleted();   // Clean slate for each test run
                db.Database.EnsureCreated();

                // Get configuration once
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var keyBytes = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
                var key = new SymmetricSecurityKey(keyBytes) { KeyId = "test-key" };

                // Override JWT options for testing
                services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = config["Jwt:Issuer"],
                            ValidAudience = config["Jwt:Audience"],
                            IssuerSigningKey = key
                        };
                    });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection?.Close();
                _connection?.Dispose();
            }
        }

        public string GenerateJwtToken(string userId, string email)
        {
            var keyBytes = Encoding.UTF8.GetBytes("your-super-secret-key-here-make-it-long-and-secure-for-testing-purposes-only");
            var key = new SymmetricSecurityKey(keyBytes) { KeyId = "test-key" };
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "CineMatch",
                audience: "CineMatchUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddYears(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
