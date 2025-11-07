using CineMatch.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CineMatch.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<MovieSwipe> MovieSwipes { get; set; }
        public DbSet<MatchSession> MatchSessions { get; set; }
        public DbSet<Friend> Friends { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
                entity.Property(u => u.Password).IsRequired();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Configure MovieSwipe entity
            modelBuilder.Entity<MovieSwipe>(entity =>
            {
                entity.HasKey(s => new { s.UserId, s.MovieId, s.SessionId });
                entity.Property(s => s.UserId).IsRequired();
                entity.Property(s => s.MovieId).IsRequired();
                entity.Property(s => s.SessionId).IsRequired();
                entity.Property(s => s.IsLiked).IsRequired();
            });

            // Configure MatchSession entity
            modelBuilder.Entity<MatchSession>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.User1Id).IsRequired();
                // User2Id can be null for solo sessions
                entity.Property(s => s.CreatedAt).IsRequired();

                // Remove IsRequired from MatchedMovieId since it starts as 0
                entity.Property(s => s.MatchedMovieId);
            });

            // Configure Friend entity
            modelBuilder.Entity<Friend>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.RequesterId).IsRequired();
                entity.Property(f => f.AddresseeId).IsRequired();
                entity.Property(f => f.Status).IsRequired();
                entity.Property(f => f.RequestedAt).IsRequired();

                // Single unique index for friend relationships
                entity.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();

                // Configure relationships
                entity.HasOne(f => f.Requester)
                      .WithMany()
                      .HasForeignKey(f => f.RequesterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Addressee)
                      .WithMany()
                      .HasForeignKey(f => f.AddresseeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}