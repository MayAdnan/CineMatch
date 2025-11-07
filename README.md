# CineMatch.API

CineMatch.API is a RESTful API built with ASP.NET Core that powers a movie matching application. It allows users to create accounts, form friendships, and swipe through movies to find matching recommendations with friends.

## Features

- **User Authentication**: JWT-based authentication with registration and login endpoints
- **Friend Management**: Send friend requests, accept/decline requests, and view friends list
- **Movie Swiping**: Create swipe sessions with friends or for personal use, swipe through movies
- **Matching Logic**: Advanced matching algorithm that finds mutual likes between users
- **TMDB Integration**: Fetches movie data from The Movie Database (TMDB) API
- **Session Management**: Create and manage swipe sessions with configurable parameters

## Tech Stack

- **Framework**: ASP.NET Core 9.0
- **Language**: C#
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: JWT Bearer Tokens
- **API Documentation**: Swagger/OpenAPI
- **External APIs**: TMDB (The Movie Database)

## Key Dependencies

- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication
- `Microsoft.EntityFrameworkCore.SqlServer` - SQL Server database provider
- `BCrypt.Net-Next` - Password hashing
- `Swashbuckle.AspNetCore` - Swagger documentation
- `Microsoft.AspNetCore.Cors` - CORS support

## Project Structure

```
CineMatch.API/
├── Controllers/          # API controllers (Auth, Friends, Movies, Swipe)
├── Models/              # Data models (User, Movie, Friend, etc.)
├── Services/            # Business logic services (MatchService, TmdbService)
├── Data/                # Database context and migrations
├── Enums/               # Application enums
└── Properties/          # Launch settings
```

## API Endpoints

### Authentication

- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login user

### Friends

- `GET /api/friends` - Get user's friends list
- `POST /api/friends/request/{email}` - Send friend request
- `POST /api/friends/accept/{id}` - Accept friend request
- `POST /api/friends/decline/{id}` - Decline friend request
- `GET /api/friends/requests` - Get pending friend requests
- `DELETE /api/friends/{id}` - Remove friendship

### Movies

- `GET /api/movies/discover` - Discover movies from TMDB

### Swipe

- `POST /api/swipe/session` - Create new swipe session
- `GET /api/swipe/session/{id}` - Get session details
- `POST /api/swipe` - Submit movie swipe

## Setup and Installation

### Prerequisites

- .NET 9.0 SDK
- SQL Server (or SQL Server LocalDB for development)
- TMDB API Key (get one at https://www.themoviedb.org/settings/api)

### Configuration

1. Clone the repository
2. Navigate to the `CineMatch.API` directory
3. Update `appsettings.json` with your configuration:
   - Set your TMDB API key in `TMDB:ApiKey`
   - Configure JWT settings if needed
   - Update the database connection string for your environment

### Database Setup

1. Run Entity Framework migrations to create the database:
   ```
   dotnet ef database update
   ```

### Running the Application

1. Restore dependencies:

   ```
   dotnet restore
   ```

2. Run the application:
   ```
   dotnet run
   ```

The API will be available at `https://localhost:5001` (or the port configured in launch settings).

### API Documentation

When running, navigate to `https://localhost:5001/swagger` to access the Swagger UI for interactive API documentation.

## Testing

The project includes comprehensive unit and integration tests located in the `CineMatchTests` project.

To run tests:

```
dotnet test
```

## CORS Configuration

The API is configured to allow requests from `http://localhost:5173` (default Vite dev server port). Update the CORS policy in `Program.cs` if your frontend runs on a different origin.

## Environment Configurations

- `appsettings.json` - Production settings
- `appsettings.Development.json` - Development overrides
- `appsettings.Test.json` - Test environment settings

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

This project is licensed under the MIT License.
