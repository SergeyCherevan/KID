$ErrorActionPreference='Stop'
function Assert-ReleasePath([string]$Path, [string]$Root) {
    $full=[IO.Path]::GetFullPath($Path)
    $prefix=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))+[IO.Path]::DirectorySeparatorChar
    if (!$full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside owned release root: $full" }
    for ($current=$full; $current; $current=[IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse path: $current" }
    }
    return $full
}
function Invoke-ReleaseProcess([string]$Executable, [string[]]$Arguments, [string]$Log, [int]$TimeoutSeconds=600) {
    $info=[Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::Start($info)
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    try {
        if (!$process.WaitForExit($TimeoutSeconds*1000)) { $process.Kill($true); $process.WaitForExit(); throw "Timeout: $Executable" }
        [IO.File]::WriteAllText($Log, $stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult())
        $script:releaseCommands += [ordered]@{executable=$Executable;arguments=$Arguments;exitCode=$process.ExitCode;log=$Log}
        if ($process.ExitCode -ne 0) { throw "Child exit $($process.ExitCode): $Executable; $Log" }
    }
    finally {
        if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        if (!(Test-Path -LiteralPath $Log)) { [IO.File]::WriteAllText($Log,$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()) }
        $process.Dispose()
    }
}
function Assert-Authenticode([string]$Path, [string]$Subject, [string]$SignTool, [string]$Log) {
    $signature=Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -ne $Subject -or !$signature.TimeStamperCertificate) { throw "Invalid publisher/trust/timestamp: $Path" }
    Invoke-ReleaseProcess $SignTool @('verify','/pa','/all','/tw','/v',$Path) $Log
    if ((Get-Content -LiteralPath $Log -Raw) -notmatch '(?i)sha256') { throw "SHA-256 signature evidence missing: $Path" }
}
