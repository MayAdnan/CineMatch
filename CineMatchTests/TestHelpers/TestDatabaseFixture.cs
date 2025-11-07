using CineMatch.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CineMatchTests.TestHelpers
{
    public class TestDatabaseFixture : IDisposable
    {
        public AppDbContext Context { get; private set; }

        public TestDatabaseFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            Context = new AppDbContext(options);
            Context.Database.OpenConnection();

            // Apply migrations for tests
            Context.Database.EnsureCreated();

            // Suppress migration warnings for tests
            Context.ChangeTracker.AutoDetectChangesEnabled = false;
            Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public void Dispose()
        {
            Context.Database.CloseConnection();
            Context.Dispose();
        }
    }
}