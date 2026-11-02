[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 54162,
    [string]$SqlServer,
    [string]$Database = 'SportsCenterManagement',
    [switch]$SkipFrontendBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = if (Test-Path (Join-Path $PSScriptRoot 'Frontend')) {
    [System.IO.Path]::GetFullPath($PSScriptRoot)
} else {
    [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
}
$frontendRoot = Join-Path $projectRoot 'Frontend'
$apiProject = Join-Path $projectRoot 'Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj'
$webOutput = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'Backend/SportsCenterManagement.API/wwwroot'))
if (-not $webOutput.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Frontend build output is outside the project.'
}

if (-not $SkipFrontendBuild) {
    $previousApiBase = $env:VITE_API_BASE_URL
    try {
        # Every browser uses this backend; do not build a machine-specific API URL.
        $env:VITE_API_BASE_URL = '/api'
        Push-Location $frontendRoot
        npm.cmd run build:api
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    }
    finally {
        Pop-Location
        $env:VITE_API_BASE_URL = $previousApiBase
    }
}

$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDemoSeed = $env:Database__SeedDemoData
$previousSqlConnection = $env:ConnectionStrings__SportsCenter
try {
    # Load this machine's existing Development configuration and User Secrets.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__SeedDemoData = 'false'
    if ($SqlServer) {
        # Credentials are supplied outside the script; Windows Authentication is the default.
        $env:ConnectionStrings__SportsCenter = "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
    }
    $existingConns = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    if ($existingConns) {
        $pids = $existingConns.OwningProcess | Select-Object -Unique
        foreach ($p in $pids) {
            $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
            if ($proc -and ($proc.ProcessName -like '*SportsCenterManagement*' -or $proc.ProcessName -eq 'dotnet')) {
                Write-Host "Dang giai phong port $Port tu tien trinh cu (PID: $p)..."
                Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 500
            }
        }
    }

    Write-Host "May nay: http://localhost:$Port"
    Write-Host "May khac trong cung mang: http://${env:COMPUTERNAME}:$Port (hoac dia chi IP cua may nay)."
    Write-Host 'Dung cung URL backend de moi may doc/ghi chung SQL. Ctrl+C de dung.'
    dotnet run --project $apiProject --no-launch-profile --urls "http://0.0.0.0:$Port"
    if ($LASTEXITCODE -ne 0) { throw 'Backend stopped with an error. Check port availability and SQL/JWT configuration.' }
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:Database__SeedDemoData = $previousDemoSeed
    $env:ConnectionStrings__SportsCenter = $previousSqlConnection
}
