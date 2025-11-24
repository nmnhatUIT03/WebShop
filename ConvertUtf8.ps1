# Convert all .cshtml files to UTF-8 encoding
$viewsPath = "D:\webfinal\webthoitrang\WebShop\Views"
$files = Get-ChildItem -Path $viewsPath -Recurse -Filter "*.cshtml"

Write-Host "Found $($files.Count) .cshtml files to convert"

foreach($file in $files) {
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Converted: $($file.FullName)" -ForegroundColor Green
    }
    catch {
        Write-Host "Error converting $($file.FullName): $_" -ForegroundColor Red
    }
}

Write-Host "UTF-8 encoding conversion complete!" -ForegroundColor Cyan
