using System;
using System.IO;
using System.Text.Json;
using ModTogetherUniversal.Models;

namespace ModTogetherUniversal.Services
{
    public class SessionManager
    {
        private static SessionManager? _instance;
        public static SessionManager Instance => _instance ??= new SessionManager();

        private readonly string _sessionFilePath;
        public UserSessionState State { get; private set; }

        public SessionManager()
        {
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var sessionFolder = Path.Combine(docsPath, "ModTogether", "Sessions");
            Directory.CreateDirectory(sessionFolder);
            _sessionFilePath = Path.Combine(sessionFolder, "session.json");
            State = Load();
        }

        private UserSessionState Load()
        {
            if (File.Exists(_sessionFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_sessionFilePath);
                    return JsonSerializer.Deserialize<UserSessionState>(json) ?? new UserSessionState();
                }
                catch
                {
                    return new UserSessionState();
                }
            }
            return new UserSessionState();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_sessionFilePath, json);
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"⚠️ Error saving session state: {ex.Message}");
            }
        }
    }
}
