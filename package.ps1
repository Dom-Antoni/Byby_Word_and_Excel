$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'dist'
foreach ($required in @('Schreib.exe','Tabelle.exe','runtime/bin/java.exe','languagetool/languagetool-server.jar','synonyms/openthesaurus.txt','synonyms/dict/data.noun','synonyms/dict/LICENSE.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $output $required))) { throw "Offline-Paket unvollständig: $required. setup-offline.ps1 und build.ps1 ausführen." }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$target = Join-Path $output 'Byby_Word_and_Excel-Windows-x64-offline.zip'
$temporary = Join-Path $output ('package-' + [guid]::NewGuid().ToString('N') + '.zip')
$archive = [System.IO.Compression.ZipFile]::Open($temporary, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('Schreib.exe','Tabelle.exe')) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $output $name), $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    foreach ($name in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md')) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $PSScriptRoot $name), $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    foreach ($name in @('runtime','languagetool','synonyms')) {
        Get-ChildItem -LiteralPath (Join-Path $output $name) -File -Recurse | ForEach-Object {
            $relative = $_.FullName.Substring($output.Length + 1).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    $manifest = Join-Path $PSScriptRoot 'vendor/downloads.json'
    if (Test-Path -LiteralPath $manifest) { [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $manifest, 'component-checksums.json') | Out-Null }
}
finally { $archive.Dispose() }
Move-Item -LiteralPath $temporary -Destination $target -Force
Write-Output "Offline-Paket: $target"
