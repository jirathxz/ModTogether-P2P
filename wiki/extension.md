# ModTogether Plugin Development Guide

ModTogether Universal features a powerful **C# .NET Assembly Plugin System** that allows developers to create custom Mod Managers for **any game**. By implementing the `IModPlugin` interface from `ModTogether.API`, you can build fully functional, natively integrated mod management UI with WPF XAML.

## 🎮 What games can it be used for?
**Any game!** The plugin system isn't bound to a specific game. You can create a plugin for Cyberpunk 2077, Stardew Valley, Resident Evil, or any other game that supports mods.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- A reference to `ModTogether.API.dll` in your plugin project

### Project Setup

1. Create a new **.NET 8 Class Library** project targeting `net8.0-windows`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ModTogether.API\ModTogether.API.csproj" />
  </ItemGroup>
</Project>
```

2. Implement the `IModPlugin` interface:

```csharp
using System.Windows.Controls;
using ModTogether.API;

namespace MyGamePlugin
{
    public class MyGamePlugin : IModPlugin
    {
        public string Name => "My Game Mod Manager";
        public string Description => "Custom mod manager for My Game";
        public string Icon => "Games24";  // FluentIcon name
        public string TargetGameFolder => "MyGameFolder";

        public UserControl CreateManagerPage()
        {
            return new MyManagerPage();
        }
    }
}
```

3. Create a WPF `UserControl` for your plugin's UI:

```xml
<!-- MyManagerPage.xaml -->
<UserControl x:Class="MyGamePlugin.MyManagerPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Grid Margin="20">
        <StackPanel>
            <TextBlock Text="Welcome to My Game Mod Manager!" FontSize="20" />
            <ui:Button x:Name="BtnTest" Content="Click Me!" Margin="0,10,0,0"
                       Click="BtnTest_Click" />
        </StackPanel>
    </Grid>
</UserControl>
```

```csharp
// MyManagerPage.xaml.cs
using System.Windows;
using System.Windows.Controls;

namespace MyGamePlugin
{
    public partial class MyManagerPage : UserControl
    {
        public MyManagerPage()
        {
            InitializeComponent();
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello from My Game Plugin!", "Plugin Test");
        }
    }
}
```

---

## 📦 Deploying Your Plugin

1. Build your plugin project in **Release** mode.
2. Copy the output `.dll` file (e.g., `MyGamePlugin.dll`) to:
   - `Documents\ModTogether\Plugins\` (user plugins directory)
3. Launch ModTogether Universal — your plugin will be automatically discovered and loaded.

---

## 🛡️ Security & Trust

ModTogether implements a multi-layer plugin security system:

### 1. Bytecode Security Inspection
All plugin DLLs undergo static security analysis before loading. The following dangerous APIs are automatically **BLOCKED**:
- `Process.Start`, `ProcessStartInfo` (process execution)
- `Registry`, `RegistryKey` (registry manipulation)
- `P/Invoke`, `DllImport` (native code calls)
- `WebClient`, `HttpClient` (network access)
- `Assembly.Load` (dynamic code loading)
- `File.Delete` outside designated paths (destructive operations)

### 2. SHA-256 Hash Verification
- When **Strict Plugin Security** is enabled in Settings, each plugin's SHA-256 hash is computed and verified.
- First-time plugins display a security confirmation dialog requiring explicit user approval.
- Approved hashes are saved to trusted list — subsequent loads are automatic.

### 3. AssemblyLoadContext Loading
- Plugins are loaded via `AssemblyLoadContext.Default.LoadFromAssemblyPath()` — the standard .NET plugin loading mechanism.
- No raw byte array injection or reflection-based loading is used.

---

## 🔌 IModPlugin Interface

```csharp
namespace ModTogether.API
{
    public interface IModPlugin
    {
        /// <summary>Display name shown in the navigation menu</summary>
        string Name { get; }

        /// <summary>Short description of the plugin</summary>
        string Description { get; }

        /// <summary>FluentIcon name for the navigation tab icon</summary>
        string Icon { get; }

        /// <summary>Target game folder name for auto-detection</summary>
        string TargetGameFolder { get; }

        /// <summary>Creates the WPF UserControl for the plugin's manager page</summary>
        UserControl CreateManagerPage();
    }
}
```

---

## 🎯 Example: Monster Hunter World Plugin

The official `ModTogether.Plugins.MHW` plugin serves as a reference implementation:

- **Source:** `ModTogether.Plugins.MHW/`
- **Features:** `nativePC` folder management, mod archive extraction, P2P sync integration, file conflict detection, mod backup/restore
- **Target:** `MonsterHunterWorld` folder detection

Study the [ManagerPage.xaml.cs](../ModTogether.Plugins.MHW/ManagerPage.xaml.cs) for a complete working example of a game-specific mod management plugin.
