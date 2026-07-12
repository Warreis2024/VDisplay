param(
    [ValidateSet("Ping", "GetStatus", "StartDriver", "StopDriver")]
    [string]$Command = "StartDriver"
)

$ErrorActionPreference = "Stop"

$commandMap = @{
    Ping = 0
    GetStatus = 1
    StartDriver = 2
    StopDriver = 3
}

$request = @{ command = $commandMap[$Command]; payload = $null } | ConvertTo-Json -Compress
$bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
$len = [BitConverter]::GetBytes([int32]$bytes.Length)

$client = New-Object System.IO.Pipes.NamedPipeClientStream(".", "VDisplay.Service.Pipe", [System.IO.Pipes.PipeDirection]::InOut)
try {
    $client.Connect(5000)
    $client.Write($len, 0, 4)
    $client.Write($bytes, 0, $bytes.Length)
    $client.Flush()

    $lenBuf = New-Object byte[] 4
    $read = 0
    while ($read -lt 4) { $read += $client.Read($lenBuf, $read, 4 - $read) }
    $respLen = [BitConverter]::ToInt32($lenBuf, 0)
    $respBuf = New-Object byte[] $respLen
    $read = 0
    while ($read -lt $respLen) { $read += $client.Read($respBuf, $read, $respLen - $read) }

    $response = [System.Text.Encoding]::UTF8.GetString($respBuf)
    Write-Host $response
}
finally {
    $client.Dispose()
}

if ($Command -eq "StartDriver") {
    Start-Sleep -Seconds 2
    Get-PnpDevice -Class Display -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -like "*VDisplay*" -or $_.InstanceId -like "*VDisplay*" } |
        Format-Table Status, FriendlyName, InstanceId -AutoSize
}
