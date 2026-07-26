using System.Windows.Controls;

namespace ModTogether.API
{
    public interface IModPlugin
    {
        string Name { get; }
        string TargetGame { get; }
        string Version { get; }
        string Description { get; }
        string Author { get; }
        string NavigationIcon { get; }
        
        void Initialize(string gameDirectory);
        void SetLanguage(string language);
        Page CreatePage();
        bool IsValidGameDirectory(string gameDirectory);
    }
}

