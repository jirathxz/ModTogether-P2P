using System.Collections.Generic;

namespace ModTogether.Plugins.MHW.Models
{
    public class ModState
    {
        public Dictionary<string, List<string>> InstalledMods { get; set; } = new();
    }
}
