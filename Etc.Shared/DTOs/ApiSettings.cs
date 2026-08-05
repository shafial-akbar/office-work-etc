namespace Etc.Shared.DTOs
{
    public class ApiSettings
    {
        public string BaseUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int TokenExpiryHours { get; set; } = 12;

    }
}
