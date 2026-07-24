// Monster Hunter: World Mod Manager Extension
// Interacts with ModTogether ModAPI

// --- State Management ---
function getStatePath(gameDir) {
    return API.CombinePath(gameDir, "mhw_installed_mods.json");
}
function loadState(gameDir) {
    var content = API.ReadState(getStatePath(gameDir));
    if (!content) return {};
    try { return JSON.parse(content); } catch (e) { return {}; }
}
function saveState(gameDir, state) {
    API.SaveState(getStatePath(gameDir), JSON.stringify(state, null, 2));
}

function isMhwFolder(name) {
    var folders = ["sound", "wp", "vfx", "stage", "art", "ui", "pl", "hm", "em", "facility", "gimmick", "collision", "shader", "ot", "item", "bg", "quest", "ev", "common", "npc", "chunk"];
    name = name.toLowerCase();
    for (var i = 0; i < folders.length; i++) {
        if (name === folders[i]) return true;
    }
    return false;
}

// Global Extension Functions called by C# Engine
function parseArchiveFiles(files) {
    var optionGroups = {};
    for (var i = 0; i < files.length; i++) {
        var entry = files[i].replace(/\\/g, '/').trim();
        if (!entry || entry.substring(entry.length - 1) === "/") continue;
        if (entry.toLowerCase().indexOf("__macosx/") === 0 || entry.toLowerCase().indexOf(".ds_store") !== -1) continue;
        
        var optionName = "Mod Files (Default)";
        var lower = entry.toLowerCase();
        var nativeIdx = lower.indexOf("nativepc/");
        
        if (nativeIdx > 0) {
            var prefix = entry.substring(0, nativeIdx).trim().replace(/\/$/, "");
            if (prefix.length > 0) {
                optionName = prefix;
            }
        }
        
        if (!optionGroups[optionName]) {
            optionGroups[optionName] = [];
        }
        optionGroups[optionName].push(entry);
    }
    
    var result = [];
    for (var groupName in optionGroups) {
        result.push({
            Name: groupName,
            Files: optionGroups[groupName]
        });
    }
    return JSON.stringify(result);
}

function installMod(archivePath, gameDir, selectedFiles) {
    if (!archivePath || !gameDir) return;
    
    var modName = API.GetFileName(archivePath);
    var tempFolder = API.CombinePath(API.CombinePath(gameDir, "GameMods"), ".temp_extract_" + Date.now());
    API.CreateDirectory(tempFolder);
    
    try {
        var allFiles = API.ExtractArchive(archivePath, tempFolder, null);
        var nativePcPath = API.CombinePath(gameDir, "nativePC");
        API.CreateDirectory(nativePcPath);
        
        var state = loadState(gameDir);
        var modFilesTracked = [];
        
        var filterActive = selectedFiles && selectedFiles.length > 0;
        var normTemp = tempFolder.replace(/\\/g, "/").replace(/\/$/, "").toLowerCase() + "/";
        
        for (var i = 0; i < allFiles.Count; i++) {
            var fullExtractedPath = allFiles[i].replace(/\\/g, "/");
            var lowerExtracted = fullExtractedPath.toLowerCase();
            
            var relPath = fullExtractedPath;
            if (lowerExtracted.indexOf(normTemp) === 0) {
                relPath = fullExtractedPath.substring(normTemp.length);
            }
            
            if (filterActive) {
                var isMatch = false;
                for (var j = 0; j < selectedFiles.length; j++) {
                    var sf = selectedFiles[j].replace(/\\/g, "/").replace(/\/$/, "");
                    if (!sf || relPath.toLowerCase() === sf.toLowerCase() || relPath.toLowerCase().indexOf(sf.toLowerCase() + "/") === 0) {
                        isMatch = true;
                        break;
                    }
                }
                if (!isMatch) continue;
            }
            
            var sourceFile = fullExtractedPath;
            var trackPath = "";
            var lowerRel = relPath.toLowerCase();
            
            var nativePcIdx = lowerRel.indexOf("nativepc/");
            if (nativePcIdx >= 0) {
                trackPath = relPath.substring(nativePcIdx + 9);
            } else {
                var parts = relPath.split('/');
                var foundMhwFolder = false;
                for (var j = 0; j < parts.length; j++) {
                    if (isMhwFolder(parts[j])) {
                        trackPath = parts.slice(j).join('/');
                        foundMhwFolder = true;
                        break;
                    }
                }
                if (!foundMhwFolder) continue;
            }
            
            if (!trackPath) continue;
            
            var targetFile = API.CombinePath(nativePcPath, trackPath);
            API.MoveFile(sourceFile, targetFile, true);
            modFilesTracked.push(trackPath);
        }
        
        state[modName] = modFilesTracked;
        saveState(gameDir, state);
        API.Log("Installed " + modName + " (" + modFilesTracked.length + " files to nativePC)");
    } catch(e) {
        API.Log("Error installing " + modName + ": " + e.message);
    } finally {
        API.DeleteDirectory(tempFolder, true);
    }
}

