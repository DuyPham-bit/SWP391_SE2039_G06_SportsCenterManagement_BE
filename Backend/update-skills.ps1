# Auto-update script for Antigravity skills: taste-skill & ux-ui-promax
$ErrorActionPreference = "SilentlyContinue"
$logFile = "$env:USERPROFILE\.gemini\config\update-skills.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Output "[$timestamp] Starting daily skills update..."
Add-Content -Path $logFile -Value "[$timestamp] Starting daily skills update..."

try {
    # 1. Update from official repositories via skills CLI
    $updateOutput = npx -y skills update -g 2>&1
    Write-Output $updateOutput
    Add-Content -Path $logFile -Value "[$timestamp] Update output: $updateOutput"

    # 2. Sync updated files to Antigravity global customization directory
    $agentsDir = "$env:USERPROFILE\.agents\skills"
    $geminiDir = "$env:USERPROFILE\.gemini\config\skills"

    if (Test-Path "$agentsDir\ui-ux-pro-max") {
        if (!(Test-Path "$geminiDir\ux-ui-promax")) { New-Item -ItemType Directory -Path "$geminiDir\ux-ui-promax" -Force | Out-Null }
        Copy-Item -Path "$agentsDir\ui-ux-pro-max\*" -Destination "$geminiDir\ux-ui-promax\" -Recurse -Force
        $promaxFile = "$geminiDir\ux-ui-promax\SKILL.md"
        if (Test-Path $promaxFile) {
            (Get-Content $promaxFile) -replace 'name: ui-ux-pro-max', 'name: ux-ui-promax' | Set-Content $promaxFile
        }
    }

    if (Test-Path "$agentsDir\design-taste-frontend") {
        if (!(Test-Path "$geminiDir\taste-skill")) { New-Item -ItemType Directory -Path "$geminiDir\taste-skill" -Force | Out-Null }
        Copy-Item -Path "$agentsDir\design-taste-frontend\*" -Destination "$geminiDir\taste-skill\" -Recurse -Force
        $tasteFile = "$geminiDir\taste-skill\SKILL.md"
        if (Test-Path $tasteFile) {
            (Get-Content $tasteFile) -replace 'name: design-taste-frontend', 'name: taste-skill' | Set-Content $tasteFile
        }
    }

    Write-Output "[$timestamp] Daily skills update completed successfully."
    Add-Content -Path $logFile -Value "[$timestamp] Daily skills update completed successfully."
} catch {
    Write-Output "[$timestamp] Error during update: $_"
    Add-Content -Path $logFile -Value "[$timestamp] Error during update: $_"
}
