param(
    [string]$ToolExe = "",
    [string]$DriversDir = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $ToolExe)    { $ToolExe    = Join-Path $root "build\P104Gen2x4.exe" }
if (-not $DriversDir) { $DriversDir = Join-Path $root "P104-2.0x16-v1.2\bin\drivers" }
if (-not $OutDir)     { $OutDir     = Join-Path $root "dist" }

$InstallerSrc = Join-Path $root "src\installer\P104Installer.cs"
$ToolSrc      = Join-Path $root "src\P104Gen2x4.cs"
$Manifest     = Join-Path $root "src\installer\installer.manifest"
$Readme       = Join-Path $root "README.md"
$Notice       = Join-Path $root "NOTICE.md"
$csc          = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw           = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"

foreach ($p in @($ToolSrc, $InstallerSrc, $Manifest, $Readme, $Notice, $csc)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Missing: $p" }
}
if (-not (Test-Path -LiteralPath $ToolExe)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ToolExe) | Out-Null
    Write-Host "Building tool..." -ForegroundColor Cyan
    & $csc /nologo /target:exe /platform:x64 /codepage:65001 /out:$ToolExe /r:"$fw\System.ServiceProcess.dll" $ToolSrc
    if ($LASTEXITCODE -ne 0) { throw "tool build failed with exit code $LASTEXITCODE" }
}
foreach ($d in @("WinRing0x64.sys", "ThrottleStop.sys")) {
    if (-not (Test-Path -LiteralPath (Join-Path $DriversDir $d))) { throw "Missing driver: $d" }
}

$payload  = Join-Path $OutDir "payload"
$drvOut   = Join-Path $payload "drivers"
$srcStage = Join-Path $OutDir "source-staging"
$outExe   = Join-Path $OutDir "P104Gen2x4-Setup.exe"
$outZip   = Join-Path $OutDir "P104Gen2x4-Service-Source.zip"

if (Test-Path -LiteralPath $payload)  { Remove-Item -LiteralPath $payload -Recurse -Force }
if (Test-Path -LiteralPath $srcStage) { Remove-Item -LiteralPath $srcStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $drvOut | Out-Null

Copy-Item -LiteralPath $ToolExe -Destination (Join-Path $payload "P104Gen2x4.exe") -Force
Copy-Item -LiteralPath (Join-Path $DriversDir "WinRing0x64.sys") -Destination (Join-Path $drvOut "WinRing0x64.sys") -Force
Copy-Item -LiteralPath (Join-Path $DriversDir "ThrottleStop.sys") -Destination (Join-Path $drvOut "ThrottleStop.sys") -Force
Copy-Item -LiteralPath $Readme -Destination (Join-Path $payload "README.md") -Force
Copy-Item -LiteralPath $Notice -Destination (Join-Path $payload "NOTICE.md") -Force

$payloadExe = Join-Path $payload "P104Gen2x4.exe"
$payloadWr  = Join-Path $drvOut "WinRing0x64.sys"
$payloadTs  = Join-Path $drvOut "ThrottleStop.sys"
$payloadMd  = Join-Path $payload "README.md"
$payloadNo  = Join-Path $payload "NOTICE.md"

$cscArgs = @(
    "/nologo", "/target:winexe", "/platform:anycpu", "/codepage:65001", "/optimize+",
    "/win32manifest:`"$Manifest`"",
    "/out:`"$outExe`"",
    "/r:`"$fw\System.dll`"",
    "/r:`"$fw\System.Core.dll`"",
    "/r:`"$fw\System.Windows.Forms.dll`"",
    "/r:`"$fw\System.Drawing.dll`"",
    "/r:`"$fw\System.ServiceProcess.dll`"",
    "/resource:`"$payloadExe`",P104Gen2x4.exe",
    "/resource:`"$payloadWr`",WinRing0x64.sys",
    "/resource:`"$payloadTs`",ThrottleStop.sys",
    "/resource:`"$payloadMd`",README.md",
    "/resource:`"$payloadNo`",NOTICE.md",
    "`"$InstallerSrc`""
)

Write-Host "Building installer..." -ForegroundColor Cyan
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

$hashLines = @()
foreach ($f in @($outExe, $payloadExe, $payloadWr, $payloadTs)) {
    $h = (Get-FileHash -Algorithm SHA256 -LiteralPath $f).Hash.ToUpper()
    $hashLines += "$h  $([IO.Path]::GetFileName($f))"
}

Write-Host "Staging source zip..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $srcStage | Out-Null
foreach ($name in @("src", "tools", "docs", "P104-2.0x16-v1.2")) {
    $src = Join-Path $root $name
    if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $srcStage $name) -Recurse -Force }
}
foreach ($name in @("README.md", "README.zh-CN.md", "NOTICE.md", "build-installer.ps1", "P104-methodology-AI-agent.md", "RELEASE_NOTES_v3.3.md", "RELEASE_NOTES_v3.4.md", "RELEASE_NOTES_v3.4.1.md")) {
    $src = Join-Path $root $name
    if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $srcStage $name) -Force }
}
if (Test-Path -LiteralPath $outZip) { Remove-Item -LiteralPath $outZip -Force }
Compress-Archive -Path (Join-Path $srcStage "*") -DestinationPath $outZip -CompressionLevel Optimal -Force
Remove-Item -LiteralPath $srcStage -Recurse -Force

$zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outZip).Hash.ToUpper()
$hashLines += "$zipHash  P104Gen2x4-Service-Source.zip"
$hashLines | Set-Content -LiteralPath (Join-Path $OutDir "SHA256.txt") -Encoding ASCII

Write-Host ""
Write-Host "OK" -ForegroundColor Green
Write-Host "  $outExe"
Write-Host "  $outZip"
Write-Host "  $(Join-Path $OutDir 'SHA256.txt')"
$hashLines | ForEach-Object { Write-Host "  $_" }
