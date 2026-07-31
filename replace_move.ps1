$files = Get-ChildItem -Path . -Recurse -Filter *.cs | Where-Object { $_.Name -ne 'FileHelper.cs' }
foreach ($file in $files) {
    $content = [IO.File]::ReadAllText($file.FullName)
    $original = $content
    $content = $content -replace '\bSystem\.IO\.File\.Move\b', 'ModTogether.API.FileHelper.SafeMove'
    $content = $content -replace '(?<![\.\w])File\.Move\b', 'ModTogether.API.FileHelper.SafeMove'
    
    $content = $content -replace '\bSystem\.IO\.Directory\.Move\b', 'ModTogether.API.FileHelper.SafeMoveDirectory'
    $content = $content -replace '(?<![\.\w])Directory\.Move\b', 'ModTogether.API.FileHelper.SafeMoveDirectory'
    
    if ($content -cne $original) {
        Write-Host "Updated $($file.FullName)"
        [IO.File]::WriteAllText($file.FullName, $content, [System.Text.Encoding]::UTF8)
    }
}
