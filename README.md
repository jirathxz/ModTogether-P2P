<div align="center">
  <h1>ModTogether Universal 🎮</h1>
  <p><strong>A fast, modular, and native Peer-to-Peer (P2P) mod synchronization & management tool for PC games.</strong></p>
  <p>โปรแกรมสำหรับการซิงค์และจัดการไฟล์ม็อด (Mod) ระหว่างเพื่อนแบบ P2P ที่รวดเร็ว ปลอดภัย และยืดหยุ่นด้วยระบบปลั๊กอิน (C# .NET 8 WPF)</p>
  <br>
  <a href="#-english-version">🇺🇸 English Version</a> &nbsp;|&nbsp; <a href="#-ภาษาไทย-thai-version">🇹🇭 ภาษาไทย (Thai Version)</a>
  <br><br>
  <img src="./images/preview.png" alt="ModTogether Preview" width="650">
</div>

---

## 🇺🇸 English Version

> [!WARNING]
> ## Disclaimer
> This application is **NOT** a cheat, hack, or memory injection tool. It is strictly a "File Synchronization & Mod Management Tool" designed to copy mod files over a network between peers. Its sole purpose is to facilitate Co-op gaming by ensuring all players have identical mod files. It does not interact with game memory or bypass Anti-Cheat systems. The mechanism is functionally identical to sending files via Google Drive or Zip archives over chat, but fully automated and direct.

### 📦 Modular Architecture & Game Plugins

ModTogether is designed as a **Universal P2P Mod Management Suite** powered by an extensible plugin architecture:
- 🧩 **Core App (`ModTogetherUniversal`)**: C# .NET 8 WPF application providing the high-speed P2P engine, Room management, Mod Explorer, Recovery Manager, and WinUI 3 Fluent interface.
- 🔌 **Plugin Engine (`ModTogether.API`)**: Plugin API supporting game plugins written in C# (.NET Assembly DLLs).
- 🐉 **Monster Hunter World Plugin (`ModTogether.Plugins.MHW`)**: Official plugin featuring `nativePC` folder management, P2P sync, archive extraction, file conflict detection, and mod validation/backup.

### 🌟 Key Features
- ⚡ **Real-Time P2P Sync:** Instant peer-to-peer file synchronization driven by embedded ASP.NET Core Kestrel HTTP streaming server and Network Discovery.
- 🏠 **Unified Room Control:** Side-by-side Host (Create Session) & Client (Join Session) interface with UPnP router auto-port forwarding, 6-digit PIN authentication, and member kick/ban controls.
- 🎮 **Multi-Game Profile Switcher:** Seamlessly switch between configured game executable profiles and directories saved in path history.
- 🎨 **Mod Presets & Profile Stashing:** Save/Load custom Mod Presets. Swapping presets safely stashes original active mods without data loss.
- ♻️ **Smart Recycle Mods (`.recycle_mods`):** Automatically recycles removed mod files for instant recovery without downloading again. Features permanent deletion and full restoration options.
- 🔒 **Session Lock Protection:** Strictly locks preset changing and game path switching during active room sessions (Host/Client) to prevent synchronization conflicts.
- 🔌 **Plugins Store & Security Inspector:** Installed Plugins inspector with SHA-256 digital signature verification, bytecode security scanning, and online repository store.
- 📊 **Bandwidth Limiter:** Speed limit controls (Kbps) for Upload and Download to prevent network lag while gaming (with quick 2 MB/s and Unlimited presets).
- 🎮 **Discord Rich Presence (RPC):** Real-time status updates on Discord showing room state, host status, and active session details.
- 🔍 **Mod Explorer:** Search and manage mods with install type controls (Single File copy vs Archive extraction) and mod folder management.
- 📦 **Full Archive Support:** Native handling for `.zip`, `.7z` (including Solid LZMA2 archives), and `.rar` files via SharpCompress.
- ⚔️ **Conflict Detection:** Scans mod archives to detect potential file overlaps before installation.
- 🌐 **Bilingual UI (English / Thai):** Instant language switching (Thai / English) without restarting the app.
- 🛠️ **Logging & Security Controls:** Toggleable verbose Debug Logging (Console Output), Error Log File Writing (`error.log`), and Strict Plugin Security Inspection.

### 🚀 Build Editions
ModTogether provides **2 build editions**:
1. 🟢 **Standalone Edition (`ModTogether_Universal_Standalone_x64.exe` ~85MB)**
   - Includes .NET 8 Runtime bundled inside.
   - **No installation or .NET download required.** Completely portable out of the box.
2. ⚡ **Lightweight Edition (`ModTogether_Universal_Lightweight_x64.exe` ~10MB)**
   - Ultra lightweight (~10MB).
   - Requires [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed on Windows.

### 🛠️ How to Build
Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed.
1. Double-click `build_universal.bat` (or run `.\build_universal.bat` in Terminal).
2. Output executables and plugins will be generated in the `dist` folder:
   - `dist\Standalone\ModTogether_Universal_Standalone_x64.exe`
   - `dist\Lightweight\ModTogether_Universal_Lightweight_x64.exe`
   - `dist\Plugins\`

### 🛡️ Antivirus & Security Notice
- Built using official Microsoft .NET 8 Single-File bundling with ReadyToRun (R2R) compilation and embedded debug symbols.
- **100% Safe & Open-Source:** No third-party packers (UPX) or obfuscators (ConfuserEx) are used. Certified clean for VirusTotal and Nexus Mods distribution.

### 🎯 How to Use

#### Creating or Joining a Room
1. Open the app and navigate to the **Room (Host / Join)** tab.
2. **To Host:** Click **Start Hosting** under *Create Session (Host)*. Option to enable UPnP Auto Port Forwarding. Share the generated IP and 6-digit PIN with your friends.
3. **To Join:** Enter the Host's IP address and 6-digit PIN under *Join Session (Client)*, or click **Scan LAN** to auto-detect hosts on your local network / VPN (ZeroTier, Radmin, Hamachi). Click **Join**.

#### Managing & Auto-Installing Mods
- Go to the **Mod Explorer** or **Game Mods Manager** tab.
- Click **Import Mod** or drop `.zip`/`.7z`/`.rar` files into the mod folder.
- Toggle checked mods and click **Install Checked** to extract them straight into your game's mod directory.

---

## 🇹🇭 ภาษาไทย (Thai Version)

> [!WARNING]
> ## คำชี้แจงสำคัญ (Disclaimer)
> โปรแกรมนี้ **ไม่ใช่** โปรแกรมช่วยเล่น (Cheat/Hack) หรือโปรแกรมแทรกแซงหน่วยความจำ (Memory Injection) ของตัวเกมแต่อย่างใด เป็นเพียงแค่ "เครื่องมือซิงค์และจัดการไฟล์ม็อด (File Synchronization & Mod Management Tool)" ที่ช่วยคัดลอกไฟล์จากเครื่องหนึ่งไปยังอีกเครื่องหนึ่งผ่านเครือข่าย เพื่อความสะดวกในการเล่นเกม Co-op ที่ต้องใช้ม็อดตรงกันเท่านั้น ไม่มีเจตนาในการละเมิดกฎกติกาหรือข้ามผ่านระบบ Anti-Cheat ของเกม การทำงานเทียบเท่ากับการส่งไฟล์ผ่าน Google Drive หรือบีบอัดไฟล์ Zip ส่งให้เพื่อนทางแชท เพียงแต่เป็นการทำงานให้อัตโนมัติและส่งหากันโดยตรงเท่านั้น

### 📦 สถาปัตยกรรมระบบปลั๊กอิน (Modular Architecture & Game Plugins)

ModTogether ถูกออกแบบเป็น **ชุดโปรแกรมซิงค์ม็อด P2P แบบเอนกประสงค์ (Universal Suite)** ที่ขับเคลื่อนด้วยระบบปลั๊กอินส่วนขยาย:
- 🧩 **แอปพลิเคชันหลัก (`ModTogetherUniversal`)**: โปรแกรม C# .NET 8 WPF ที่ทำหน้าที่รันระบบ P2P, จัดการห้อง, Mod Explorer, Recovery Manager และหน้าต่าง WinUI 3 Fluent
- 🔌 **ระบบส่วนขยาย (`ModTogether.API`)**: ระบบ API รองรับปลั๊กอินเสริมสำหรับเกมต่างๆ พัฒนาด้วยภาษา C# (.NET Assembly DLLs)
- 🐉 **ปลั๊กอิน Monster Hunter World (`ModTogether.Plugins.MHW`)**: ปลั๊กอินอย่างเป็นทางการสำหรับเกม MHW รองรับการจัดการโฟลเดอร์ `nativePC`, ซิงค์ P2P, คลายบีบอัดไฟล์ม็อด, ตรวจจับไฟล์ทับซ้อน และการกู้คืน/สำรองข้อมูลม็อด

### 🌟 จุดเด่นและฟีเจอร์หลัก
- ⚡ **ซิงค์ม็อดเรียลไทม์ (Real-Time P2P Sync):** ส่งและรับไฟล์ม็อดระหว่างเพื่อนในห้องผ่าน Kestrel HTTP Streaming และ Network Discovery
- 🏠 **หน้าจัดการห้องรวม (Unified Room Page):** รวมหน้าสร้างห้อง (Host) และเข้าร่วมห้อง (Client) พร้อมระบบ UPnP เปิดพอร์ตอัตโนมัติ, รหัส PIN 6 หลัก และระบบเตะ/แบนผู้เล่นในเซสชัน
- 🎮 **ระบบสลับโปรไฟล์เกม (Multi-Game Profile Switcher):** สลับโฟลเดอร์เกมและโปรไฟล์ที่เคยตั้งค่าไว้ได้อย่างรวดเร็วจากประวัติการใช้งาน
- 🎨 **โปรไฟล์ม็อด (Mod Presets & Profile Stashing):** บันทึก/โหลดโปรไฟล์ม็อด และสลับม็อดได้ปลอดภัยโดยไม่เสียไฟล์เดิมด้วยระบบ `.stash`
- ♻️ **ระบบกู้คืนไฟล์ม็อด (.recycle_mods):** ม็อดที่ถูกถอนหรือลบจะถูกย้ายเข้าถังขยะกู้คืน สามารถกดกู้คืน (Restore) หรือลบถาวร (Delete Permanently) ได้ตลอดเวลา
- 🔒 **ระบบล็อกความปลอดภัยขณะมีเซสชัน (Session Lock Protection):** ป้องกันการเปลี่ยนโปรไฟล์ม็อดหรือที่อยู่เกมขณะโฮสต์/เข้าร่วมห้อง เพื่อป้องกันข้อผิดพลาดในการซิงค์ไฟล์
- 🔌 **คลังปลั๊กอิน & ตรวจสอบความปลอดภัย (Plugins Store & Inspector):** ตรวจสอบลายเซ็นดิจิทัล รหัส SHA-256 และสแกนความปลอดภัยของปลั๊กอิน DLL พร้อมคลังออนไลน์สำหรับอัปเดตปลั๊กอิน
- 📊 **ระบบจำกัดความเร็วเน็ต (Bandwidth Limiter):** ตั้งค่าจำกัดความเร็ว Download/Upload (KB/s) ไม่ให้ดึงเน็ตขณะเล่นเกม พร้อมปุ่มทางลัด Unlimited และ 2 MB/s
- 🎮 **Discord Rich Presence (RPC):** แสดงสถานะการใช้งาน ห้อง และจำนวนผู้เล่นบนโปรไฟล์ Discord แบบ Real-time
- 🔍 **สำรวจม็อด (Mod Explorer):** ค้นหา จัดการ และเลือกรูปแบบการติดตั้งม็อด (คัดลอกไฟล์ตรง หรือ แตกไฟล์ archive)
- 📦 **รองรับไฟล์บีบอัดทุกประเภท:** คลายบีบอัดไฟล์ `.zip`, `.7z` (รวมถึง Solid LZMA2 Archives) และ `.rar` ได้สมบูรณ์ผ่าน SharpCompress
- ⚔️ **ตรวจสอบไฟล์ทับซ้อน (Conflict Detection):** สแกนไฟล์ม็อดเพื่อแจ้งเตือนความเสี่ยงการเขียนทับไฟล์ก่อนเริ่มติดตั้ง
- 🌐 **รองรับ 2 ภาษา (Bilingual UI):** สลับเปลี่ยนภาษาไทยและอังกฤษในโปรแกรมได้ทันทีโดยไม่ต้องรีสตาร์ท
- 🛠️ **ควบคุม Logging & Security:** เปิด/ปิด Debug Log (Console), Error Log File (`error.log`) และ Plugin Security Inspection ได้จากหน้าตั้งค่า

### 🚀 ตัวเลือกเวอร์ชันจัดส่ง (Build Editions)
โปรแกรมมี **2 เวอร์ชัน** ให้เลือกใช้งานตามความเหมาะสม:
1. 🟢 **เวอร์ชัน Standalone (`ModTogether_Universal_Standalone_x64.exe` ~85MB)**
   - มัดรวม .NET 8 Runtime มาในตัวโปรแกรม
   - **ไม่ต้องติดตั้งโปรแกรมหรือลง .NET เพิ่มเติม** ดับเบิลคลิกเปิดใช้งานบน Windows 10/11 ได้ทันที
2. ⚡ **เวอร์ชัน Lightweight (`ModTogether_Universal_Lightweight_x64.exe` ~10MB)**
   - ขนาดไฟล์เบาหวิวเพียง ~10MB
   - จำเป็นต้องมี [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) ติดตั้งอยู่ในเครื่องก่อน

### 🛠️ วิธีการ Build โปรแกรม
จำเป็นต้องมี [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) ติดตั้งบนเครื่องนักพัฒนา
1. ดับเบิลคลิกไฟล์ `build_universal.bat` (หรือรัน `.\build_universal.bat` ใน Terminal)
2. สคริปต์จะทำการ Restore Dependencies และ Build ทั้ง 2 เวอร์ชันพร้อม Plugins ออกมาในโฟลเดอร์ `dist`:
   - `dist\Standalone\ModTogether_Universal_Standalone_x64.exe`
   - `dist\Lightweight\ModTogether_Universal_Lightweight_x64.exe`
   - `dist\Plugins\`

### 🛡️ ความปลอดภัยและการสแกนแอนตี้ไวรัส (Security & Antivirus Notice)
- คอมไพล์ด้วยระบบ Single-File Bundling ทางการของ Microsoft .NET 8 ร่วมกับเทคโนโลยี ReadyToRun (R2R) และฝัง Debug Symbols (PDB) ในตัว
- **ปลอดภัย 100% และเป็น Open-Source:** ไม่มีการใช้เครื่องมือพรางโค้ด (ConfuserEx) หรือเครื่องมือบีบอัดภายนอก (UPX) ที่มักทำให้เกิดผลสแกนไวรัสผิดพลาด ปลอดภัยสำหรับการแจกจ่ายบน VirusTotal และ Nexus Mods

### 🎯 วิธีการใช้งาน

#### การสร้าง หรือ เข้าร่วมห้อง (Room)
1. เปิดโปรแกรม เข้าไปที่แท็บ **ห้อง (สร้าง / เข้าร่วม)**
2. **สำหรับคนเปิดห้อง (Host):** กดปุ่ม **เริ่มเปิดห้อง** ในฝั่ง *สร้างห้อง (Host)* (เปิดใช้งาน UPnP เพื่อเปิดพอร์ตอัตโนมัติได้) จากนั้นนำ IP และ PIN 6 หลักไปแจ้งเพื่อน
3. **สำหรับคนเข้าร่วม (Client):** กรอก IP และ PIN 6 หลัก ในฝั่ง *เข้าร่วมห้อง (Client)* หรือกดปุ่ม **ค้นหาใน LAN** เพื่อสแกนหาห้องอัตโนมัติ (รองรับ ZeroTier, Radmin VPN, Hamachi) แล้วกด **เข้าร่วม**

#### การจัดการและติดตั้งม็อด (Mod Explorer / Manager)
- เข้าไปที่แท็บ **จัดการม็อดทั่วไป (Mod Explorer)** หรือ **จัดการม็อดเกม**
- กดปุ่ม **นำเข้าม็อด** หรือลากไฟล์ `.zip`/`.7z`/`.rar` มาวางในโฟลเดอร์ม็อด
- ติ๊กเลือกลิสต์ม็อดที่ต้องการ แล้วกดปุ่ม **ติดตั้งที่เลือก** เพื่อติดตั้งลงโฟลเดอร์ม็อดของเกมทันที

---

## 🙏 Acknowledgements & Tech Stack

- **[.NET 8 (WPF)](https://dotnet.microsoft.com/):** High-performance desktop UI and CoreCLR runtime.
- **[WPF-UI (v3.0.4)](https://github.com/lepoco/wpfui):** Modern WinUI 3 controls and Fluent design elements.
- **[ASP.NET Core Kestrel](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel):** High-speed embedded HTTP web server powering P2P transfers.
- **[SharpCompress (v0.38.0)](https://github.com/adamhathcock/sharpcompress):** Robust archive extraction library for Zip, 7z, and Rar formats.
- **ModTogether.API:** Extensible plugin API for .NET assembly game plugins.
