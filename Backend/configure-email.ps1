[CmdletBinding()]
param(
    [string]$SenderEmail,
    [string]$SmtpHost,
    [ValidateRange(1, 65535)]
    [int]$SmtpPort = 587,
    [string]$SmtpUsername,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$apiProject = Join-Path $PSScriptRoot 'SportsCenterManagement.API/SportsCenterManagement.API.csproj'
if (-not (Test-Path $apiProject)) { throw 'Khong tim thay tap tin project SportsCenterManagement.API.csproj.' }
$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source

function Invoke-SecretManager {
    param([string]$Operation, [string]$InputJson)
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $dotnetPath
    $startInfo.Arguments = "user-secrets $Operation --project `"$apiProject`""
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        # Send credentials through stdin, never command arguments or a file in the repository.
        if ($InputJson) { $process.StandardInput.WriteLine($InputJson) }
        $process.StandardInput.Close()
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdoutTask.GetAwaiter().GetResult()
        [void]$stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw 'Khong truy cap duoc User Secrets. Kiem tra .NET SDK va quyen tai khoan Windows.'
        }
        return $output
    }
    finally { $process.Dispose() }
}

if ($CheckOnly) {
    $savedSecrets = Invoke-SecretManager -Operation 'list'
    $smtpKeys = @('Host', 'From', 'Username', 'Password')
    foreach ($smtpKey in $smtpKeys) {
        $configKey = "ExternalCommunications:Email:Smtp:$smtpKey"
        $isSet = $savedSecrets -match ('(?m)^' + [regex]::Escape($configKey) + '\s*=\s*\S')
        Write-Host ($configKey + ': ' + $(if ($isSet) { 'Da cau hinh' } else { 'CHUA CAU HINH' }))
    }
    Write-Host 'Chi kiem tra User Secrets cua tai khoan Windows hien tai; chua gui email.'
    return
}

if (-not $SenderEmail) { $SenderEmail = Read-Host 'Email dung de GUI ma OTP' }
$SenderEmail = $SenderEmail.Trim()
try { $senderAddress = New-Object System.Net.Mail.MailAddress($SenderEmail) }
catch { throw 'Dia chi email gui khong hop le.' }
if ($senderAddress.Address -ne $SenderEmail) { throw 'Chi nhap dia chi email, khong kem ten hien thi.' }
if (-not $SmtpHost) { $SmtpHost = Read-Host 'SMTP host (vi du: smtp.gmail.com)' }
if ([string]::IsNullOrWhiteSpace($SmtpHost)) { throw 'SmtpHost khong duoc de trong.' }
if (-not $SmtpUsername) { $SmtpUsername = Read-Host 'SMTP login/username' }
if ([string]::IsNullOrWhiteSpace($SmtpUsername)) { throw 'SMTP login khong duoc de trong.' }

$credentialHint = if ($SmtpHost -eq 'smtp.gmail.com') {
    'Dung dia chi Gmail va App Password; khong dung mat khau dang nhap.'
} else {
    'Dung thong tin SMTP do nha cung cap cap.'
}
Write-Host "SMTP: ${SmtpHost}:$SmtpPort (STARTTLS). $credentialHint"
$securePassword = Read-Host 'SMTP password/key (an ky tu khi nhap)' -AsSecureString
if ($securePassword.Length -eq 0) { throw 'Chua nhap mat khau SMTP.' }
$passwordPointer = [IntPtr]::Zero
$plainPassword = $null
$payload = $null
try {
    $passwordPointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    # Google's app-password screen groups the code with spaces for readability.
    if ($SmtpHost -eq 'smtp.gmail.com') { $plainPassword = $plainPassword -replace '\s', '' }
    $payload = @{
        'ExternalCommunications:Email:WebhookUrl' = ''
        'ExternalCommunications:Email:Smtp:Host' = $SmtpHost.Trim()
        'ExternalCommunications:Email:Smtp:Port' = [string]$SmtpPort
        'ExternalCommunications:Email:Smtp:EnableSsl' = 'true'
        'ExternalCommunications:Email:Smtp:From' = $SenderEmail
        'ExternalCommunications:Email:Smtp:Username' = $SmtpUsername.Trim()
        'ExternalCommunications:Email:Smtp:Password' = $plainPassword
    } | ConvertTo-Json -Compress
    [void](Invoke-SecretManager -Operation 'set' -InputJson $payload)
}
finally {
    if ($passwordPointer -ne [IntPtr]::Zero) { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    $securePassword.Dispose()
    $plainPassword = $null
    $payload = $null
}
Write-Host 'Da luu SMTP vao .NET User Secrets, giu nguyen cac cau hinh khac. Chua gui email.'
Write-Host 'Khoi dong lai backend o Development, sau do thu dang ky/quen mat khau tren giao dien.'
