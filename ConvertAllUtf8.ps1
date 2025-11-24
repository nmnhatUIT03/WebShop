# Convert all files in WebShop project to UTF-8 encoding
$projectRoot = "D:\webfinal\webthoitrang\WebShop"

# Define file extensions to convert
$extensions = @("*.cshtml", "*.cs", "*.json", "*.xml", "*.csproj", "*.config", "*.js", "*.css", "*.html", "*.txt")

$totalFiles = 0
$convertedFiles = 0
$errorFiles = 0

Write-Host "Starting UTF-8 encoding conversion for all files in: $projectRoot" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

foreach($ext in $extensions) {
    Write-Host "`nProcessing $ext files..." -ForegroundColor Yellow
    
    $files = Get-ChildItem -Path $projectRoot -Filter $ext -Recurse -ErrorAction SilentlyContinue
    
    if ($files.Count -eq 0) {
        Write-Host "No $ext files found" -ForegroundColor Gray
        continue
    }
    
    Write-Host "Found $($files.Count) $ext files" -ForegroundColor Green
    
    foreach($file in $files) {
        $totalFiles++
        try {
            # Read file content
            $content = [System.IO.File]::ReadAllText($file.FullName)
            
            # Write back with UTF-8 encoding (without BOM)
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
            
            $convertedFiles++
            Write-Host "? Converted: $($file.Name)" -ForegroundColor Green
        }
        catch {
            $errorFiles++
            Write-Host "? Error converting $($file.Name): $_" -ForegroundColor Red
        }
    }
}

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Conversion Summary:" -ForegroundColor Cyan
Write-Host "Total files scanned: $totalFiles" -ForegroundColor White
Write-Host "Successfully converted: $convertedFiles" -ForegroundColor Green
Write-Host "Errors: $errorFiles" -ForegroundColor $(if ($errorFiles -eq 0) { "Green" } else { "Red" })
Write-Host "`nUTF-8 encoding conversion complete!" -ForegroundColor Cyan
