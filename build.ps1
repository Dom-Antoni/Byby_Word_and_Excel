param([string]$OutputDirectory='dist')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $PSScriptRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Data.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Xml.Linq.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $wpf $_) }
foreach ($app in @('Schreib', 'Tabelle')) {
    $sources = @((Join-Path $PSScriptRoot 'src\Common.cs'), (Join-Path $PSScriptRoot 'src\Theme.cs'), (Join-Path $PSScriptRoot 'src\Formats.cs'), (Join-Path $PSScriptRoot ('src\' + $app + '.cs')))
    if ($app -eq 'Schreib') { $sources += @('Writing.cs','Grammar.cs','Synonyms.cs','WritingPanel.cs','WritingSettingsWindow.cs','DocumentEditor.cs','DocumentFiles.cs','References.cs','ReferencePanel.cs','Completion.cs','ReferenceStore.cs','ReferenceLookup.cs','ReferencePickerWindow.cs') | ForEach-Object { Join-Path $PSScriptRoot ('src\' + $_) } }
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ ('/out:' + (Join-Path $output ($app + '.exe'))) $references $sources
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen: $app" }
}
$components = @(@('jdk-21.0.12.1+1-jre', 'runtime'), @('LanguageTool-6.6', 'languagetool'), @('synonyms', 'synonyms'))
foreach ($component in $components) {
    $source = Join-Path (Join-Path $PSScriptRoot 'vendor') $component[0]
    $target = Join-Path $output $component[1]
    if (Test-Path -LiteralPath $source) {
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        & (Join-Path $env:WINDIR 'System32\robocopy.exe') $source $target /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Offline-Komponente konnte nicht kopiert werden: $source" }
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $output -Force
Write-Output "Fertig: $output"

