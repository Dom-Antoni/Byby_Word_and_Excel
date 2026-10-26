$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$cache = Join-Path $PSScriptRoot '.downloads'
$target = Join-Path $PSScriptRoot 'vendor\inno-6.7.3'
$compiler = Join-Path $target 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) {Write-Output "Inno Setup bereit: $compiler"; return}
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$download = Join-Path $cache 'innosetup-6.7.3.exe'
if (!(Test-Path -LiteralPath $download)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732') {throw 'Inno-Setup-Download: SHA256 stimmt nicht.'}
# Official portable mode: no compiler installation, shortcuts or uninstall entry on this PC.
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/PORTABLE=1',('/DIR="'+$target+'"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) {throw "Compiler konnte nicht entpackt werden: $($process.ExitCode)"}
Write-Output "Inno Setup bereit: $compiler"
