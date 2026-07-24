
-- Monster Hunter: World Mod Manager Extension (Lua)
-- Interacts with ModTogether ModAPI

local StringUtils = luanet.import_type("System.String")
local Directory = luanet.import_type("System.IO.Directory")
local FileInfo = luanet.import_type("System.IO.FileInfo")
local File = luanet.import_type("System.IO.File")

local ListString = luanet.import_type("System.Collections.Generic.List`1[System.String]")

function getStatePath(gameDir)
    return API:CombinePath(gameDir, "GameMods/mhw_installed_mods.json")
end

function loadState(gameDir)
    return API:LoadMhwState(getStatePath(gameDir))
end

function saveState(gameDir, state)
    API:SaveMhwState(getStatePath(gameDir), state)
end

function isMhwFolder(name)
    local folders = {sound=true, wp=true, vfx=true, stage=true, art=true, ui=true, pl=true, hm=true, em=true, facility=true, gimmick=true, collision=true, shader=true, ot=true, item=true, bg=true, quest=true, ev=true, common=true, npc=true, chunk=true}
    return folders[string.lower(name)] == true
end

function installMod(archivePath, gameDir, selectedFiles)
    if not archivePath or not gameDir then return end
    
    local modName = API:GetFileName(archivePath)
    local tempFolder = API:CombinePath(API:CombinePath(gameDir, "GameMods"), ".temp_extract_" .. tostring(os.time()))
    API:CreateDirectory(tempFolder)
    
    local success, err = pcall(function()
        local allFiles = API:ExtractArchive(archivePath, tempFolder, nil)
        local nativePcPath = API:CombinePath(gameDir, "nativePC")
        API:CreateDirectory(nativePcPath)
        
        local state = loadState(gameDir)
        local modFilesTracked = ListString()
        
        local filterActive = selectedFiles and selectedFiles.Count > 0
        local normTemp = string.lower(string.gsub(tempFolder, "\\", "/"))
        if string.sub(normTemp, -1) ~= "/" then normTemp = normTemp .. "/" end
        
        for i = 0, allFiles.Count - 1 do
            local fullExtractedPath = string.gsub(allFiles[i], "\\", "/")
            local lowerExtracted = string.lower(fullExtractedPath)
            
            local relPath = fullExtractedPath
            if string.find(lowerExtracted, normTemp, 1, true) == 1 then
                relPath = string.sub(fullExtractedPath, string.len(normTemp) + 1)
            end
            
            local skip = false
            if filterActive then
                local isMatch = false
                for j = 0, selectedFiles.Count - 1 do
                    local sf = string.gsub(selectedFiles[j], "\\", "/")
                    while string.sub(sf, -1) == "/" do sf = string.sub(sf, 1, -2) end
                    
                    local lowerRel = string.lower(relPath)
                    local lowerSf = string.lower(sf)
                    
                    if not sf or sf == "" or lowerRel == lowerSf or string.find(lowerRel, lowerSf .. "/", 1, true) == 1 then
                        isMatch = true
                        break
                    end
                end
                if not isMatch then skip = true end
            end
            
            if not skip then
                local sourceFile = fullExtractedPath
                local trackPath = ""
                local lowerRel = string.lower(relPath)
                
                local nativePcIdx = string.find(lowerRel, "nativepc/", 1, true)
                if nativePcIdx then
                    trackPath = string.sub(relPath, nativePcIdx + 9)
                else
                    local parts = {}
                    for part in string.gmatch(relPath, "([^/]+)") do
                        table.insert(parts, part)
                    end
                    local foundMhwFolder = false
                    for j, part in ipairs(parts) do
                        if isMhwFolder(part) then
                            trackPath = table.concat(parts, "/", j)
                            foundMhwFolder = true
                            break
                        end
                    end
                    if not foundMhwFolder then skip = true end
                end
                
                if not skip and trackPath ~= "" then
                    local targetFile = API:CombinePath(nativePcPath, trackPath)
                    API:MoveFile(sourceFile, targetFile, true)
                    modFilesTracked:Add(trackPath)
                end
            end
        end
        
        state[modName] = modFilesTracked
        saveState(gameDir, state)
        API:Log("Installed " .. modName .. " (" .. tostring(modFilesTracked.Count) .. " files to nativePC)")
    end)
    
    if not success then
        API:Log("Error installing " .. modName .. ": " .. tostring(err))
        API:ShowMessage("Install Error", tostring(err))
    end
    API:DeleteDirectory(tempFolder, true)
