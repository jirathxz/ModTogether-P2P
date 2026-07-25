using System.Windows.Controls;
using ModTogether.API;
using ModTogether.Extensions.MHW.Models;

namespace ModTogether.Extensions.MHW
{
    public static class App
    {
        public static AppSettings Settings = new AppSettings();
        public static ModTogether.Extensions.MHW.Services.ModInstaller Installer = new ModTogether.Extensions.MHW.Services.ModInstaller(Settings.Current.MhwDirectory);
        public static RealServerBridge Server = new RealServerBridge();
        public static RealClientBridge Client = new RealClientBridge();
    }

    public class RealServerBridge
    { 
        public bool IsRunning
        {
            get
            {
                try
                {
                    var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ModTogetherUniversal");
                    var appType = asm?.GetType("ModTogetherUniversal.App");
                    var serverProp = appType?.GetProperty("Server", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var serverObj = serverProp?.GetValue(null);
                    var isRunningProp = serverObj?.GetType().GetProperty("IsRunning");
                    return (bool)(isRunningProp?.GetValue(serverObj) ?? false);
                }
                catch { return false; }
            }
        }

        public RealDeletedModsBridge DeletedMods { get; } = new RealDeletedModsBridge();
        public string HostUsername => "Host";
        public void BroadcastModStateChange(string id, ModState state) { } 
    }

    public class RealDeletedModsBridge
    {
        public bool TryAdd(string key, string value)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ModTogetherUniversal");
                var appType = asm?.GetType("ModTogetherUniversal.App");
                var serverProp = appType?.GetProperty("Server", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var serverObj = serverProp?.GetValue(null);
                if (serverObj != null)
                {
                    var deletedModsProp = serverObj.GetType().GetProperty("DeletedMods");
                    var deletedModsObj = deletedModsProp?.GetValue(serverObj);
                    if (deletedModsObj != null)
                    {
                        var tryAddMethod = deletedModsObj.GetType().GetMethod("TryAdd", new[] { typeof(string), typeof(string) });
                        return (bool)(tryAddMethod?.Invoke(deletedModsObj, new object[] { key, value }) ?? false);
                    }
                }
            }
            catch { }
            return false;
        }
    }

    public class RealClientBridge
    { 
        public bool IsConnected => true; 

        public async System.Threading.Tasks.Task DeleteModAsync(string id)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ModTogetherUniversal");
                var appType = asm?.GetType("ModTogetherUniversal.App");
                var clientProp = appType?.GetProperty("Client", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var clientObj = clientProp?.GetValue(null);
                if (clientObj != null)
                {
                    var deleteMethod = clientObj.GetType().GetMethod("DeleteModAsync", new[] { typeof(string) });
                    if (deleteMethod != null)
                    {
                        var task = (System.Threading.Tasks.Task)deleteMethod.Invoke(clientObj, new object[] { id })!;
                        await task;
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RealClientBridge DeleteModAsync Error] {ex.Message}");
            }
        }

        public void ReportModStateChange(string id, ModState state) { } 
    }

