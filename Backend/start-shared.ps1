[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 5000,
    [string]$SqlServer,
    [string]$Database = 'SportsCenterManagement',
    [switch]$SkipFrontendBuild,
    [string]$SqlServiceName = 'MSSQLSERVER',
    [switch]$SkipSqlServerCheck
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-SqlServerReady {
    param([string]$ServiceName)

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $service) {
        Write-Warning "Khong tim thay dich vu SQL Server '$ServiceName'. Bo qua kiem tra vi co the ban dang dung SQL Server tu xa hoac LocalDB."
        return
    }

    if ($service.Status -eq 'Running') {
        Write-Host "SQL Server ($ServiceName) dang chay."
        return
    }

    if (-not (Test-IsAdministrator)) {
        $starter = Join-Path (Split-Path $PSScriptRoot -Parent) 'start-sql-server.bat'
        throw "SQL Server ($ServiceName) dang dung nen API se khong mo cong $Port. Hay chay '$starter' bang quyen Administrator, sau do chay lai script nay."
    }

    Write-Host "Dang khoi dong SQL Server ($ServiceName)..."
    try {
        Start-Service -Name $ServiceName -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Seconds 1
            $service.Refresh()
        } while ($service.Status -ne 'Running' -and (Get-Date) -lt $deadline)
    }
    catch {
        throw "Khong the khoi dong SQL Server ($ServiceName): $($_.Exception.Message)"
    }

    if ($service.Status -ne 'Running') {
        throw "SQL Server ($ServiceName) khong san sang sau 30 giay. Kiem tra Windows Event Viewer va cau hinh SQL Server."
    }

    Write-Host "SQL Server ($ServiceName) da san sang."
}

if (-not $SkipSqlServerCheck) {
    Ensure-SqlServerReady -ServiceName $SqlServiceName
}

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
