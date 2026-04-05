namespace BikeFitness.Shared.Models
{
    public class StravaToken
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public long ExpiresAt { get; set; } // Unix timestamp
    }
}