function uninstallMod(modName, gameDir) {
    if (!modName || !gameDir) return;
    if (modName.indexOf("/") >= 0 || modName.indexOf("\\") >= 0) {
        modName = API.GetFileName(modName);
    }
    
    var state = loadState(gameDir);
    if (state[modName]) {
        var nativePcPath = API.CombinePath(gameDir, "nativePC");
        var files = state[modName];
        
        for (var i = 0; i < files.length; i++) {
            var targetFile = API.CombinePath(nativePcPath, files[i]);
            API.DeleteFile(targetFile);
        }
        
        delete state[modName];
        saveState(gameDir, state);
        API.CleanupEmptyDirectories(nativePcPath);
        API.Log("Uninstalled " + modName + " and cleaned up empty folders.");
    } else {
        API.Log("Warning: " + modName + " is not in installed state.");
    }
}

// --- UI Injection ---
var tabId = "mhw_manager";
var xaml = `<Grid VerticalAlignment="Stretch" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <!-- We remove row definitions so the bottom panel can float over the content -->
    
    <Grid Margin="16,16,16,80">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="2*" />
            <ColumnDefinition Width="2.5*" />
            <ColumnDefinition Width="1*" />
        </Grid.ColumnDefinitions>

        <ui:Card Grid.Column="0" Padding="12" Margin="0,0,8,0" VerticalAlignment="Stretch" VerticalContentAlignment="Top">
            <Grid VerticalAlignment="Stretch">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>
                <StackPanel Orientation="Horizontal" Margin="0,0,0,12" Grid.Row="0">
                    <TextBlock Text="MHW Mod Library" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center" Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
                    <ComboBox x:Name="ComboSort" Margin="16,0,0,0" Width="130" SelectedIndex="0">
                        <ComboBoxItem Content="Sort: Name" />
                        <ComboBoxItem Content="Sort: Date" />
                        <ComboBoxItem Content="Sort: Size" />
                    </ComboBox>
                </StackPanel>

                <ui:TextBox x:Name="TxtSearch" PlaceholderText="Search mods..." Grid.Row="1" Margin="0,0,0,8" />

                <StackPanel Orientation="Horizontal" Margin="0,0,0,12" Grid.Row="2" VerticalAlignment="Center">
                    <Border Width="10" Height="10" Background="#3C27AE60" BorderBrush="#FF27AE60" BorderThickness="1" CornerRadius="2" Margin="0,0,4,0"/>
                    <TextBlock Text="Installed" FontSize="11" Margin="0,0,12,0" Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                    <Border Width="10" Height="10" Background="#3C95A5A6" BorderBrush="#FF95A5A6" BorderThickness="1" CornerRadius="2" Margin="0,0,4,0"/>
                    <TextBlock Text="Not Installed" FontSize="11" Margin="0,0,12,0" Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                    <Border Width="10" Height="10" Background="#3CE74C3C" BorderBrush="#FFE74C3C" BorderThickness="1" CornerRadius="2" Margin="0,0,4,0"/>
                    <TextBlock Text="Conflict" FontSize="11" Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                </StackPanel>
                
                <ListBox x:Name="ListMods" Grid.Row="3" Background="{DynamicResource ControlFillColorDefaultBrush}" BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}" BorderThickness="1" Padding="0,0,0,0" VerticalAlignment="Stretch" ScrollViewer.VerticalScrollBarVisibility="Auto" ScrollViewer.HorizontalScrollBarVisibility="Disabled" VirtualizingStackPanel.IsVirtualizing="True" VirtualizingStackPanel.VirtualizationMode="Recycling" ScrollViewer.CanContentScroll="True" Grid.IsSharedSizeScope="True">
                    <ListBox.ItemContainerStyle>
                        <Style TargetType="ListBoxItem">
                            <Setter Property="Background" Value="{Binding BackgroundColor}" />
                            <Setter Property="Margin" Value="0,0,0,2" />
                            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                        </Style>
                    </ListBox.ItemContainerStyle>
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <Grid Margin="4,2">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" SharedSizeGroup="DateCol" />
                                    <ColumnDefinition Width="Auto" SharedSizeGroup="SizeCol" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Orientation="Horizontal" Grid.Column="0" Margin="0,0,8,0">
                                    <CheckBox IsChecked="{Binding IsChecked, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,6,0" />
                                    <TextBlock Text="{Binding DisplayName}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" Foreground="{DynamicResource TextFillColorPrimaryBrush}" ToolTip="{Binding Filename}" />
                                </StackPanel>
                                <TextBlock Text="{Binding DateModified}" Grid.Column="1" VerticalAlignment="Center" Margin="0,0,12,0" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontSize="11" />
                                <TextBlock Text="{Binding Size}" Grid.Column="2" VerticalAlignment="Center" Foreground="{DynamicResource TextFillColorSecondaryBrush}" FontSize="11" />
                            </Grid>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Grid>
        </ui:Card>

        <ui:Card Grid.Column="1" Padding="12" Margin="8,0,8,0" VerticalAlignment="Stretch" VerticalContentAlignment="Top">
            <Grid VerticalAlignment="Stretch">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>
                
                <Grid Grid.Row="0" Margin="0,0,0,12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="Mod Files (nativePC)" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center" Foreground="{DynamicResource TextFillColorPrimaryBrush}" Grid.Column="0"/>
                    <StackPanel Orientation="Horizontal" Grid.Column="1" VerticalAlignment="Center">
                        <ui:Button x:Name="BtnCheckAllTree" Content="Check All" Icon="{ui:SymbolIcon Checkmark24}" Margin="0,0,8,0" />
                        <ui:Button x:Name="BtnUncheckAllTree" Content="Uncheck All" Icon="{ui:SymbolIcon Dismiss24}" />
                    </StackPanel>
                </Grid>
                
                <ScrollViewer Grid.Row="1" VerticalAlignment="Stretch" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto">
                    <Grid VerticalAlignment="Stretch" Margin="0,0,0,0">
                        <TreeView x:Name="_listModFiles" Background="Transparent" BorderThickness="0" Margin="0,10,0,0" VirtualizingStackPanel.IsVirtualizing="True" VirtualizingStackPanel.VirtualizationMode="Recycling">
                            <TreeView.ItemTemplate>
                                <HierarchicalDataTemplate ItemsSource="{Binding Children}">
                                    <StackPanel Orientation="Horizontal" Margin="0,2">
                                        <CheckBox IsThreeState="True" IsChecked="{Binding IsChecked, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,5,0" />
                                        <ui:SymbolIcon Filled="True" Margin="0,0,5,0" VerticalAlignment="Center">
                                            <ui:SymbolIcon.Style>
                                                <Style TargetType="ui:SymbolIcon">
                                                    <Setter Property="Symbol" Value="Document24"/>
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding IsDirectory}" Value="True">
                                                            <Setter Property="Symbol" Value="Folder24"/>
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </ui:SymbolIcon.Style>
                                        </ui:SymbolIcon>
                                        <TextBlock Text="{Binding Name}" VerticalAlignment="Center" Foreground="{DynamicResource TextFillColorPrimaryBrush}" />
                                    </StackPanel>
                                </HierarchicalDataTemplate>
                            </TreeView.ItemTemplate>
                            <TreeView.ItemContainerStyle>
                                <Style TargetType="TreeViewItem">
                                    <Setter Property="IsExpanded" Value="{Binding IsExpanded, Mode=TwoWay}"/>
                                    <Setter Property="Padding" Value="4"/>
                                </Style>
                            </TreeView.ItemContainerStyle>
                        </TreeView>
                        <ui:ProgressRing x:Name="LoadingRing" IsIndeterminate="True" Visibility="Collapsed" HorizontalAlignment="Center" VerticalAlignment="Center" Width="48" Height="48" />
                    </Grid>
                </ScrollViewer>
            </Grid>
        </ui:Card>

        <ui:Card Grid.Column="2" Padding="12" Margin="8,0,0,0" VerticalAlignment="Top" VerticalContentAlignment="Top">
            <StackPanel>
                <TextBlock x:Name="LblModInfo" Text="Select a mod." TextWrapping="Wrap" Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,16" />
                <ui:Button x:Name="BtnInstall" Content="Install Mod" Icon="{ui:SymbolIcon Add24}" Appearance="Primary" HorizontalAlignment="Stretch" IsEnabled="False" Margin="0,0,0,8" />
                <ui:Button x:Name="BtnUninstall" Content="Uninstall Mod" Icon="{ui:SymbolIcon Subtract24}" HorizontalAlignment="Stretch" IsEnabled="False" Margin="0,0,0,8" />
                <ui:Button x:Name="BtnDelete" Content="Delete Mod" Icon="{ui:SymbolIcon Delete24}" HorizontalAlignment="Stretch" IsEnabled="False" />
            </StackPanel>
        </ui:Card>
    </Grid>

    <Border VerticalAlignment="Bottom" HorizontalAlignment="Center" Margin="16,0,16,16" Background="#B2000000" BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}" BorderThickness="1" CornerRadius="8">
        <Border.Effect>
            <DropShadowEffect BlurRadius="15" Opacity="0.5" ShadowDepth="4" Direction="270" />
        </Border.Effect>
        <WrapPanel Orientation="Horizontal" Margin="12,12,4,4" HorizontalAlignment="Center">
            <StackPanel Orientation="Horizontal">
                <ui:Button x:Name="BtnCheckAll" Content="Check All" Icon="{ui:SymbolIcon Checkmark24}" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnUncheckAll" Content="Uncheck All" Icon="{ui:SymbolIcon Dismiss24}" Margin="0,0,16,8" />
                <Rectangle Width="1" Height="24" Fill="{DynamicResource DividerStrokeColorDefaultBrush}" Margin="0,0,16,8" />
            </StackPanel>

            <StackPanel Orientation="Horizontal">
                <ui:Button x:Name="BtnRefresh" Content="Refresh Mods" Icon="{ui:SymbolIcon ArrowSync24}" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnValidate" Content="Validate" Icon="{ui:SymbolIcon Document24}" Margin="0,0,16,8" />
                <Rectangle Width="1" Height="24" Fill="{DynamicResource DividerStrokeColorDefaultBrush}" Margin="0,0,16,8" />
            </StackPanel>

            <StackPanel Orientation="Horizontal">
                <ui:Button x:Name="BtnInstallChecked" Content="Install Checked" Icon="{ui:SymbolIcon Add24}" Appearance="Primary" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnUninstallChecked" Content="Uninstall Checked" Icon="{ui:SymbolIcon Subtract24}" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnDeleteChecked" Content="Delete Checked" Icon="{ui:SymbolIcon Delete24}" Margin="0,0,8,8" />
            </StackPanel>
        </WrapPanel>
    </Border>
</Grid>`;

