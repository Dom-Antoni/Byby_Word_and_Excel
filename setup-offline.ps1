$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$cache = Join-Path $PSScriptRoot '.downloads'
$vendor = Join-Path $PSScriptRoot 'vendor'
New-Item -ItemType Directory -Force -Path $cache, $vendor | Out-Null
function Download-File([string]$url, [string]$name, [string]$sha = '') {
    $target = Join-Path $cache $name
    if (!(Test-Path -LiteralPath $target)) {
        Write-Host "Lade $name …"
        Invoke-WebRequest -Uri $url -OutFile ($target + '.part')
        Move-Item -LiteralPath ($target + '.part') -Destination $target
    }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sha -and $actual -ne $sha) { throw "SHA256 stimmt nicht: $name" }
    return $target
}
$javaZip = Download-File 'https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.12.1%2B1/OpenJDK21U-jre_x64_windows_hotspot_21.0.12.1_1.zip' 'temurin21.zip' 'd35f31e712f0fcf6ac5a093edc90204fbff22f720ba3950bd09d331d5e621636'
$ltZip = Download-File 'https://languagetool.org/download/LanguageTool-6.6.zip' 'LanguageTool-6.6.zip' '53600506b399bb5ffe1e4c8dec794fd378212f14aaf38ccef9b6f89314d11631'
$deZip = Download-File 'https://www.openthesaurus.de/export/OpenThesaurus-Textversion.zip' 'OpenThesaurus-Textversion.zip'
$enTar = Download-File 'https://wordnetcode.princeton.edu/wn3.1.dict.tar.gz' 'wn3.1.dict.tar.gz' '3f7d8be8ef6ecc7167d39b10d66954ec734280b5bdcd57f7d9eafe429d11c22a'
if (!(Test-Path -LiteralPath (Join-Path $vendor 'jdk-21.0.12.1+1-jre\bin\java.exe'))) { Expand-Archive -LiteralPath $javaZip -DestinationPath $vendor -Force }
if (!(Test-Path -LiteralPath (Join-Path $vendor 'LanguageTool-6.6\languagetool-server.jar'))) { Expand-Archive -LiteralPath $ltZip -DestinationPath $vendor -Force }
$synonyms = Join-Path $vendor 'synonyms'
New-Item -ItemType Directory -Force -Path $synonyms | Out-Null
Expand-Archive -LiteralPath $deZip -DestinationPath $synonyms -Force
& (Join-Path $env:WINDIR 'System32\tar.exe') -xzf $enTar -C $synonyms
if ($LASTEXITCODE -ne 0) { throw 'WordNet konnte nicht entpackt werden.' }
Get-Content -LiteralPath (Join-Path $synonyms 'dict\data.adj') -TotalCount 29 | ForEach-Object { $_ -replace '^\s+\d+\s', '' } | Set-Content -LiteralPath (Join-Path $synonyms 'dict\LICENSE.txt') -Encoding utf8
$manifest = @($javaZip, $ltZip, $deZip, $enTar) | ForEach-Object { @{file=(Split-Path $_ -Leaf);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()} }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $vendor 'downloads.json') -Encoding utf8
Write-Output 'Offline-Komponenten bereit. Jetzt build.ps1 ausführen.'