    public class MainWindow
    { 
        public static MainWindow Instance = new MainWindow();
        public void Log(string msg)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ModTogetherUniversal");
                var mwType = asm?.GetType("ModTogetherUniversal.MainWindow");
                var instanceProp = mwType?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var mwObj = instanceProp?.GetValue(null);
                if (mwObj != null)
                {
                    var logMethod = mwType?.GetMethod("Log", new[] { typeof(string) });
                    logMethod?.Invoke(mwObj, new object[] { msg });
                    return;
                }
            }
            catch { }
            System.Diagnostics.Debug.WriteLine(msg);
        }

        public bool ValidateGamePath() { return true; }
        
        public void UpdateInstallProgress(int p)
        {
            try
            {
                var asm = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ModTogetherUniversal");
                var mwType = asm?.GetType("ModTogetherUniversal.MainWindow");
                var instanceProp = mwType?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var mwObj = instanceProp?.GetValue(null);
                if (mwObj != null)
                {
                    var progressMethod = mwType?.GetMethod("UpdateDownloadProgress", new[] { typeof(int) });
                    progressMethod?.Invoke(mwObj, new object[] { p });
                }
            }
            catch { }
        }

        public object? FindName(string name) { return null; }
    }
    
    namespace Models {
        public static class I18N {
            public static System.Action? OnLanguageChanged;
            
            public static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> Translations = new()
            {
                {
                    "th", new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"title", "ModTogether - MHW P2P Mod Manager"},
                        {"game_dir", "MHW Game Directory:"},
                        {"placeholder_dir", "Select Monster Hunter World folder..."},
                        {"btn_select_folder", "Select Folder"},
                        {"legend_installed", "à¸•à¸´à¸”à¸•à¸±à¹‰à¸‡à¹à¸¥à¹‰à¸§"},
                        {"legend_not_installed", "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¸•à¸´à¸”à¸•à¸±à¹‰à¸‡"},
                        {"legend_conflict", "à¸­à¸²à¸ˆà¸¡à¸µà¹„à¸Ÿà¸¥à¹Œà¸—à¸±à¸šà¸à¸±à¸™"},
                        {"btn_check_update", "à¹€à¸Šà¹‡à¸„à¸­à¸±à¸žà¹€à¸”à¸—"}
                    }
                },
                {
                    "en", new System.Collections.Generic.Dictionary<string, string>
                    {
                        {"title", "ModTogether - MHW P2P Mod Manager"},
                        {"game_dir", "MHW Game Directory:"},
                        {"placeholder_dir", "Select Monster Hunter World folder..."},
                        {"btn_select_folder", "Select Folder"},
                        {"btn_reset_path", "Reset Path"},
                        {"err_invalid_dir_reset", "MonsterHunterWorld.exe was not found or the game directory is invalid. Game path has been reset. Please select a new folder."},
                        {"title_path_error", "Game Path Error"},
                        {"auto_enable", "Auto Enable Downloaded Mods"},
                        
                        {"tab_room", "Room (Host / Join)"},
                        {"tab_host", "Host Room"},
                        {"tab_client", "Join Room"},
                        {"tab_manager", "Mod Manager"},
                        {"tab_recovery", "Recovery Mod"},
                        {"tab_settings", "Settings"},
                        
                        {"host_title", "Create Session (Host)"},
                        {"host_pin", "PIN will appear after hosting"},
                        {"host_port", "Port (Default: 52100)"},
                        {"btn_host", "Start Hosting"},
                        {"btn_stop_host", "Stop Session"},
                        {"btn_kill_host", "Kill Old Hosts"},
                        {"copy_ip", "Copy IP"},
                        {"copy_pin", "Copy PIN"},
                        
                        {"client_title", "Join Session (Client)"},
                        {"client_ip", "Host IP (e.g. 192.168.1.5:52100)"},
                        {"client_pin", "6-Digit PIN"},
                        {"btn_join", "Join"},
                        {"btn_scan", "Scan LAN"},
                        
                        {"lib_title", "Game Mods Library"},
                        {"search_placeholder", "Search mods..."},
                        {"btn_check_all", "Check All"},
                        {"btn_uncheck_all", "Uncheck All"},
                        {"btn_import", "Import Mod"},
                        {"btn_refresh", "Refresh Mods"},
                        {"btn_open_folder", "Open Folder"},
                        
                        {"recovery_title", "Recycled Mods Library (.recycle_mods)"},
                        {"btn_restore", "Restore"},
                        {"btn_restore_all", "Restore Checked"},
                        {"btn_delete_permanently", "Delete Permanently"},
                        {"btn_delete_all_permanently", "Delete Checked Permanently"},
                        
                        {"btn_validate", "Validate"},
                        {"btn_backup", "Backup nativePC"},
                        
                        {"btn_install_checked", "Install Checked"},
                        {"btn_uninstall_checked", "Uninstall Checked"},
                        {"btn_delete_checked", "Delete Checked"},
                        
                        {"tree_title", "Mod Files (nativePC)"},
                        {"tree_header", "Files / Folders"},
                        
                        {"info_default", "Select a mod from the library to view details."},
                        {"btn_install_mod", "Install Mod"},
                        {"btn_uninstall_mod", "Uninstall Mod"},
                        {"btn_delete_mod", "Delete Mod"},
                        
                        {"lbl_users", "ðŸ‘¥ Connected Users: -"},
                        {"lbl_upload", "Upload"},
                        {"lbl_download", "Download"},
                        {"lbl_install", "Install"},
                        {"btn_disconnect", "Disconnect"},
                        {"btn_clear_log", "Clear Log"},
                        
                        {"legend_installed", "Installed"},
                        {"legend_not_installed", "Not Installed"},
                        {"legend_conflict", "Conflict"},
                        {"btn_check_update", "Check Update"}
                    }
                }
            };

            public static string GetString(string key, string fallback = "") {
                string lang = App.Settings.Current.Language;
                if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
                    return val;
                if (Translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
                    return enVal;
                return string.IsNullOrEmpty(fallback) ? key : fallback;
            }
        }
    }

    public class AppSettings
    {
        public CurrentSettings Current = new CurrentSettings();
        public void Save() { } // Dummy save
    }
    
    public class CurrentSettings
    {
        public string GameDirectory { get; set; } = string.Empty;
        public string MhwDirectory => GameDirectory;
        public string Language { get; set; } = "en";
    }

    public class MhwModPlugin : IModPlugin
    {
        public string Name => "MHW Mod Manager";
        public string TargetGame => "Monster Hunter: World";
        public string Version => "1.2.0";
        public string Description => "A powerful mod manager for Monster Hunter: World";
        public string Author => "jirathxz";
        public string NavigationIcon => "Games24";

        public void Initialize(string gameDirectory)
        {
            App.Settings.Current.GameDirectory = gameDirectory;
        }
        
        public void SetLanguage(string language)
        {
            App.Settings.Current.Language = language;
            Models.I18N.OnLanguageChanged?.Invoke();
        }

        public Page CreatePage()
        {
            return new ManagerPage();
        }
    }
}

