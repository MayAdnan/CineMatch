namespace CineMatch.API.Models
{
    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public double VoteAverage { get; set; }
        public int Runtime { get; set; }
        public List<int> GenreIds { get; set; } = new List<int>();
    }
}