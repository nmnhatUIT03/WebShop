# Replace all font declarations in Views to use Roboto instead of Noto Sans/Lora
$viewsPath = "D:\webfinal\webthoitrang\WebShop\Views"
$files = Get-ChildItem -Path $viewsPath -Recurse -Filter "*.cshtml"

Write-Host "Starting font replacement in Views folder..." -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$totalFiles = 0
$replacedFiles = 0

foreach($file in $files) {
    $totalFiles++
    try {
        # Read file content
        $content = [System.IO.File]::ReadAllText($file.FullName)
        $originalContent = $content
        
        # Replace Noto Sans/Lora with Roboto in inline styles
        $content = $content -replace "'Noto Sans', 'Noto Sans CJK VF', sans-serif", "'Roboto', sans-serif"
        $content = $content -replace "'Lora', 'Noto Sans CJK VF', serif", "'Roboto', sans-serif"
        $content = $content -replace "'Noto Sans CJK VF'", "'Roboto'"
        $content = $content -replace "font-family: 'Lora'", "font-family: 'Roboto'"
        $content = $content -replace "font-family: 'Noto Sans'", "font-family: 'Roboto'"
        
        # Replace in CSS classes
        $content = $content -replace "font-family: 'Noto Sans', 'Noto Sans CJK VF'", "font-family: 'Roboto'"
        $content = $content -replace "font-family: 'Lora', 'Noto Sans CJK VF'", "font-family: 'Roboto'"
        
        # Only write if content changed
        if ($content -ne $originalContent) {
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
            $replacedFiles++
            Write-Host "? Updated: $($file.Name)" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "? Error updating $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Font replacement summary:" -ForegroundColor Cyan
Write-Host "Total files scanned: $totalFiles" -ForegroundColor White
Write-Host "Files updated: $replacedFiles" -ForegroundColor Green
Write-Host "`nFont replacement complete!" -ForegroundColor Cyan