end

function uninstallMod(modName, gameDir)
    if not modName or not gameDir then return end
    if string.find(modName, "/") or string.find(modName, "\\") then
        modName = API:GetFileName(modName)
    end
    
    local state = loadState(gameDir)
    if state:ContainsKey(modName) then
        local nativePcPath = API:CombinePath(gameDir, "nativePC")
        local files = state[modName]
        
        for i = 0, files.Count - 1 do
            local targetFile = API:CombinePath(nativePcPath, files[i])
            API:DeleteFile(targetFile)
        end
        
        state:Remove(modName)
        saveState(gameDir, state)
        API:CleanupEmptyDirectories(nativePcPath)
        API:Log("Uninstalled " .. modName .. " and cleaned up empty folders.")
    else
        API:Log("Warning: " .. modName .. " is not in installed state.")
    end
end

local tabId = "mhw_manager"
local xaml = [=[<Grid Height="{Binding ActualHeight, RelativeSource={RelativeSource AncestorType={x:Type Page}}}" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <!-- No RowDefinitions, allow floating toolbar to overlay -->
    
    <!-- Panels Layout -->
    <Grid Margin="16" Grid.Row="0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="1*" />
            <ColumnDefinition Width="1.5*" />
        </Grid.ColumnDefinitions>

        <!-- Left Panel -->
        <ui:Card Grid.Column="0" Padding="12" Margin="0,0,8,0" VerticalAlignment="Stretch" VerticalContentAlignment="Stretch">
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

        <!-- Middle Panel -->
        <ui:Card Grid.Column="1" Padding="12" Margin="8,0,8,0" VerticalAlignment="Stretch" VerticalContentAlignment="Stretch">
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
                
                <!-- Removed ScrollViewer wrapper to preserve Virtualization! -->
                <TreeView x:Name="_listModFiles" Grid.Row="1" Background="Transparent" BorderThickness="0" Margin="0,10,0,60" VirtualizingStackPanel.IsVirtualizing="True" VirtualizingStackPanel.VirtualizationMode="Recycling" ScrollViewer.CanContentScroll="True">
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
                <ui:ProgressRing x:Name="LoadingRing" Grid.Row="1" IsIndeterminate="True" Visibility="Collapsed" HorizontalAlignment="Center" VerticalAlignment="Center" Width="48" Height="48" />
            </Grid>
        </ui:Card>
    </Grid>

    <!-- Tools Card (Floating) -->
    <Border Grid.Row="1" VerticalAlignment="Bottom" HorizontalAlignment="Center" Margin="16,0,16,16" Background="#26000000" BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}" BorderThickness="1" CornerRadius="8">
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
                <ui:Button x:Name="BtnInstallChecked" Content="Install Checked" Icon="{ui:SymbolIcon Play24}" Appearance="Primary" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnUninstallChecked" Content="Uninstall Checked" Icon="{ui:SymbolIcon Stop24}" Margin="0,0,8,8" />
                <ui:Button x:Name="BtnDeleteChecked" Content="Delete Checked" Icon="{ui:SymbolIcon Delete24}" Margin="0,0,8,8" />
            </StackPanel>
        </WrapPanel>
    </Border>
</Grid>]=]

API:CreateTab(tabId, "MHW Mod Manager", "Games24")
API:SetTabContent(tabId, xaml)

local GAME_DIR = API:GetGameDirectory()
if not GAME_DIR or GAME_DIR == "" then
    API:Log("Warning: Could not get GameDirectory. Features will be disabled.")
end

local modCollection = API:CreateModItemCollection()
API:SetItemsSource(tabId, "ListMods", modCollection)

local treeCollection = API:CreateModTreeCollection()
API:SetItemsSource(tabId, "_listModFiles", treeCollection)

local currentModFilename = ""
local allRawModItems = {}