API.CreateTab(tabId, "MHW Mod Manager", "Games24");
API.SetTabContent(tabId, xaml);

// --- Mod Manager Logic in JS ---

var GAME_DIR = "";
try {
    var SystemApp = importNamespace("ModTogetherUniversal").App;
    GAME_DIR = SystemApp.Settings.Current.GameDirectory;
} catch (e) {
    API.Log("Warning: Could not get GameDirectory natively. Falling back to default.");
}

var modCollection = API.CreateModItemCollection();
API.SetItemsSource(tabId, "ListMods", modCollection);

var treeCollection = API.CreateModTreeCollection();
API.SetItemsSource(tabId, "_listModFiles", treeCollection);

var currentModFilename = "";
var allRawModItems = [];

function autoDetectInstalledMods() {
    if (!GAME_DIR) return;
    var modsDir = API.CombinePath(GAME_DIR, "GameMods");
    var nativePcDir = API.CombinePath(GAME_DIR, "nativePC");
    if (!API.DirectoryExists(modsDir) || !API.DirectoryExists(nativePcDir)) return;

    var state = loadState(GAME_DIR);
    var changes = false;
    
    try {
        var Directory = importNamespace("System.IO").Directory;
        var FileInfo = importNamespace("System.IO").FileInfo;
        var files = Directory.GetFiles(modsDir);
        
        for (var i = 0; i < files.Length; i++) {
            var filePath = files[i];
            var fi = new FileInfo(filePath);
            var ext = fi.Extension.toLowerCase();
            if (ext !== ".zip" && ext !== ".rar" && ext !== ".7z") continue;
            
            var filename = fi.Name;
            if (state[filename]) continue;
            
            try {
                var contents = API.GetArchiveContents(filePath);
                var modFilesTracked = [];
                var allExist = true;
                
                for (var j = 0; j < contents.Count; j++) {
                    var rawEntry = contents[j].replace(/\\/g, "/").trim();
                    if (!rawEntry || rawEntry.substring(rawEntry.length - 1) === "/") continue;
                    var lowerEntry = rawEntry.toLowerCase();
                    
                    var nativePcIdx = lowerEntry.indexOf("nativepc/");
                    var relPath = "";
                    if (nativePcIdx >= 0) {
                        relPath = rawEntry.substring(nativePcIdx + 9);
                    } else {
                        var parts = rawEntry.split('/');
                        for (var k = 0; k < parts.length; k++) {
                            if (isMhwFolder(parts[k])) {
                                relPath = parts.slice(k).join('/');
                                break;
                            }
                        }
                    }
                    if (!relPath) continue;
                    
                    var targetFile = API.CombinePath(nativePcDir, relPath);
                    if (API.FileExists(targetFile)) {
                        modFilesTracked.push(relPath);
                    } else {
                        allExist = false;
                        break;
                    }
                }
                
                if (allExist && modFilesTracked.length > 0) {
                    state[filename] = modFilesTracked;
                    changes = true;
                    API.Log("🔍 Auto-detected manually installed mod: " + filename);
                }
            } catch(e) {}
        }
        
        if (changes) {
            saveState(GAME_DIR, state);
        }
    } catch(e) {}
}

