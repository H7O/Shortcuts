$output = Join-Path $PSScriptRoot 'deploy\release'
$exe = Join-Path $output 'Shortcuts.exe'

# The app stays loaded once started, and a running exe cannot be replaced. If it is running from
# the output folder, ask it to exit first, and start it again once the new build is in place.
$running = @(Get-Process Shortcuts -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
if ($running) {
    Start-Process $exe '--exit' -Wait
    foreach ($process in $running) {
        # A version from before --exit existed ignores the request, so it has to be ended the hard way.
        if (-not $process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
    }
}

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($running) {
    Start-Process $exe '--hidden'
}
