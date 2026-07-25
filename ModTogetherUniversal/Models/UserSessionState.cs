namespace ModTogetherUniversal.Models
{
    public class UserSessionState
    {
        public string SelectedRoomPreset { get; set; } = "[Off / Default Mods]";
        public string SelectedExplorerPreset { get; set; } = "[Off / Default Mods]";
        public int ExplorerInstallTypeIndex { get; set; } = 0;
        public string HostUsername { get; set; } = "";
        public string HostCustomPort { get; set; } = "52100";
        public bool HostToggleCustomPort { get; set; } = false;
        public string ClientIp { get; set; } = "";
        public string ClientPin { get; set; } = "";
    }
}