function applyFilterAndSort() {
    var query = API.GetText(tabId, "TxtSearch").trim().toLowerCase();
    var sortIdx = API.GetSelectedIndex(tabId, "ComboSort");
    
    var filtered = [];
    for (var i = 0; i < allRawModItems.length; i++) {
        var m = allRawModItems[i];
        if (!query || m.Filename.toLowerCase().indexOf(query) !== -1 || m.DisplayName.toLowerCase().indexOf(query) !== -1) {
            filtered.push(m);
        }
    }
    
    if (sortIdx === 0) {
        filtered.sort(function(a, b) { return a.DisplayName.localeCompare(b.DisplayName); });
    } else if (sortIdx === 1) {
        filtered.sort(function(a, b) { return b.DateNum - a.DateNum; });
    } else if (sortIdx === 2) {
        filtered.sort(function(a, b) { return b.SizeNum - a.SizeNum; });
    }
    
    modCollection.Clear();
    for (var j = 0; j < filtered.length; j++) {
        modCollection.Add(filtered[j]);
    }
}

function loadMods() {
    autoDetectInstalledMods();
    allRawModItems = [];
    modCollection.Clear();
    if (!GAME_DIR) return;
    
    var modsDir = API.CombinePath(GAME_DIR, "GameMods");
    if (!API.DirectoryExists(modsDir)) {
        API.CreateDirectory(modsDir);
    }
    
    var state = loadState(GAME_DIR);
    
    var installedFilesMap = {};
    for (var modName in state) {
        var installedList = state[modName];
        for (var f = 0; f < installedList.length; f++) {
            installedFilesMap[installedList[f].toLowerCase()] = true;
        }
    }
    
    try {
        var Directory = importNamespace("System.IO").Directory;
        var FileInfo = importNamespace("System.IO").FileInfo;
        var files = Directory.GetFiles(modsDir);
        
        for (var i = 0; i < files.Length; i++) {
            var filePath = files[i];
            var fi = new FileInfo(filePath);
            var ext = fi.Extension.toLowerCase();
            
            if (ext !== ".zip" && ext !== ".rar" && ext !== ".7z") continue;
            
            var filename = fi.Name;
            var isInstalled = state[filename] ? true : false;
            var hasConflict = false;
            
            if (!isInstalled) {
                try {
                    var archiveFiles = API.GetArchiveContents(filePath);
                    for (var a = 0; a < archiveFiles.Count; a++) {
                        var rawEntry = archiveFiles[a].replace(/\\/g, "/").trim();
                        var lowerEntry = rawEntry.toLowerCase();
                        var nativeIdx = lowerEntry.indexOf("nativepc/");
                        if (nativeIdx >= 0) {
                            var relPath = lowerEntry.substring(nativeIdx + 9);
                            if (relPath && installedFilesMap[relPath]) {
                                hasConflict = true;
                                break;
                            }
                        }
                    }
                } catch(e) {}
            }
            
            var item = API.CreateModItem();
            item.Filename = filename;
            item.DisplayName = filename + (isInstalled ? " [Installed]" : "");
            item.Size = (fi.Length / (1024 * 1024)).toFixed(2) + " MB";
            item.SizeNum = fi.Length;
            item.DateModified = fi.LastWriteTime.toLocaleString();
            item.DateNum = fi.LastWriteTime;
            
            if (isInstalled) {
                item.IsInstalled = true;
                item.BackgroundColor = API.GetBrush("#3C27AE60"); // Green
            } else if (hasConflict) {
                item.IsInstalled = false;
                item.BackgroundColor = API.GetBrush("#3CE74C3C"); // Red Conflict
            } else {
                item.IsInstalled = false;
                item.BackgroundColor = API.GetBrush("#3C95A5A6"); // Gray Not Installed
            }
            
            allRawModItems.push(item);
        }
        
        applyFilterAndSort();
        
        if (currentModFilename) {
            if (state[currentModFilename]) {
                API.SetIsEnabled(tabId, "BtnInstall", false);
                API.SetIsEnabled(tabId, "BtnUninstall", true);
                API.SetIsEnabled(tabId, "BtnDelete", true);
            } else {
                API.SetIsEnabled(tabId, "BtnInstall", true);
                API.SetIsEnabled(tabId, "BtnUninstall", false);
                API.SetIsEnabled(tabId, "BtnDelete", true);
            }
        }
    } catch(e) {
        API.Log("Error loading mods: " + e.message);
    }
}

