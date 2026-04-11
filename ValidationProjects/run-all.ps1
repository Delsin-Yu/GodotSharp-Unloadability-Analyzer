# run-all.ps1 — Execute all validation plugins through PluginHost and report results.
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Continue'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding  = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$repoRoot   = Split-Path $PSScriptRoot -Parent
$slnPath    = Join-Path $PSScriptRoot 'ValidationProjects.slnx'
$hostProj   = Join-Path $PSScriptRoot 'PluginHost'
$pluginsDir = Join-Path $PSScriptRoot 'Plugins'

# ── Build ────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host '=== Building solution ===' -ForegroundColor Cyan
    dotnet build $slnPath -c Debug --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'BUILD FAILED' -ForegroundColor Red
        exit 1
    }
    Write-Host ''
}

# ── Plugin list (ordered) ────────────────────────────────────────────────
$plugins = @(
    'Plugin.Baseline'
    'Plugin.ThreadStatic'
    'Plugin.ExternalStaticEvent'
    'Plugin.GCHandle'
    'Plugin.GCHandleCleanup'
    'Plugin.MarshalFnPtr'
    'Plugin.ThreadPoolRegisterWait'
    'Plugin.SystemTextJson'
    'Plugin.SystemTextJsonClearCache'
    'Plugin.SystemTextJsonClearCacheComplex'
    'Plugin.NewtonsoftJson'
    'Plugin.NewtonsoftJsonPinPointClear'
    'Plugin.NewtonsoftJsonPinPointClearComplex'
    'Plugin.NewtonsoftJsonBruteForceClear'
    'Plugin.XmlSerializer'
    'Plugin.TypeDescriptor'
    'Plugin.TypeDescriptorCleanup'
    'Plugin.ThreadCreation'
    'Plugin.TimerCreation'
    'Plugin.EncodingRegisterProvider'
    'Plugin.EncodingRegisterProviderCleanup'
    'Plugin.TaskRun'
    'Plugin.ThreadPoolQueueWork'
    'Plugin.MarshalFnPtrStatic'
    'Plugin.MethodHandleGetFnPtr'
    'Plugin.FunctionPointer'
    'Plugin.NewtonsoftJsonPinPointClearConcurrent'
    'Plugin.SystemTextJsonClearCacheConcurrent'
    'Plugin.NewtonsoftJsonPinPointClearCycleA'
    'Plugin.NewtonsoftJsonPinPointClearCycleB'
    'Plugin.TypeDescriptorCycleA'
    'Plugin.TypeDescriptorCycleB'
    'Plugin.EncodingCycleA'
    'Plugin.EncodingCycleB'
)

# ── Run each plugin ─────────────────────────────────────────────────────
$results = @()
$index = 0
foreach ($name in $plugins) {
    $index++
    $dll = Join-Path $pluginsDir "$name\bin\Debug\net10.0\$name.dll"
    if (-not (Test-Path $dll)) {
        Write-Host "[$index/$($plugins.Count)] $name — DLL NOT FOUND: $dll" -ForegroundColor Red
        $results += [pscustomobject]@{ Index=$index; Plugin=$name; Result='ERROR'; Output='DLL not found' }
        continue
    }

    Write-Host "[$index/$($plugins.Count)] $name ..." -NoNewline
    $output = ''
    $proc = Start-Process -FilePath 'dotnet' `
        -ArgumentList "run --project `"$hostProj`" -- `"$dll`"" `
        -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput "$env:TEMP\pluginhost_stdout.txt" `
        -RedirectStandardError  "$env:TEMP\pluginhost_stderr.txt" `
        -ErrorAction SilentlyContinue

    # Apply timeout — kill if still running after 30s
    if (-not $proc.HasExited) {
        $proc.WaitForExit(30000) | Out-Null
        if (-not $proc.HasExited) {
            $proc.Kill()
            $output = (Get-Content "$env:TEMP\pluginhost_stdout.txt" -Raw -ErrorAction SilentlyContinue) + ' [TIMEOUT]'
            Write-Host " TIMEOUT" -ForegroundColor Yellow
            $results += [pscustomobject]@{ Index=$index; Plugin=$name; Result='TIMEOUT'; Output=$output.Trim() }
            continue
        }
    }

    $stdout = Get-Content "$env:TEMP\pluginhost_stdout.txt" -Raw -ErrorAction SilentlyContinue
    $stderr = Get-Content "$env:TEMP\pluginhost_stderr.txt" -Raw -ErrorAction SilentlyContinue
    $output = ("$stdout`n$stderr").Trim()
    $exitCode = $proc.ExitCode

    switch ($exitCode) {
        0       { $result = 'PASS';  $color = 'Green'  }
        1       { $result = 'FAIL';  $color = 'Red'    }
        default { $result = 'ERROR'; $color = 'Yellow' }
    }
    Write-Host " $result" -ForegroundColor $color
    $results += [pscustomobject]@{ Index=$index; Plugin=$name; Result=$result; Output=$output }
}

# ── Summary ─────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '=== SUMMARY ===' -ForegroundColor Cyan
Write-Host ''
Write-Host ('{0,-4} {1,-35} {2,-8} {3}' -f '#', 'Plugin', 'Result', 'Output')
Write-Host ('{0,-4} {1,-35} {2,-8} {3}' -f '---', ('─' * 35), '──────', ('─' * 40))
foreach ($r in $results) {
    $color = switch ($r.Result) { 'PASS' {'Green'} 'FAIL' {'Red'} default {'Yellow'} }
    $shortOutput = if ($r.Output.Length -gt 80) { $r.Output.Substring(0,77) + '...' } else { $r.Output }
    Write-Host ('{0,-4} {1,-35} ' -f $r.Index, $r.Plugin) -NoNewline
    Write-Host ('{0,-8}' -f $r.Result) -ForegroundColor $color -NoNewline
    Write-Host " $shortOutput"
}

# Return results for piping
$results
