using System.Collections.Generic;
using System.Text;

namespace ModTogetherUniversal.Models
{
    public static class I18N
    {
        private static readonly Encoding Windows1252 = Encoding.GetEncoding(1252);
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        public static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            {
                "th", new Dictionary<string, string>
                {
                    {"title", "ModTogether - Universal Mod Explorer"},
                    {"game_dir", "ที่อยู่โฟลเดอร์เกม:"},
                    {"placeholder_dir", "เลือกโฟลเดอร์เกม..."},
                    {"btn_select_folder", "เลือกโฟลเดอร์"},
                    
                    {"tab_room", "ห้อง (สร้าง / เข้าร่วม)"},
                    {"tab_host", "โฮสต์ห้อง"},
                    {"tab_client", "เข้าร่วมห้อง"},
                    {"tab_manager", "จัดการม็อด (Mod Manager)"},
                    {"tab_recovery", "กู้คืนไฟล์เกม"},
                    {"tab_settings", "ตั้งค่า"},
                    
                    {"host_title", "สร้างห้อง (Host)"},
                    {"host_pin", "PIN จะปรากฏขึ้นหลังจากเริ่มห้อง"},
                    {"host_port", "พอร์ต (ค่าเริ่มต้น: 52100)"},
                    {"btn_host", "เริ่มเปิดห้อง"},
                    {"btn_stop_host", "ปิดห้อง (Stop Session)"},
                    {"btn_kill_host", "ปิดห้องเก่า"},
                    {"copy_ip", "คัดลอก IP"},
                    {"copy_pin", "คัดลอก PIN"},
                    {"host_upnp", "เปิดพอร์ตเร้าเตอร์อัตโนมัติ (UPnP)"},
                    {"host_preset_label", "โปรไฟล์ม็อด (Preset):"},
                    {"host_preset_save", "บันทึกโปรไฟล์"},
                    {"host_btn_kick", "เตะออก"},
                    {"host_btn_ban", "แบน"},
                    {"host_members", "👥 สมาชิกในเซสชัน"},
                    
                    {"client_title", "เข้าร่วมห้อง (Client)"},
                    {"client_ip", "Host IP (เช่น 192.168.1.5:52100)"},
                    {"client_pin", "PIN 6 หลัก"},
                    {"btn_join", "เข้าร่วม"},
                    {"btn_scan", "ค้นหาใน LAN"},
                    
                    {"lib_title", "รายชื่อม็อดในเครื่อง"},
                    {"search_placeholder", "ค้นหาม็อด..."},
                    {"btn_check_all", "เลือกทั้งหมด"},
                    {"btn_uncheck_all", "ยกเลิกเลือกทั้งหมด"},
                    {"btn_import", "นำเข้าม็อด"},
                    {"btn_refresh", "รีเฟรช"},
                    {"btn_open_folder", "เปิดโฟลเดอร์"},
                    
                    {"recovery_title", "รายชื่อม็อดที่อยู่ในถังขยะ (.recycle_mods)"},
                    {"btn_restore", "กู้คืน"},
                    {"btn_restore_all", "กู้คืนที่เลือก"},
                    {"btn_delete_permanently", "ลบถาวร"},
                    {"btn_delete_all_permanently", "ลบถาวรที่เลือก"},
                    
                    {"btn_validate", "ตรวจสอบไฟล์"},
                    {"btn_backup", "สำรองข้อมูล"},
                    
                    {"btn_delete_checked", "ลบที่เลือก"},
                    
                    {"tree_title", "ไฟล์ในม็อด"},
                    {"tree_header", "ไฟล์ / โฟลเดอร์"},
                    
                    {"info_default", "กรุณาเลือกม็อดจากรายชื่อด้านซ้ายเพื่อดูรายละเอียด"},
                    {"btn_delete_mod", "ลบม็อดนี้"},
                    
                    {"lbl_users", "👥 ผู้เล่นในห้อง: -"},
                    {"lbl_upload", "อัปโหลด"},
                    {"lbl_download", "ดาวน์โหลด"},
                    {"btn_disconnect", "ยกเลิกการเชื่อมต่อ"},
                    {"btn_clear_log", "ล้าง Log"},
                    
                    {"btn_check_update", "ตรวจสอบอัปเดต"},
                    
                    {"btn_reset_path", "รีเซ็ตที่อยู่"},
                    {"err_invalid_dir_reset", "โฟลเดอร์ที่เลือกไม่ถูกต้อง หรือไม่พบไฟล์เกม ระบบได้ทำการรีเซ็ตโฟลเดอร์เกมแล้ว"},
                    {"desc_game_dir", "เลือกโฟลเดอร์เกมที่ต้องการเก็บไฟล์ม็อด"},
                    {"lbl_language", "ภาษา (Language)"},
                    {"desc_language", "เปลี่ยนภาษาที่ใช้แสดงผลภายในโปรแกรม"},
                    {"lbl_app_update", "อัปเดตแอปพลิเคชัน"},
                    {"desc_update", "ตรวจสอบเวอร์ชันใหม่ของ ModTogether"},
                    {"lbl_theme", "ธีมหน้าต่าง / สี"},
                    {"desc_theme", "ปรับเปลี่ยนธีมและสีของตัวโปรแกรม"},
                    {"theme_light", "สว่าง (Light)"},
                    {"theme_dark", "มืด (Dark)"},
                    {"theme_system", "ตามระบบ (System)"},

                    {"tab_explorer", "จัดการม็อดทั่วไป (Mod Explorer)"},
                    {"tab_plugins", "ปลั๊กอิน (Plugins)"},
                    {"explorer_title", "Mod Explorer"},
                    {"explorer_desc", "เครื่องมือจัดการม็อดทั่วไป เลือก Mod Folder Path ในหน้า Settings แล้วเลือกวิธีติดตั้งม็อด"},
                    {"explorer_mod_folder_label", "Mod Folder Path:"},
                    {"explorer_no_mod_folder", "ยังไม่ได้ตั้งค่า — กรุณาไปตั้งใน Settings"},
                    {"explorer_install_type", "รูปแบบการติดตั้ง:"},
                    {"explorer_type_single", "ไฟล์เดียว (คัดลอกไฟล์ลงโฟลเดอร์ปลายทางโดยตรง)"},
                    {"explorer_type_extract", "แตกไฟล์ (แตกไฟล์ zip ลงในโฟลเดอร์ปลายทาง)"},
                    {"explorer_btn_open", "เปิดโฟลเดอร์เก็บม็อด"},
                    {"explorer_btn_refresh", "รีเฟรช"},
                    {"explorer_col_install", "ติดตั้ง"},
                    {"explorer_col_filename", "ชื่อไฟล์"},
                    {"explorer_col_size", "ขนาด"},
                    {"explorer_col_modified", "แก้ไขล่าสุด"},
                    
                    {"plugins_title", "ปลั๊กอิน (Plugins)"},
                    {"plugins_desc", "จัดการและโหลดปลั๊กอิน (Plugins) สำหรับเกมต่างๆ"},
                    {"plugins_btn_open", "เปิดโฟลเดอร์ปลั๊กอิน"},
                    {"plugins_btn_reload", "รีโหลดปลั๊กอิน"},

                    {"lbl_mod_dir", "โฟลเดอร์ม็อดทั่วไป (Mod Folder Path):"},
                    {"desc_mod_dir", "เลือกโฟลเดอร์ที่จะใช้ติดตั้งม็อดผ่าน Mod Explorer"},
                    {"placeholder_mod_dir", "เลือกโฟลเดอร์ม็อดหรือโฟลเดอร์เกม..."},
                    {"btn_select_mod_folder", "เลือกโฟลเดอร์"},
                    {"btn_reset_mod_path", "รีเซ็ต"},

                    {"lbl_debug_log", "บันทึกข้อมูล Debug (Debug Log)"},
                    {"desc_debug_log", "แสดงข้อความวิเคราะห์การทำงานระดับละเอียดใน Console"},
                    {"lbl_error_log", "บันทึกข้อผิดพลาดลงไฟล์ (Error Log)"},
                    {"desc_error_log", "บันทึกประวัติข้อผิดพลาดรุนแรงลงไฟล์ error.log อัตโนมัติ"},
                    {"lbl_plugin_security", "ความปลอดภัยของปลั๊กอิน (Plugin Security Inspection)"},
                    {"desc_plugin_security", "ตรวจสอบลายเซ็นดิจิทัล รหัส SHA-256 และสแกน API ที่เสี่ยงภัยก่อนโหลดปลั๊กอิน"},

                    {"plugins_online_title", "🛒 คลังปลั๊กอินออนไลน์ / ที่เก็บข้อมูล"},
                    {"plugins_installed_title", "🛡️ ปลั๊กอินที่ติดตั้ง & การตรวจสอบความปลอดภัย"},
                    {"plugins_btn_check_update", "ตรวจสอบ & อัปเดตทั้งหมด"},
                    {"plugins_btn_install", "ติดตั้งปลั๊กอิน"},
                    {"plugins_btn_delete", "ลบปลั๊กอิน"},
                    {"plugins_installed_badge", "✅ ติดตั้งแล้ว"},
                    {"plugins_not_installed_badge", "ยังไม่ได้ติดตั้ง"},
                    {"plugins_downloading", "กำลังดาวน์โหลด DLL จริง..."},
                    {"plugins_no_dll_notice", "⚠️ ไม่พบไฟล์ปลั๊กอิน (.dll) ใน GitHub Release เวอร์ชันล่าสุด (หรือไม่มีไฟล์ .dll ถูกแนบไว้บนหน้า Release)"},

                    {"explorer_btn_install_checked", "⚡ ติดตั้งที่เลือก"},
                    {"explorer_btn_uninstall_checked", "❎ ถอนที่เลือก"},
                    {"explorer_btn_check_all", "เลือกทั้งหมด"},
                    {"explorer_btn_uncheck_all", "ยกเลิกเลือก"},
                    {"explorer_btn_scan_conflicts", "สแกนไฟล์ทับซ้อน ⚔️"},
                    {"explorer_btn_import", "นำเข้าม็อด"},
                    {"explorer_btn_open_folder", "เปิดโฟลเดอร์ม็อด"},
                    {"explorer_btn_backup", "สำรองข้อมูลที่เลือก"},
                    {"explorer_btn_delete", "ลบที่เลือก"},
                    {"explorer_preset_label", "โปรไฟล์ม็อด Preset:"},
                    {"explorer_preset_save", "บันทึกโปรไฟล์"},
                    {"explorer_preset_delete", "ลบ"},
                    {"explorer_preset_name_placeholder", "ชื่อโปรไฟล์ใหม่..."},

                    {"preset_locked_title", "Preset ถูกล็อกขณะอยู่ในเซสชัน"},
                    {"preset_locked_msg", "⚠️ ไม่สามารถเปลี่ยน Mod Preset ขณะที่มีเซสชันกำลังใช้งาน (กำลังโฮสต์หรือเข้าร่วม)\n\nกรุณาหยุดโฮสต์หรือตัดการเชื่อมต่อจากห้องก่อนเพื่อป้องกันข้อผิดพลาดในการซิงค์ไฟล์"},

                    {"host_btn_unban", "ปลดแบน"},
                    {"lbl_bandwidth_limit", "จำกัดความเร็ว (Bandwidth Limiter)"},
                    {"desc_bandwidth_limit", "ตั้งค่าจำกัดความเร็วดาวน์โหลด/อัปโหลดสูงสุด (Kbps)"},
                    {"lbl_discord_rpc", "Discord Rich Presence (RPC)"},
                    {"desc_discord_rpc", "แสดงสถานะ ModTogether บนโปรไฟล์ Discord แบบ Real-time"}
                }
            },
            {
                "en", new Dictionary<string, string>
                {
                    {"title", "ModTogether - Universal Mod Explorer"},
                    {"game_dir", "Game Directory:"},
                    {"placeholder_dir", "Select game folder..."},
                    {"btn_select_folder", "Select Folder"},
                    {"btn_reset_path", "Reset Path"},
                    {"err_invalid_dir_reset", "The selected directory is invalid. Game path has been reset."},
                    {"title_path_error", "Game Path Error"},
                    
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
                    {"host_upnp", "Auto Port Forward (UPnP)"},
                    {"host_preset_label", "Modpack Preset:"},
                    {"host_preset_save", "Save Preset"},
                    {"host_btn_kick", "Kick"},
                    {"host_btn_ban", "Ban"},
                    {"host_members", "👥 Session Members"},
                    
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
                    {"btn_backup", "Backup Mods"},
                    
                    {"btn_delete_checked", "Delete Checked"},
                    
                    {"tree_title", "Mod Files (nativePC)"},
                    {"tree_header", "Files / Folders"},
                    
                    {"info_default", "Select a mod from the library to view details."},
                    {"btn_delete_mod", "Delete Mod"},
                    
                    {"lbl_users", "👥 Connected Users: -"},
                    {"lbl_upload", "Upload"},
                    {"lbl_download", "Download"},
                    {"btn_disconnect", "Disconnect"},
                    {"btn_clear_log", "Clear Log"},
                    
                    {"btn_check_update", "Check Update"},
                    
                    {"desc_game_dir", "Select the folder where you want to keep the downloaded mods."},
                    {"lbl_language", "Language"},
                    {"desc_language", "Change the application display language."},
                    {"lbl_app_update", "Application Updates"},
                    {"desc_update", "Check for the latest ModTogether versions on GitHub."},
                    {"lbl_theme", "Theme / Appearance"},
                    {"desc_theme", "Change the application color theme."},
                    {"theme_light", "Light"},
                    {"theme_dark", "Dark"},
                    {"theme_system", "System"},

                    {"tab_explorer", "Mod Explorer"},
                    {"tab_plugins", "Plugins"},
                    {"explorer_title", "Mod Explorer"},
                    {"explorer_desc", "Generic mod manager. Install mods from GameMods folder into the Mod Folder Path configured in Settings."},
                    {"explorer_mod_folder_label", "Mod Folder Path:"},
                    {"explorer_no_mod_folder", "Not configured — please set in Settings"},
                    {"explorer_install_type", "Install Type:"},
                    {"explorer_type_single", "Single File (Copy files directly)"},
                    {"explorer_type_extract", "Extract File (Extract archive into Target Path)"},
                    {"explorer_btn_open", "Open Mods Folder"},
                    {"explorer_btn_refresh", "Refresh"},
                    {"explorer_col_install", "Install"},
                    {"explorer_col_filename", "Filename"},
                    {"explorer_col_size", "Size"},
                    {"explorer_col_modified", "Modified"},
                    
                    {"plugins_title", "Plugins"},
                    {"plugins_desc", "Manage and load plugins for different games."},
                    {"plugins_btn_open", "Open Plugins Folder"},
                    {"plugins_btn_reload", "Reload Plugins"},

                    {"lbl_mod_dir", "Mod Folder Path:"},
                    {"desc_mod_dir", "Select the folder where Mod Explorer will install mods."},
                    {"placeholder_mod_dir", "Select mod or game folder..."},
                    {"btn_select_mod_folder", "Select Folder"},
                    {"btn_reset_mod_path", "Reset"},

                    {"lbl_debug_log", "Debug Logging"},
                    {"desc_debug_log", "Enable verbose debugging output in Console Log."},
                    {"lbl_error_log", "Error Log File Writing"},
                    {"desc_error_log", "Automatically write crash tracebacks to error.log."},
                    {"lbl_plugin_security", "Plugin Security Verification"},
                    {"desc_plugin_security", "Verify SHA-256 signatures and inspect plugin DLL bytecode before execution."},

                    {"plugins_online_title", "🛒 Online Plugin Store / Repository"},
                    {"plugins_installed_title", "🛡️ Installed Plugins & Security Inspector"},
                    {"plugins_btn_check_update", "Check & Update All"},
                    {"plugins_btn_install", "Install Plugin"},
                    {"plugins_btn_delete", "Delete Plugin"},
                    {"plugins_installed_badge", "✅ Installed"},
                    {"plugins_not_installed_badge", "Not Installed"},
                    {"plugins_downloading", "Downloading Real DLL..."},
                    {"plugins_no_dll_notice", "⚠️ No plugin files (.dll) found in the latest GitHub Release (or no .dll files were attached to the Release page)"},

                    {"explorer_btn_install_checked", "⚡ Install Checked"},
                    {"explorer_btn_uninstall_checked", "❎ Uninstall Checked"},
                    {"explorer_btn_check_all", "Check All"},
                    {"explorer_btn_uncheck_all", "Uncheck All"},
                    {"explorer_btn_scan_conflicts", "Scan Conflicts ⚔️"},
                    {"explorer_btn_import", "Import Mod"},
                    {"explorer_btn_open_folder", "Open Mods Folder"},
                    {"explorer_btn_backup", "Backup Checked"},
                    {"explorer_btn_delete", "Delete Checked"},
                    {"explorer_preset_label", "Mod Profile Preset:"},
                    {"explorer_preset_save", "Save Profile"},
                    {"explorer_preset_delete", "Delete"},
                    {"explorer_preset_name_placeholder", "New Profile Name..."},

                    {"preset_locked_title", "Preset Locked During Session"},
                    {"preset_locked_msg", "⚠️ Changing Mod Presets is strictly locked while an active room session is in progress (Hosting or Joined).\n\nPlease stop hosting or disconnect from the room first to prevent critical synchronization errors."},

                    {"host_btn_unban", "Unban"},
                    {"lbl_bandwidth_limit", "Bandwidth Limiter"},
                    {"desc_bandwidth_limit", "Set maximum download/upload speed limit (Kbps)"},
                    {"lbl_discord_rpc", "Discord Rich Presence (RPC)"},
                    {"desc_discord_rpc", "Show ModTogether status on Discord profile in real-time"}
                }
            }
        };

        public static string GetString(string key, string lang)
        {
            if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
            {
                return RepairMojibake(val);
            }
            if (Translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            {
                return RepairMojibake(enVal);
            }
            return key;
        }

        private static string RepairMojibake(string value)
        {
            for (var attempt = 0; attempt < 2 && LooksLikeMojibake(value); attempt++)
            {
                try
                {
                    var repaired = StrictUtf8.GetString(Windows1252.GetBytes(value));
                    if (repaired == value) break;
                    value = repaired;
                }
                catch (DecoderFallbackException)
                {
                    break;
                }
            }

            return value;
        }

        private static bool LooksLikeMojibake(string value) =>
            value.Contains('Ã') || value.Contains('Â') || value.Contains('â') || value.Contains('ð') || value.Contains('Å') || value.Contains('à');
    }
}