API.AddTabEvent(tabId, "TxtSearch", "TextChanged", function() {
    applyFilterAndSort();
});

API.AddTabEvent(tabId, "ComboSort", "SelectionChanged", function() {
    applyFilterAndSort();
});

API.AddTabEvent(tabId, "BtnRefresh", "Click", function() {
    loadMods();
    API.ShowMessage("Refresh", "Mod Library refreshed!");
});

API.AddTabEvent(tabId, "BtnValidate", "Click", function() {
    if (!GAME_DIR) return;
    var state = loadState(GAME_DIR);
    var nativePcPath = API.CombinePath(GAME_DIR, "nativePC");
    
    var corruptedMods = [];
    var totalChecked = 0;
    
    for (var modName in state) {
        totalChecked++;
        var files = state[modName];
        var missingCount = 0;
        
        for (var i = 0; i < files.length; i++) {
            var targetFile = API.CombinePath(nativePcPath, files[i]);
            if (!API.FileExists(targetFile)) {
                missingCount++;
            }
        }
        
        if (missingCount > 0) {
            corruptedMods.push(modName);
            API.Log("❌ Validation failed for " + modName + ": " + missingCount + " files missing! Marking as Uninstalled.");
        }
    }
    
    if (corruptedMods.length > 0) {
        for (var k = 0; k < corruptedMods.length; k++) {
            delete state[corruptedMods[k]];
        }
        saveState(GAME_DIR, state);
        loadMods();
        API.ShowMessage("Validation", corruptedMods.length + " mod(s) were found corrupted and marked as uninstalled.");
    } else {
        API.ShowMessage("Validation", "All " + totalChecked + " installed mod(s) are intact and valid!");
    }
});

