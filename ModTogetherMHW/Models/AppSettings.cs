namespace ModTogetherMHW.Models
{
    public class AppSettings
    {
        public string MhwDirectory { get; set; } = "";
        public bool AutoEnableMods { get; set; } = true;
        public string Language { get; set; } = "en";
        public string Theme { get; set; } = "System"; // Light, Dark, System
    }
}
