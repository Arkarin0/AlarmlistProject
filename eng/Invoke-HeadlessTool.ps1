# Invoke native preparation tools without PowerShell allocating a console.
# The UI fixture owns the surrounding process job and its descendants.
function ConvertTo-HeadlessArgument([AllowEmptyString()][string]$Value) {
    $quoted = New-Object Text.StringBuilder
    [void]$quoted.Append('"')
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $slashes++; continue }
        $count = if ($character -eq '"') { $slashes * 2 + 1 } else { $slashes }
        [void]$quoted.Append([char]'\', $count)
        [void]$quoted.Append($character)
        $slashes = 0
    }
    [void]$quoted.Append([char]'\', $slashes * 2)
    [void]$quoted.Append('"')
    return $quoted.ToString()
}

function Invoke-HeadlessTool {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [AllowEmptyCollection()][string[]]$Arguments = @(),
        [string]$WorkingDirectory = (Get-Location).ProviderPath
    )
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $FilePath
    $start.Arguments = ($Arguments | ForEach-Object { ConvertTo-HeadlessArgument $_ }) -join ' '
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    $started = $false
    try {
        $started = $process.Start()
        $process.StandardInput.Close()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        while (!$process.WaitForExit(100)) { }
        [Console]::Out.Write($stdout.GetAwaiter().GetResult())
        [Console]::Error.Write($stderr.GetAwaiter().GetResult())
        $global:LASTEXITCODE = $process.ExitCode
        return $process.ExitCode
    } finally {
        if ($started -and !$process.HasExited) {
            if ($PSVersionTable.PSEdition -eq 'Core') { $process.Kill($true) } else { $process.Kill() }
            [void]$process.WaitForExit(10000)
        }
        $process.Dispose()
    }
}