API.AddTabEvent(tabId, "ListMods", "SelectionChanged", function() {
    var selectedItem = API.GetSelectedItem(tabId, "ListMods");
    if (!selectedItem) {
        currentModFilename = "";
        treeCollection.Clear();
        API.SetText(tabId, "LblModInfo", "Select a mod.");
        API.SetIsEnabled(tabId, "BtnInstall", false);
        API.SetIsEnabled(tabId, "BtnUninstall", false);
        API.SetIsEnabled(tabId, "BtnDelete", false);
        return;
    }
    
    var filename = selectedItem.Filename;
    currentModFilename = filename;
    
    API.SetText(tabId, "LblModInfo", filename);
    
    var state = loadState(GAME_DIR);
    if (state[filename]) {
        API.SetIsEnabled(tabId, "BtnInstall", false);
        API.SetIsEnabled(tabId, "BtnUninstall", true);
        API.SetIsEnabled(tabId, "BtnDelete", true);
    } else {
        API.SetIsEnabled(tabId, "BtnInstall", true);
        API.SetIsEnabled(tabId, "BtnUninstall", false);
        API.SetIsEnabled(tabId, "BtnDelete", true);
    }
    
    treeCollection.Clear();
    API.ShowLoading(tabId, "LoadingRing", true);
    
    API.ExecuteDelayed(10, function() {
        try {
            var fullPath = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), filename);
            var allFiles = API.GetArchiveContents(fullPath); 
            var strFiles = [];
            for (var i = 0; i < allFiles.Count; i++) {
                strFiles.push(allFiles[i]);
            }
            buildTree(strFiles);
        } catch(e) {
            API.Log("Error listing archive: " + e.message);
        }
        
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

API.AddTabEvent(tabId, "BtnInstall", "Click", function() {
    if (!currentModFilename || !GAME_DIR) return;
    var archivePath = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), currentModFilename);
    var selectedFiles = [];
    for (var i = 0; i < treeCollection.Count; i++) {
        getCheckedFiles(treeCollection[i], selectedFiles);
    }
    API.ShowLoading(tabId, "LoadingRing", true);
    API.ExecuteDelayed(10, function() {
        try {
            installMod(archivePath, GAME_DIR, selectedFiles);
            API.ShowMessage("Install", "Installed " + currentModFilename + " successfully!");
            loadMods();
        } catch(e) {
            API.Log("Error installing mod: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

API.AddTabEvent(tabId, "BtnUninstall", "Click", function() {
    if (!currentModFilename || !GAME_DIR) return;
    API.ShowLoading(tabId, "LoadingRing", true);
    API.ExecuteDelayed(10, function() {
        try {
            uninstallMod(currentModFilename, GAME_DIR);
            API.ShowMessage("Uninstall", "Uninstalled " + currentModFilename + " successfully!");
            loadMods();
        } catch(e) {
            API.Log("Error uninstalling mod: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

API.AddTabEvent(tabId, "BtnDelete", "Click", function() {
    if (!currentModFilename || !GAME_DIR) return;
    API.ShowLoading(tabId, "LoadingRing", true);
    API.ExecuteDelayed(10, function() {
        try {
            var File = importNamespace("System.IO").File;
            var Directory = importNamespace("System.IO").Directory;
            var recycleDir = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), ".recycle_mods");
            if (!Directory.Exists(recycleDir)) Directory.CreateDirectory(recycleDir);
            
            var fullPath = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), currentModFilename);
            var targetPath = API.CombinePath(recycleDir, currentModFilename);
            if (File.Exists(fullPath)) {
                File.Move(fullPath, targetPath, true);
                API.Log("🗑️ Mod moved to recycle bin: " + currentModFilename);
            }
            loadMods();
        } catch(e) {
            API.Log("Error deleting mod: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

API.AddTabEvent(tabId, "BtnDeleteChecked", "Click", function() {
    if (!GAME_DIR) return;
    var checkedItems = [];
    for (var i = 0; i < modCollection.Count; i++) {
        if (modCollection[i].IsChecked) checkedItems.push(modCollection[i].Filename);
    }
    
    if (checkedItems.length === 0) {
        API.ShowMessage("Delete", "No mods selected to delete.");
        return;
    }
    
    API.ShowLoading(tabId, "LoadingRing", true);
    
    API.ExecuteDelayed(10, function() {
        try {
            var File = importNamespace("System.IO").File;
            var Directory = importNamespace("System.IO").Directory;
            var recycleDir = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), ".recycle_mods");
            if (!Directory.Exists(recycleDir)) Directory.CreateDirectory(recycleDir);
            
            for (var i = 0; i < checkedItems.length; i++) {
                var fullPath = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), checkedItems[i]);
                var targetPath = API.CombinePath(recycleDir, checkedItems[i]);
                if (File.Exists(fullPath)) {
                    File.Move(fullPath, targetPath, true);
                    API.Log("🗑️ Mod moved to recycle bin: " + checkedItems[i]);
                }
            }
            loadMods();
        } catch(e) {
            API.Log("Error deleting mods: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

function buildTree(files) {
    var optionGroups = {};
    var groupOrder = [];
    var totalFiles = 0;
    
    for (var i = 0; i < files.length; i++) {
        var entry = files[i].replace(/\\/g, '/').trim();
        if (!entry || entry.substring(entry.length - 1) === "/") continue;
        if (entry.toLowerCase().indexOf("__macosx/") === 0 || entry.toLowerCase().indexOf(".ds_store") !== -1) continue;
        
        var ext = entry.substring(entry.lastIndexOf(".")).toLowerCase();
        if (ext === ".png" || ext === ".jpg" || ext === ".jpeg" || ext === ".gif" || ext === ".bmp" || ext === ".txt" || ext === ".md" || ext === ".url") continue;
        if (entry.toLowerCase().indexOf("fomod/") === 0) continue;
        
        var parts = entry.split('/');
        if (parts.length === 0) continue;
        
        var mhwFolderIndex = -1;
        for (var j = 0; j < parts.length; j++) {
            if (parts[j].toLowerCase() === "nativepc" || isMhwFolder(parts[j])) {
                mhwFolderIndex = j;
                break;
            }
        }
        
        if (mhwFolderIndex < 0) continue;
        
        var prefix = "";
        var optionName = "";
        
        if (mhwFolderIndex > 0) {
            prefix = parts.slice(0, mhwFolderIndex).join('/');
            optionName = prefix;
        } else {
            prefix = "";
            optionName = "nativePC (Default)";
        }
        
        if (!optionGroups[optionName]) {
            optionGroups[optionName] = [];
            groupOrder.push(optionName);
        }
        optionGroups[optionName].push(entry);
        totalFiles++;
    }
    
    var optionCount = groupOrder.length;
    API.SetText(tabId, "LblModInfo", currentModFilename + "\nNativePC Options: " + optionCount + " | Files: " + totalFiles);
    
    for (var g = 0; g < groupOrder.length; g++) {
        var key = groupOrder[g];
        var filesInGroup = optionGroups[key];
        var isDefaultSelected = (g === 0);
        var rootNode = buildOptionTree(key, "", filesInGroup, isDefaultSelected);
        treeCollection.Add(rootNode);
    }
}

function buildOptionTree(optionName, prefix, files, isDefaultSelected) {
    var rootNode = API.CreateModTreeNode();
    rootNode.Name = optionName;
    rootNode.IsDirectory = true;
    rootNode.IsExpanded = true;
    rootNode.IsChecked = isDefaultSelected;
    
    var dict = {};
    
    for (var i = 0; i < files.length; i++) {
        var file = files[i];
        var relativePath = file;
        if (prefix && file.indexOf(prefix + "/") === 0) {
            relativePath = file.substring(prefix.length + 1);
        }
        
        var parts = relativePath.split('/');
        var currentNode = rootNode;
        var currentPath = "";
        
        for (var j = 0; j < parts.length; j++) {
            var part = parts[j];
            currentPath += (currentPath.length > 0 ? "/" : "") + part;
            
            if (!dict[currentPath]) {
                var newNode = API.CreateModTreeNode();
                newNode.Name = part;
                newNode.IsDirectory = (j < parts.length - 1);
                newNode.EntryKey = file;
                newNode.Parent = currentNode;
                newNode.IsChecked = isDefaultSelected;
                
                currentNode.Children.Add(newNode);
                dict[currentPath] = newNode;
            }
            
            currentNode = dict[currentPath];
        }
    }
    
    return rootNode;
}

// --- Install Logic ---

function getCheckedFiles(node, checkedList) {
    if (node.IsChecked !== false) {
        if (!node.IsDirectory && node.EntryKey) {
            checkedList.push(node.EntryKey);
        }
        for (var i = 0; i < node.Children.Count; i++) {
            getCheckedFiles(node.Children[i], checkedList);
        }
    }
}

function setAllTreeChecked(nodes, isChecked) {
    if (!nodes) return;
    for (var i = 0; i < nodes.Count; i++) {
        nodes[i].IsChecked = isChecked;
        if (nodes[i].Children && nodes[i].Children.Count > 0) {
            setAllTreeChecked(nodes[i].Children, isChecked);
        }
    }
}

API.AddTabEvent(tabId, "BtnCheckAll", "Click", function() {
    for (var i = 0; i < modCollection.Count; i++) {
        modCollection[i].IsChecked = true;
    }
});

API.AddTabEvent(tabId, "BtnUncheckAll", "Click", function() {
    for (var i = 0; i < modCollection.Count; i++) {
        modCollection[i].IsChecked = false;
    }
});

API.AddTabEvent(tabId, "BtnCheckAllTree", "Click", function() {
    setAllTreeChecked(treeCollection, true);
});

API.AddTabEvent(tabId, "BtnUncheckAllTree", "Click", function() {
    setAllTreeChecked(treeCollection, false);
});

API.AddTabEvent(tabId, "BtnInstallChecked", "Click", function() {
    if (!GAME_DIR) return;
    var checkedMods = [];
    for (var i = 0; i < modCollection.Count; i++) {
        if (modCollection[i].IsChecked) {
            checkedMods.push(modCollection[i].Filename);
        }
    }
    
    if (checkedMods.length === 0) {
        if (currentModFilename) {
            checkedMods.push(currentModFilename);
        } else {
            API.ShowMessage("Install", "No mods checked in library to install.");
            return;
        }
    }
    
    API.ShowLoading(tabId, "LoadingRing", true);
    
    API.ExecuteDelayed(10, function() {
        try {
            for (var k = 0; k < checkedMods.length; k++) {
                var modFilename = checkedMods[k];
                var archivePath = API.CombinePath(API.CombinePath(GAME_DIR, "GameMods"), modFilename);
                
                var selectedFiles = null;
                if (modFilename === currentModFilename && treeCollection.Count > 0) {
                    selectedFiles = [];
                    for (var t = 0; t < treeCollection.Count; t++) {
                        getCheckedFiles(treeCollection[t], selectedFiles);
                    }
                }
                
                installMod(archivePath, GAME_DIR, selectedFiles);
            }
            API.ShowMessage("Install", "Successfully installed " + checkedMods.length + " mod(s).");
            loadMods();
        } catch(e) {
            API.Log("Error installing checked mods: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

API.AddTabEvent(tabId, "BtnUninstallChecked", "Click", function() {
    if (!GAME_DIR) return;
    var checkedMods = [];
    for (var i = 0; i < modCollection.Count; i++) {
        if (modCollection[i].IsChecked) {
            checkedMods.push(modCollection[i].Filename);
        }
    }
    
    if (checkedMods.length === 0) {
        var selectedItem = API.GetSelectedItem(tabId, "ListMods");
        if (selectedItem) {
            checkedMods.push(selectedItem.Filename);
        } else {
            API.ShowMessage("Uninstall", "No mods checked in library to uninstall.");
            return;
        }
    }
    
    API.ShowLoading(tabId, "LoadingRing", true);
    
    API.ExecuteDelayed(10, function() {
        try {
            for (var k = 0; k < checkedMods.length; k++) {
                uninstallMod(checkedMods[k], GAME_DIR);
            }
            API.ShowMessage("Uninstall", "Successfully uninstalled " + checkedMods.length + " mod(s).");
            loadMods();
        } catch(e) {
            API.Log("Error uninstalling checked mods: " + e.message);
        }
        API.ShowLoading(tabId, "LoadingRing", false);
    });
});

// Initial Load
// We use a small delay to ensure UI elements are loaded
API.ExecuteDelayed(500, function() {
    loadMods();
});
