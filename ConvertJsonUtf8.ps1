# Convert all .json files to UTF-8 encoding
$projectPath = "D:\webfinal\webthoitrang\WebShop"
$jsonFiles = Get-ChildItem -Path $projectPath -Filter "*.json" -Recurse

Write-Host "Found $($jsonFiles.Count) .json files to convert" -ForegroundColor Yellow

foreach($file in $jsonFiles) {
    try {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Converted: $($file.FullName)" -ForegroundColor Green
    }
    catch {
        Write-Host "Error converting $($file.FullName): $_" -ForegroundColor Red
    }
}

Write-Host "JSON files encoding conversion complete!" -ForegroundColor Cyan