function autoDetectInstalledMods()
    if not GAME_DIR or GAME_DIR == "" then return end
    local modsDir = API:CombinePath(GAME_DIR, "GameMods")
    local nativePcDir = API:CombinePath(GAME_DIR, "nativePC")
    if not API:DirectoryExists(modsDir) or not API:DirectoryExists(nativePcDir) then return end

    local state = loadState(GAME_DIR)
    local changes = false
    
    pcall(function()
        local files = Directory.GetFiles(modsDir)
        for i = 0, files.Length - 1 do
            local filePath = files[i]
            local fi = FileInfo(filePath)
            local ext = string.lower(fi.Extension)
            if ext == ".zip" or ext == ".rar" or ext == ".7z" then
                local filename = fi.Name
                if not state:ContainsKey(filename) then
                    pcall(function()
                        local contents = API:GetArchiveContents(filePath)
                        local modFilesTracked = ListString()
                        local allExist = true
                        
                        for j = 0, contents.Count - 1 do
                            local rawEntry = string.gsub(contents[j], "\\", "/")
                            rawEntry = string.match(rawEntry, "^%s*(.-)%s*$")
                            if rawEntry ~= "" and string.sub(rawEntry, -1) ~= "/" then
                                local lowerEntry = string.lower(rawEntry)
                                local nativePcIdx = string.find(lowerEntry, "nativepc/", 1, true)
                                local relPath = ""
                                if nativePcIdx then
                                    relPath = string.sub(rawEntry, nativePcIdx + 9)
                                else
                                    local parts = {}
                                    for part in string.gmatch(rawEntry, "([^/]+)") do
                                        table.insert(parts, part)
                                    end
                                    for k, part in ipairs(parts) do
                                        if isMhwFolder(part) then
                                            relPath = table.concat(parts, "/", k)
                                            break
                                        end
                                    end
                                end
                                
                                if relPath ~= "" then
                                    local targetFile = API:CombinePath(nativePcDir, relPath)
                                    if API:FileExists(targetFile) then
                                        modFilesTracked:Add(relPath)
                                    else
                                        allExist = false
                                        break
                                    end
                                end
                            end
                        end
                        
                        if allExist and modFilesTracked.Count > 0 then
                            state[filename] = modFilesTracked
                            changes = true
                            API:Log("🔍 Auto-detected manually installed mod: " .. filename)
                        end
                    end)
                end
            end
        end
        
        if changes then
            saveState(GAME_DIR, state)
        end
    end)
end

function applyFilterAndSort()
    local query = string.lower(string.match(API:GetText(tabId, "TxtSearch"), "^%s*(.-)%s*$"))
    local sortIdx = API:GetSelectedIndex(tabId, "ComboSort")
    
    local filtered = {}
    for i, m in ipairs(allRawModItems) do
        if query == "" or string.find(string.lower(m.Filename), query, 1, true) or string.find(string.lower(m.DisplayName), query, 1, true) then
            table.insert(filtered, m)
        end
    end
    
    if sortIdx == 0 then
        table.sort(filtered, function(a, b) return a.DisplayName < b.DisplayName end)
    elseif sortIdx == 1 then
        table.sort(filtered, function(a, b) return b.DateNum.Ticks > a.DateNum.Ticks end)
    elseif sortIdx == 2 then
        table.sort(filtered, function(a, b) return b.SizeNum > a.SizeNum end)
    end
    
    modCollection:Clear()
    for i, m in ipairs(filtered) do
        modCollection:Add(m)
    end
end

