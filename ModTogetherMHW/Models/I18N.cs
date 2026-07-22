using System.Collections.Generic;

namespace ModTogetherMHW.Models
{
    public static class I18N
    {
        public static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            {
                "th", new Dictionary<string, string>
                {
                    {"title", "ModTogether - MHW P2P Mod Manager"},
                    {"game_dir", "โฟลเดอร์เกม MHW:"},
                    {"placeholder_dir", "เลือกโฟลเดอร์ Monster Hunter World..."},
                    {"btn_select_folder", "เลือกโฟลเดอร์"},
                    {"auto_enable", "เปิดใช้งาน Mod ที่โหลดมาอัตโนมัติ"},
                    
                    {"tab_room", "สร้าง / เข้าร่วมห้อง (Room)"},
                    {"tab_host", "โฮสต์ห้อง"},
                    {"tab_client", "เข้าร่วมห้อง"},
                    {"tab_manager", "จัดการม็อด"},
                    {"tab_recovery", "กู้คืนม็อด"},
                    {"tab_settings", "ตั้งค่า"},
                    
                    {"host_title", "สร้างห้อง (Host)"},
                    {"host_pin", "PIN จะปรากฏขึ้นหลังจากเริ่มห้อง"},
                    {"host_port", "พอร์ต (ค่าเริ่มต้น: 52100)"},
                    {"btn_host", "เริ่มเปิดห้อง"},
                    {"btn_stop_host", "ปิดห้อง (Stop Session)"},
                    {"btn_kill_host", "ปิดห้องเก่า"},
                    {"copy_ip", "คัดลอก IP"},
                    {"copy_pin", "คัดลอก PIN"},
                    
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
                    {"btn_backup", "สำรองข้อมูล nativePC"},
                    
                    {"btn_install_checked", "ติดตั้งที่เลือก"},
                    {"btn_uninstall_checked", "ถอดถอนที่เลือก"},
                    {"btn_delete_checked", "ลบที่เลือก"},
                    
                    {"tree_title", "ไฟล์ในม็อด (nativePC)"},
                    {"tree_header", "ไฟล์ / โฟลเดอร์"},
                    
                    {"info_default", "กรุณาเลือกม็อดจากรายชื่อด้านซ้ายเพื่อดูรายละเอียด"},
                    {"btn_install_mod", "ติดตั้งม็อดนี้"},
                    {"btn_uninstall_mod", "ถอดถอนม็อดนี้"},
                    {"btn_delete_mod", "ลบม็อดนี้"},
                    
                    {"lbl_users", "👥 ผู้ใช้งานในห้อง: -"},
                    {"lbl_upload", "อัปโหลด"},
                    {"lbl_download", "ดาวน์โหลด"},
                    {"lbl_install", "ติดตั้ง"},
                    {"btn_disconnect", "ยกเลิกการเชื่อมต่อ"},
                    {"btn_clear_log", "ล้าง Log"},
                    
                    {"legend_installed", "ติดตั้งแล้ว"},
                    {"legend_not_installed", "ยังไม่ติดตั้ง"},
                    {"legend_conflict", "อาจมีไฟล์ทับกัน"},
                    {"btn_check_update", "เช็คอัพเดท"},
                    
                    {"btn_reset_path", "รีเซ็ต Path"},
                    {"err_invalid_dir_reset", "ไม่พบ MonsterHunterWorld.exe หรือโฟลเดอร์เกมไม่ถูกต้อง Path ถูกรีเซ็ตแล้ว กรุณาเลือกโฟลเดอร์เกมใหม่"},
                    {"title_path_error", "ข้อผิดพลาด Game Path"},
                    {"desc_game_dir", "เลือกโฟลเดอร์ที่เป็นที่ตั้งของไฟล์ MonsterHunterWorld.exe"},
                    {"desc_auto_enable", "ติดตั้งม็อดเข้าเครื่องอัตโนมัติทันทีที่ดาวน์โหลดจาก Host เสร็จ"},
                    {"lbl_language", "ภาษา (Language)"},
                    {"desc_language", "เปลี่ยนภาษาที่ใช้แสดงผลภายในโปรแกรม"},
                    {"lbl_app_update", "อัปเดตแอปพลิเคชัน"},
                    {"desc_update", "ตรวจสอบ ModTogether เวอร์ชันใหม่ล่าสุดจาก GitHub"},
                    {"lbl_theme", "ธีมหน้าต่าง / สี"},
                    {"desc_theme", "ปรับเปลี่ยนธีมและสีของตัวโปรแกรม"},
                    {"theme_light", "สว่าง (Light)"},
                    {"theme_dark", "มืด (Dark)"},
                    {"theme_system", "ตามระบบ (System)"}
                }
            },
            {
                "en", new Dictionary<string, string>
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
                    
                    {"lbl_users", "👥 Connected Users: -"},
                    {"lbl_upload", "Upload"},
                    {"lbl_download", "Download"},
                    {"lbl_install", "Install"},
                    {"btn_disconnect", "Disconnect"},
                    {"btn_clear_log", "Clear Log"},
                    
                    {"legend_installed", "Installed"},
                    {"legend_not_installed", "Not Installed"},
                    {"legend_conflict", "Conflict"},
                    {"btn_check_update", "Check Update"},
                    
                    {"desc_game_dir", "Select the folder where MonsterHunterWorld.exe is located."},
                    {"desc_auto_enable", "Automatically install mods when downloading from Host."},
                    {"lbl_language", "Language"},
                    {"desc_language", "Change the application display language."},
                    {"lbl_app_update", "Application Updates"},
                    {"desc_update", "Check for the latest ModTogether versions on GitHub."},
                    {"lbl_theme", "Theme / Appearance"},
                    {"desc_theme", "Change the application color theme."},
                    {"theme_light", "Light"},
                    {"theme_dark", "Dark"},
                    {"theme_system", "System"}
                }
            }
        };

        public static string GetString(string key, string lang)
        {
            if (Translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }
            if (Translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            {
                return enVal;
            }
            return key;
        }
    }
}
