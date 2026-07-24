namespace ModTogetherUniversal.Models
{
    public class AppSettings
    {
        public string GameDirectory { get; set; } = "";
        public string ModDirectory { get; set; } = "";
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "System"; // Light, Dark, System
    }
}