function loadMods()
    local allSuccess, allErr = pcall(function()
        autoDetectInstalledMods()
        allRawModItems = {}
        modCollection:Clear()
        if not GAME_DIR or GAME_DIR == "" then return end
        
        local modsDir = API:CombinePath(GAME_DIR, "GameMods")
        if not API:DirectoryExists(modsDir) then
            API:CreateDirectory(modsDir)
        end
    
    local state = loadState(GAME_DIR)
    local installedFilesMap = {}
    
    for kvp in luanet.each(state) do
        local installedList = kvp.Value
        for f = 0, installedList.Count - 1 do
            installedFilesMap[string.lower(installedList[f])] = true
        end
    end
    
    local success, err = pcall(function()
        local files = Directory.GetFiles(modsDir)
        
        for i = 0, files.Length - 1 do
            local filePath = files[i]
            local fi = FileInfo(filePath)
            local ext = string.lower(fi.Extension)
            
            if ext == ".zip" or ext == ".rar" or ext == ".7z" then
                local filename = fi.Name
                local isInstalled = state:ContainsKey(filename)
                local hasConflict = false
                
                if not isInstalled then
                    pcall(function()
                        local archiveFiles = API:GetArchiveContents(filePath)
                        for a = 0, archiveFiles.Count - 1 do
                            local rawEntry = string.gsub(archiveFiles[a], "\\", "/")
                            rawEntry = string.match(rawEntry, "^%s*(.-)%s*$")
                            local lowerEntry = string.lower(rawEntry)
                            local nativeIdx = string.find(lowerEntry, "nativepc/", 1, true)
                            if nativeIdx then
                                local relPath = string.sub(lowerEntry, nativeIdx + 9)
                                if relPath ~= "" and installedFilesMap[relPath] then
                                    hasConflict = true
                                    break
                                end
                            end
                        end
                    end)
                end
                
                local item = API:CreateModItem()
                item.Filename = filename
                item.DisplayName = filename .. (isInstalled and " [Installed]" or "")
                item.Size = string.format("%.2f MB", fi.Length / (1024 * 1024))
                item.SizeNum = fi.Length
                item.DateModified = fi.LastWriteTime:ToString()
                item.DateNum = fi.LastWriteTime
                
                if isInstalled then
                    item.IsInstalled = true
                    item.BackgroundColor = API:GetBrush("#3C27AE60")
                elseif hasConflict then
                    item.IsInstalled = false
                    item.BackgroundColor = API:GetBrush("#3CE74C3C")
                else
                    item.IsInstalled = false
                    item.BackgroundColor = API:GetBrush("#3C95A5A6")
                end
                
                table.insert(allRawModItems, item)
            end
        end
        
        applyFilterAndSort()
        
        if currentModFilename ~= "" then
            if state:ContainsKey(currentModFilename) then
                API:SetIsEnabled(tabId, "BtnInstallChecked", false)
                API:SetIsEnabled(tabId, "BtnUninstallChecked", true)
                API:SetIsEnabled(tabId, "BtnDeleteChecked", true)
            else
                API:SetIsEnabled(tabId, "BtnInstallChecked", true)
                API:SetIsEnabled(tabId, "BtnUninstallChecked", false)
                API:SetIsEnabled(tabId, "BtnDeleteChecked", true)
            end
        end
        end)
        if not success then
            API:Log("Error in inner loadMods: " .. tostring(err))
            API:ShowMessage("Inner Lua Error", tostring(err))
        end
        
        API:Log("Debug: Finished loadMods with " .. tostring(modCollection.Count) .. " items")
    end)
    
    if not allSuccess then
        API:Log("Critical error in loadMods: " .. tostring(allErr))
        API:ShowMessage("Critical Lua Error", tostring(allErr))
    end
end


API:AddTabEvent(tabId, "TxtSearch", "TextChanged", function()
    applyFilterAndSort()
end)

API:AddTabEvent(tabId, "ComboSort", "SelectionChanged", function()
    applyFilterAndSort()
end)

API:AddTabEvent(tabId, "BtnRefresh", "Click", function()
    loadMods()
    API:ShowMessage("Refresh", "Mod Library refreshed!")
end)

