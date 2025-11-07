using CineMatch.API.Models;
using Xunit;

namespace CineMatchTests.UnitTests.Models
{
    public class UserTests
    {
        [Fact]
        public void User_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "password123" };
            var now = DateTime.UtcNow;

            // Assert
            Assert.Equal("test-id", user.Id);
            Assert.Equal("test@example.com", user.Email);
            Assert.Equal("password123", user.Password);
        }

        [Fact]
        public void User_ShouldBeEqualToItself()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "password123" };

            // Act & Assert
            Assert.Equal(user, user);
            Assert.NotNull(user);
        }

        [Fact]
        public void User_ShouldHandleEmptyStringsForRequiredProperties()
        {
            // Arrange
            var user = new User { Id = "", Email = "", Password = "" };

            // Assert
            Assert.Equal("", user.Id);
            Assert.Equal("", user.Email);
            Assert.Equal("", user.Password);
        }

        [Fact]
        public void User_ShouldHandleNullStringsForRequiredProperties()
        {
            // Arrange
            var user = new User { Id = null!, Email = null!, Password = null! };

            // Assert - Properties are set even if null (since they're required but nullable)
            Assert.Null(user.Id);
            Assert.Null(user.Email);
            Assert.Null(user.Password);
        }

        [Fact]
        public void User_ShouldInitializeWithConstructor()
        {
            // Arrange
            var now = DateTime.UtcNow;

            // Act
            var user = new User
            {
                Id = "constructor-id",
                Email = "constructor@example.com",
                Password = "constructor-password"
            };

            // Assert
            Assert.Equal("constructor-id", user.Id);
            Assert.Equal("constructor@example.com", user.Email);
            Assert.Equal("constructor-password", user.Password);
        }

        [Fact]
        public void User_ShouldHandleSpecialCharactersInEmail()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test_user_123!@#", Password = "pass" };

            // Assert
            Assert.Equal("test_user_123!@#", user.Email);
        }

        [Fact]
        public void User_ShouldHandleLongStrings()
        {
            // Arrange
            var user = new User { Id = "id", Email = "email", Password = "pass" };
            var longString = new string('a', 1000);

            // Act
            user.Id = longString;
            user.Email = longString;
            user.Password = longString;

            // Assert
            Assert.Equal(longString, user.Id);
            Assert.Equal(longString, user.Email);
            Assert.Equal(longString, user.Password);
        }

        [Fact]
        public void User_ShouldHandleUnicodeCharacters()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "тест@пример.рф", Password = "пароль" };

            // Assert
            Assert.Equal("тест@пример.рф", user.Email);
            Assert.Equal("пароль", user.Password);
        }

        [Fact]
        public void User_Id_ShouldHandleGuidFormat()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid().ToString(), Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.True(Guid.TryParse(user.Id, out _));
        }

        [Fact]
        public void User_Email_ShouldHandleVariousFormats()
        {
            // Arrange
            var emails = new[]
            {
                "test@example.com",
                "user.name+tag@domain.co.uk",
                "123@test-domain.org"
            };

            // Act & Assert
            foreach (var email in emails)
            {
                var user = new User { Id = "id", Email = email, Password = "pass" };
                Assert.Equal(email, user.Email);
            }
        }

        [Fact]
        public void User_ShouldHandleConcurrentPropertyAccess()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "password" };

            // Act
            Parallel.For(0, 10, i =>
            {
                user.Id = $"id-{i}";
                user.Email = $"email-{i}@example.com";
                user.Password = $"password-{i}";
            });

            // Assert - Just verify no exceptions were thrown
            Assert.NotNull(user);
        }

        [Fact]
        public void User_ShouldPreserveDataIntegrity()
        {
            // Arrange
            var user = new User
            {
                Id = "original-id",
                Email = "original@example.com",
                Password = "original-password"
            };

            // Act - Simulate property modifications
            user.Email = "modified@example.com";
            user.Password = "modified-password";

            // Assert
            Assert.Equal("original-id", user.Id);
            Assert.Equal("modified@example.com", user.Email);
            Assert.Equal("modified-password", user.Password);
        }

        [Fact]
        public void User_Id_ShouldBeCaseSensitive()
        {
            // Arrange
            var user = new User { Id = "TestId", Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("TestId", user.Id);
            Assert.NotEqual("testid", user.Id);
        }

        [Fact]
        public void User_Email_ShouldBeCaseSensitive()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "Test@Example.Com", Password = "pass" };

            // Assert
            Assert.Equal("Test@Example.Com", user.Email);
            Assert.NotEqual("test@example.com", user.Email);
        }

        [Fact]
        public void User_ShouldHandleJsonSerialization()
        {
            // Arrange
            var user = new User
            {
                Id = "json-id",
                Email = "json@example.com",
                Password = "json-password"
            };

            // Act
            var json = System.Text.Json.JsonSerializer.Serialize(user);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<User>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(user.Id, deserialized.Id);
            Assert.Equal(user.Email, deserialized.Email);
            Assert.Equal(user.Password, deserialized.Password);
        }

        [Fact]
        public void User_ShouldHandleComplexEmailFormats()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "pass" };
            var complexEmails = new[]
            {
                "user_name.123@test-domain.org",
                "test-user+tag@example.co.uk",
                "Email.Address_123!@#$%^&*()@sub.domain.com"
            };

            // Act & Assert
            foreach (var email in complexEmails)
            {
                user.Email = email;
                Assert.Equal(email, user.Email);
            }
        }

        [Fact]
        public void User_ShouldHandleEmptyGuidStrings()
        {
            // Arrange
            var user = new User { Id = Guid.Empty.ToString(), Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("00000000-0000-0000-0000-000000000000", user.Id);
            Assert.True(Guid.TryParse(user.Id, out var parsedGuid));
            Assert.Equal(Guid.Empty, parsedGuid);
        }

        [Fact]
        public void User_ShouldHandlePasswordWithSpecialCharacters()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "P@ssw0rd!@#$%^&*()" };

            // Assert
            Assert.Equal("P@ssw0rd!@#$%^&*()", user.Password);
        }

        [Fact]
        public void User_ShouldHandleVeryLongPasswords()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = new string('*', 10000) };

            // Assert
            Assert.Equal(10000, user.Password.Length);
            Assert.All(user.Password, c => Assert.Equal('*', c));
        }

        [Fact]
        public void User_ShouldHandlePasswordWithUnicode()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "пароль_с_русскими_символами_!@#" };

            // Assert
            Assert.Equal("пароль_с_русскими_символами_!@#", user.Password);
        }

        [Fact]
        public void User_ShouldHandleMinimalValidData()
        {
            // Arrange
            var user = new User { Id = "1", Email = "a@b.c", Password = "x" };

            // Assert
            Assert.Equal("1", user.Id);
            Assert.Equal("a@b.c", user.Email);
            Assert.Equal("x", user.Password);
        }

        [Fact]
        public void User_ShouldHandleMaximumReasonableData()
        {
            // Arrange
            var user = new User
            {
                Id = new string('a', 255),
                Email = new string('b', 254) + "@example.com",
                Password = new string('c', 1000)
            };

            // Assert
            Assert.Equal(255, user.Id.Length);
            Assert.StartsWith(new string('b', 254), user.Email);
            Assert.EndsWith("@example.com", user.Email);
            Assert.Equal(1000, user.Password.Length);
        }

        [Fact]
        public void User_ShouldHandleEmailWithMultipleAtSymbols()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "user@domain@subdomain.com", Password = "pass" };

            // Assert - Even if invalid email format, string should be stored
            Assert.Equal("user@domain@subdomain.com", user.Email);
        }

        [Fact]
        public void User_ShouldHandleIdWithSpecialCharacters()
        {
            // Arrange
            var user = new User { Id = "id-with!@#$%^&*()_+-=[]{}|;:,.<>?", Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("id-with!@#$%^&*()_+-=[]{}|;:,.<>?", user.Id);
        }

        [Fact]
        public void User_ShouldHandlePasswordWithWhitespaces()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "password with spaces" };

            // Assert
            Assert.Equal("password with spaces", user.Password);
        }

        [Fact]
        public void User_ShouldHandlePasswordAsEmptyString()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "" };

            // Assert
            Assert.Equal("", user.Password);
        }

        [Fact]
        public void User_ShouldHandlePasswordAsWhitespaceOnly()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "   \t\n  " };

            // Assert
            Assert.Equal("   \t\n  ", user.Password);
        }

        [Fact]
        public void User_ShouldHandleEmailWithWhitespaces()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = " test@example.com ", Password = "pass" };

            // Assert
            Assert.Equal(" test@example.com ", user.Email);
        }

        [Fact]
        public void User_ShouldHandleIdAsNumbersOnly()
        {
            // Arrange
            var user = new User { Id = "1234567890", Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("1234567890", user.Id);
        }

        [Fact]
        public void User_ShouldHandleIdAsNegativeNumberString()
        {
            // Arrange
            var user = new User { Id = "-123", Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("-123", user.Id);
        }

        [Fact]
        public void User_ShouldHandleEmailWithoutDomain()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "user@", Password = "pass" };

            // Assert
            Assert.Equal("user@", user.Email);
        }

        [Fact]
        public void User_ShouldHandleEmailWithMultipleDots()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "user@domain.co.uk.org", Password = "pass" };

            // Assert
            Assert.Equal("user@domain.co.uk.org", user.Email);
        }

        [Fact]
        public void User_ShouldHandlePasswordWithNewlines()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@example.com", Password = "password\nwith\nnewlines" };

            // Assert
            Assert.Equal("password\nwith\nnewlines", user.Password);
        }

        [Fact]
        public void User_ShouldHandleIdWithNewlines()
        {
            // Arrange
            var user = new User { Id = "id\nwith\nnewlines", Email = "test@example.com", Password = "pass" };

            // Assert
            Assert.Equal("id\nwith\nnewlines", user.Id);
        }

        [Fact]
        public void User_ShouldHandleEmailWithNewlines()
        {
            // Arrange
            var user = new User { Id = "test-id", Email = "test@\nexample.com", Password = "pass" };

            // Assert
            Assert.Equal("test@\nexample.com", user.Email);
        }

        [Fact]
        public void User_ShouldInitializeWithDefaultConstructor_WhenRequiredPropertiesSet()
        {
            // Arrange & Act
            var user = new User { Id = "test", Email = "test@example.com", Password = "password" };

            // Assert
            Assert.IsType<User>(user);
            Assert.Equal("test", user.Id);
            Assert.Equal("test@example.com", user.Email);
            Assert.Equal("password", user.Password);
        }
    }
}