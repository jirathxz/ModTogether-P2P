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
        Page? CreatePage(); // Nullable: proxy implementations may return null if method is missing
        bool IsValidGameDirectory(string gameDirectory);
    }
}

