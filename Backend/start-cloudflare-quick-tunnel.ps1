[CmdletBinding()]
param(
    [ValidatePattern('^https?://')]
    [string]$ApiUrl = 'http://127.0.0.1:5000'
)

$ErrorActionPreference = 'Stop'

$cloudflared = Get-Command cloudflared -ErrorAction SilentlyContinue
$cloudflaredPath = $cloudflared.Source
if (-not $cloudflared) {
    $installedPath = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
    if (Test-Path -LiteralPath $installedPath) {
        $cloudflaredPath = $installedPath
    }
}
if (-not $cloudflaredPath) {
    throw 'Khong tim thay cloudflared. Cai bang: winget install --id Cloudflare.cloudflared -e'
}

try {
    $health = Invoke-WebRequest -Uri "$ApiUrl/health/live" -UseBasicParsing -TimeoutSec 5
    if ($health.StatusCode -ne 200) { throw "Health endpoint tra ve HTTP $($health.StatusCode)." }
}
catch {
    throw "API chua san sang tai $ApiUrl/health/live. Hay khoi dong Backend truoc. Chi tiet: $($_.Exception.Message)"
}

Write-Host 'API da san sang. Cloudflare se in URL https://*.trycloudflare.com ben duoi.' -ForegroundColor Green
Write-Host 'Dat URL do kem /api vao VITE_API_BASE_URL tren Vercel, sau do redeploy Frontend.' -ForegroundColor Yellow
& $cloudflaredPath tunnel --url $ApiUrl
