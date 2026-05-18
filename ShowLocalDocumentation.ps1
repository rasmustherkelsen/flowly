dotnet tool restore

Start-Job {
    $url = "http://localhost:8080/docs/"
    while ($true) {
        try {
            $null = Invoke-WebRequest -Uri $url -TimeoutSec 2 -ErrorAction Stop
            break
        } catch {
            Start-Sleep 1
        }
    }
    Start-Process $url
} | Out-Null

dotnet tool run docfx docfx.json --serve
