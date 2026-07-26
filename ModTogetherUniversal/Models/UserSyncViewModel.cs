using System;
using System.Linq;
using System.Windows.Media;

namespace ModTogetherUniversal.Models
{
    public class UserSyncViewModel
    {
        public string Username { get; set; } = string.Empty;
        public bool IsSynced { get; set; }
        public int SyncProgress { get; set; }
        public string CurrentActivity { get; set; } = string.Empty;
        public int PingMs { get; set; }

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Username)) return "?";
                var cleanName = Username.Replace("(Host)", "").Trim();
                if (string.IsNullOrWhiteSpace(cleanName)) return "?";
                return cleanName.Substring(0, Math.Min(2, cleanName.Length)).ToUpperInvariant();
            }
        }

        public Brush AvatarColor
        {
            get
            {
                var colors = new[] { 
                    "#3B82F6", "#EF4444", "#10B981", "#F59E0B", "#8B5CF6", "#EC4899", "#14B8A6" 
                };
                int index = Math.Abs(Username.GetHashCode()) % colors.Length;
                return (Brush)new BrushConverter().ConvertFromString(colors[index])!;
            }
        }

        public Brush PingColor
        {
            get
            {
                if (PingMs <= 0 && Username.Contains("(Host)")) return Brushes.LightGreen;
                if (PingMs < 80) return Brushes.LightGreen;
                if (PingMs < 150) return Brushes.Gold;
                return Brushes.OrangeRed;
            }
        }

        public System.Windows.Visibility ManagementVisibility
        {
            get
            {
                bool isHost = App.Server != null && App.Server.IsRunning;
                return (isHost && !Username.Contains("(Host)")) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }
    }
}
