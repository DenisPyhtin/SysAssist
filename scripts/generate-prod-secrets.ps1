param(
    [int]$BootstrapPasswordLength = 28
)

$ErrorActionPreference = "Stop"

function New-Base64Secret {
    param([int]$Bytes)

    $buffer = [byte[]]::new($Bytes)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
    [Convert]::ToBase64String($buffer)
}

function New-Password {
    param([int]$Length)

    $alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^*-_=+"
    $bytes = [byte[]]::new($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    -join ($bytes | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

$jwtSecret = New-Base64Secret 48
$encryptionKey = New-Base64Secret 32
$bootstrapPassword = New-Password ([Math]::Max(16, $BootstrapPasswordLength))

@"
# Paste these values into .env.production or the server secret manager.
# Keep the real file out of Git.

Auth__JwtSecret=$jwtSecret
Security__SecretEncryptionKey=$encryptionKey
SysAssist__BootstrapAdminPassword=$bootstrapPassword
SYSASSIST_SMOKE_ADMIN_PASSWORD=$bootstrapPassword

# Licensing__PublicKey and Licensing__LicenseKey must come from the signed SysAssist license issuer.
"@
