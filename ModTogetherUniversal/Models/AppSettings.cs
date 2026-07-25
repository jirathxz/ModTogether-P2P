namespace ModTogetherUniversal.Models
{
    public class AppSettings
    {
        public string GameDirectory { get; set; } = "";
        public string ModDirectory { get; set; } = "";
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "System"; // Light, Dark, System
        
        // Debug & Logging Controls (Disabled by default for Normal Users)
        public bool EnableDebugLog { get; set; } = false;
        public bool EnableErrorLog { get; set; } = false;

        // Security Controls
        public bool StrictPluginSecurity { get; set; } = true;
        public System.Collections.Generic.List<string> TrustedPluginHashes { get; set; } = new();

        // Bandwidth Control (0 = Unlimited)
        public int MaxDownloadSpeedKbps { get; set; } = 0;
        public int MaxUploadSpeedKbps { get; set; } = 0;

        // Multi-Game Profile Switching History
        public System.Collections.Generic.List<string> GamePathHistory { get; set; } = new();
    }
}
