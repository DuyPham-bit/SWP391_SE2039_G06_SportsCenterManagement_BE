$scriptPath = "$PSScriptRoot\update-skills.ps1"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -File `"$scriptPath`""
$trigger = New-ScheduledTaskTrigger -Daily -At 9:00AM
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Register-ScheduledTask -TaskName "AntigravityUpdateSkillsDaily" -Action $action -Trigger $trigger -Settings $settings -Description "Auto update taste-skill and ux-ui-promax daily for Antigravity" -Force
Write-Output "Successfully registered Windows Scheduled Task: AntigravityUpdateSkillsDaily"