API:AddTabEvent(tabId, "BtnValidate", "Click", function()
    if not GAME_DIR or GAME_DIR == "" then return end
    local state = loadState(GAME_DIR)
    local nativePcPath = API:CombinePath(GAME_DIR, "nativePC")
    
    local corruptedMods = {}
    local totalChecked = 0
    
    for kvp in luanet.each(state) do
        local modName = kvp.Key
        totalChecked = totalChecked + 1
        local files = kvp.Value
        local missingCount = 0
        
        for i = 0, files.Count - 1 do
            local targetFile = API:CombinePath(nativePcPath, files[i])
            if not API:FileExists(targetFile) then
                missingCount = missingCount + 1
            end
        end
        
        if missingCount > 0 then
            table.insert(corruptedMods, modName)
            API:Log("❌ Validation failed for " .. modName .. ": " .. tostring(missingCount) .. " files missing! Marking as Uninstalled.")
        end
    end
    
    if #corruptedMods > 0 then
        for i = 1, #corruptedMods do
            state:Remove(corruptedMods[i])
        end
        saveState(GAME_DIR, state)
        loadMods()
        API:ShowMessage("Validation", tostring(#corruptedMods) .. " mod(s) were found corrupted and marked as uninstalled.")
    else
        API:ShowMessage("Validation", "All " .. tostring(totalChecked) .. " installed mod(s) are intact and valid!")
    end
end)

API:AddTabEvent(tabId, "ListMods", "SelectionChanged", function()
    local selectedItem = API:GetSelectedItem(tabId, "ListMods")
    if not selectedItem then
        currentModFilename = ""
        treeCollection:Clear()
        API:SetIsEnabled(tabId, "BtnInstallChecked", false)
        API:SetIsEnabled(tabId, "BtnUninstallChecked", false)
        API:SetIsEnabled(tabId, "BtnDeleteChecked", false)
        return
    end
    
    local filename = selectedItem.Filename
    currentModFilename = filename
    
    local state = loadState(GAME_DIR)
    if state:ContainsKey(filename) then
        API:SetIsEnabled(tabId, "BtnInstallChecked", false)
        API:SetIsEnabled(tabId, "BtnUninstallChecked", true)
        API:SetIsEnabled(tabId, "BtnDeleteChecked", true)
    else
        API:SetIsEnabled(tabId, "BtnInstallChecked", true)
        API:SetIsEnabled(tabId, "BtnUninstallChecked", false)
        API:SetIsEnabled(tabId, "BtnDeleteChecked", true)
    end
    
    treeCollection:Clear()
    API:ShowLoading(tabId, "LoadingRing", true)
    
    API:ExecuteDelayed(10, function()
        pcall(function()
            local fullPath = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), filename)
            local allFiles = API:GetArchiveContents(fullPath)
            local strFiles = {}
            for i = 0, allFiles.Count - 1 do
                table.insert(strFiles, allFiles[i])
            end
            buildTree(strFiles)
        end)
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

function getCheckedFiles(node, checkedList)
    if node.IsChecked ~= false then
        if not node.IsDirectory and node.EntryKey then
            checkedList:Add(node.EntryKey)
        end
        for i = 0, node.Children.Count - 1 do
            getCheckedFiles(node.Children[i], checkedList)
        end
    end
end

function setAllTreeChecked(nodes, isChecked)
    if not nodes then return end
    for i = 0, nodes.Count - 1 do
        nodes[i].IsChecked = isChecked
        if nodes[i].Children and nodes[i].Children.Count > 0 then
            setAllTreeChecked(nodes[i].Children, isChecked)
        end
    end
end

API:AddTabEvent(tabId, "BtnCheckAll", "Click", function()
    for i = 0, modCollection.Count - 1 do
        modCollection[i].IsChecked = true
    end
end)

API:AddTabEvent(tabId, "BtnUncheckAll", "Click", function()
    for i = 0, modCollection.Count - 1 do
        modCollection[i].IsChecked = false
    end
end)

API:AddTabEvent(tabId, "BtnCheckAllTree", "Click", function()
    setAllTreeChecked(treeCollection, true)
end)

API:AddTabEvent(tabId, "BtnUncheckAllTree", "Click", function()
    setAllTreeChecked(treeCollection, false)
end)

function buildOptionTree(optionName, prefix, files, isDefaultSelected)
    local rootNode = API:CreateModTreeNode()
    rootNode.Name = optionName
    rootNode.IsDirectory = true
    rootNode.IsExpanded = true
    rootNode.IsChecked = isDefaultSelected
    
    local dict = {}
    
    for i, file in ipairs(files) do
        local relativePath = file
        if prefix ~= "" and string.find(file, prefix .. "/", 1, true) == 1 then
            relativePath = string.sub(file, string.len(prefix) + 2)
        end
        
        local parts = {}
        for part in string.gmatch(relativePath, "([^/]+)") do
            table.insert(parts, part)
        end
        
        local currentNode = rootNode
        local currentPath = ""
        
        for j, part in ipairs(parts) do
            currentPath = currentPath .. (currentPath ~= "" and "/" or "") .. part
            if not dict[currentPath] then
                local newNode = API:CreateModTreeNode()
                newNode.Name = part
                newNode.IsDirectory = (j < #parts)
                newNode.EntryKey = file
                newNode.Parent = currentNode
                newNode.IsChecked = isDefaultSelected
                
                currentNode.Children:Add(newNode)
                dict[currentPath] = newNode
            end
            currentNode = dict[currentPath]
        end
    end
    
    return rootNode
end

function buildTree(files)
    local optionGroups = {}
    local groupOrder = {}
    local totalFiles = 0
    
    for i, file in ipairs(files) do
        local entry = string.gsub(file, "\\", "/")
        entry = string.match(entry, "^%s*(.-)%s*$")
        if entry ~= "" and string.sub(entry, -1) ~= "/" then
            local lowerEntry = string.lower(entry)
            if string.find(lowerEntry, "__macosx/", 1, true) ~= 1 and not string.find(lowerEntry, ".ds_store", 1, true) then
                local ext = ""
                local lastDot = string.find(lowerEntry, "%.[^%.]*$")
                if lastDot then ext = string.sub(lowerEntry, lastDot) end
                
                if ext ~= ".png" and ext ~= ".jpg" and ext ~= ".jpeg" and ext ~= ".gif" and ext ~= ".bmp" and ext ~= ".txt" and ext ~= ".md" and ext ~= ".url" then
                    if string.find(lowerEntry, "fomod/", 1, true) ~= 1 then
                        local parts = {}
                        for part in string.gmatch(entry, "([^/]+)") do
                            table.insert(parts, part)
                        end
                        
                        local mhwFolderIndex = -1
                        for j, part in ipairs(parts) do
                            if string.lower(part) == "nativepc" or isMhwFolder(part) then
                                mhwFolderIndex = j
                                break
                            end
                        end
                        
                        if mhwFolderIndex >= 1 then
                            local prefix = ""
                            local optionName = ""
                            
                            if mhwFolderIndex > 1 then
                                prefix = table.concat(parts, "/", 1, mhwFolderIndex - 1)
                                optionName = prefix
                            else
                                prefix = ""
                                optionName = "nativePC (Default)"
                            end
                            
                            if not optionGroups[optionName] then
                                optionGroups[optionName] = {}
                                table.insert(groupOrder, optionName)
                            end
                            table.insert(optionGroups[optionName], entry)
                            totalFiles = totalFiles + 1
                        end
                    end
                end
            end
        end
    end
    
    local optionCount = #groupOrder
    API:SetText(tabId, "LblModInfo", currentModFilename .. "\nNativePC Options: " .. tostring(optionCount) .. " | Files: " .. tostring(totalFiles))
    
    for g, key in ipairs(groupOrder) do
        local filesInGroup = optionGroups[key]
        local isDefaultSelected = (g == 1)
        local rootNode = buildOptionTree(key, key ~= "nativePC (Default)" and key or "", filesInGroup, isDefaultSelected)
        treeCollection:Add(rootNode)
    end
end

API:AddTabEvent(tabId, "BtnInstall", "Click", function()
    if currentModFilename == "" or not GAME_DIR or GAME_DIR == "" then return end
    local archivePath = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), currentModFilename)
    
    local selectedFiles = ListString()
    for i = 0, treeCollection.Count - 1 do
        getCheckedFiles(treeCollection[i], selectedFiles)
    end
    
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        installMod(archivePath, GAME_DIR, selectedFiles)
        API:ShowMessage("Install", "Installed " .. currentModFilename .. " successfully!")
        loadMods()
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:AddTabEvent(tabId, "BtnUninstall", "Click", function()
    if currentModFilename == "" or not GAME_DIR or GAME_DIR == "" then return end
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        uninstallMod(currentModFilename, GAME_DIR)
        API:ShowMessage("Uninstall", "Uninstalled " .. currentModFilename .. " successfully!")
        loadMods()
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:AddTabEvent(tabId, "BtnDelete", "Click", function()
    if currentModFilename == "" or not GAME_DIR or GAME_DIR == "" then return end
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        pcall(function()
            local recycleDir = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), ".recycle_mods")
            if not Directory.Exists(recycleDir) then Directory.CreateDirectory(recycleDir) end
            
            local fullPath = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), currentModFilename)
            local targetPath = API:CombinePath(recycleDir, currentModFilename)
            if File.Exists(fullPath) then
                File.Move(fullPath, targetPath, true)
                API:Log("🗑️ Mod moved to recycle bin: " .. currentModFilename)
            end
            loadMods()
        end)
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:AddTabEvent(tabId, "BtnDeleteChecked", "Click", function()
    if not GAME_DIR or GAME_DIR == "" then return end
    local checkedItems = {}
    for i = 0, modCollection.Count - 1 do
        if modCollection[i].IsChecked then
            table.insert(checkedItems, modCollection[i].Filename)
        end
    end
    
    if #checkedItems == 0 then
        API:ShowMessage("Delete", "No mods selected to delete.")
        return
    end
    
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        pcall(function()
            local recycleDir = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), ".recycle_mods")
            if not Directory.Exists(recycleDir) then Directory.CreateDirectory(recycleDir) end
            
            for i, filename in ipairs(checkedItems) do
                local fullPath = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), filename)
                local targetPath = API:CombinePath(recycleDir, filename)
                if File.Exists(fullPath) then
                    File.Move(fullPath, targetPath, true)
                    API:Log("🗑️ Mod moved to recycle bin: " .. filename)
                end
            end
            loadMods()
        end)
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:AddTabEvent(tabId, "BtnInstallChecked", "Click", function()
    if not GAME_DIR or GAME_DIR == "" then return end
    local checkedMods = {}
    for i = 0, modCollection.Count - 1 do
        if modCollection[i].IsChecked then
            table.insert(checkedMods, modCollection[i].Filename)
        end
    end
    
    if #checkedMods == 0 then
        if currentModFilename ~= "" then
            table.insert(checkedMods, currentModFilename)
        else
            API:ShowMessage("Install", "No mods checked in library to install.")
            return
        end
    end
    
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        for i, modFilename in ipairs(checkedMods) do
            local archivePath = API:CombinePath(API:CombinePath(GAME_DIR, "GameMods"), modFilename)
            local selectedFiles = nil
            if modFilename == currentModFilename and treeCollection.Count > 0 then
                selectedFiles = ListString()
                for t = 0, treeCollection.Count - 1 do
                    getCheckedFiles(treeCollection[t], selectedFiles)
                end
            end
            installMod(archivePath, GAME_DIR, selectedFiles)
        end
        API:ShowMessage("Install", "Successfully installed " .. tostring(#checkedMods) .. " mod(s).")
        loadMods()
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:AddTabEvent(tabId, "BtnUninstallChecked", "Click", function()
    if not GAME_DIR or GAME_DIR == "" then return end
    local checkedMods = {}
    for i = 0, modCollection.Count - 1 do
        if modCollection[i].IsChecked then
            table.insert(checkedMods, modCollection[i].Filename)
        end
    end
    
    if #checkedMods == 0 then
        local selectedItem = API:GetSelectedItem(tabId, "ListMods")
        if selectedItem then
            table.insert(checkedMods, selectedItem.Filename)
        else
            API:ShowMessage("Uninstall", "No mods checked in library to uninstall.")
            return
        end
    end
    
    API:ShowLoading(tabId, "LoadingRing", true)
    API:ExecuteDelayed(10, function()
        for i, modFilename in ipairs(checkedMods) do
            uninstallMod(modFilename, GAME_DIR)
        end
        API:ShowMessage("Uninstall", "Successfully uninstalled " .. tostring(#checkedMods) .. " mod(s).")
        loadMods()
        API:ShowLoading(tabId, "LoadingRing", false)
    end)
end)

API:ExecuteDelayed(500, function()
    loadMods()
end)

function parseArchiveFiles(files)
    local optionGroups = {}
    local groupOrder = {}
    
    for i = 0, files.Count - 1 do
        local entry = string.gsub(files[i], "\\", "/")
        entry = string.match(entry, "^%s*(.-)%s*$")
        if entry ~= "" and string.sub(entry, -1) ~= "/" then
            local lowerEntry = string.lower(entry)
            if string.find(lowerEntry, "__macosx/", 1, true) ~= 1 and not string.find(lowerEntry, ".ds_store", 1, true) then
                local optionName = "Mod Files (Default)"
                local nativeIdx = string.find(lowerEntry, "nativepc/", 1, true)
                
                if nativeIdx and nativeIdx > 1 then
                    local prefix = string.sub(entry, 1, nativeIdx - 1)
                    prefix = string.match(prefix, "^%s*(.-)%s*$")
                    while string.sub(prefix, -1) == "/" do
                        prefix = string.sub(prefix, 1, -2)
                    end
                    if prefix ~= "" then
                        optionName = prefix
                    end
                end
                
                if not optionGroups[optionName] then
                    optionGroups[optionName] = {}
                    table.insert(groupOrder, optionName)
                end
                table.insert(optionGroups[optionName], entry)
            end
        end
    end
    
    local result = {}
    for _, groupName in ipairs(groupOrder) do
        local groupFiles = optionGroups[groupName]
        table.insert(result, {
            Name = groupName,
            Files = groupFiles
        })
    end
    
    -- Convert table to JSON
    local JsonSerializer = luanet.import_type("System.Text.Json.JsonSerializer")
    return JsonSerializer.Serialize(result)
end
