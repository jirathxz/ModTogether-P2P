using System.Collections.Generic;

namespace ModTogetherMHW.Models
{
    public class ModState
    {
        public Dictionary<string, List<string>> InstalledMods { get; set; } = new();
    }
}
