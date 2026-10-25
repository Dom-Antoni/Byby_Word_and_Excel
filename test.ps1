param([switch]$Integration,[switch]$DocumentsOnly,[switch]$ViewsOnly,[switch]$TablesOnly,[switch]$PlotsOnly,[switch]$OnlineReferences,[string]$OutputDirectory='dist')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$output=Join-Path $PSScriptRoot $OutputDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$themeSource=Join-Path $PSScriptRoot 'src\Theme.cs'
$plotSources=@('src/PlotData.cs','src/PlotGrouping.cs','src/PlotDrawing.cs','src/PlotPanel.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$references = @('System.dll','System.Core.dll','System.Data.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
if (!$DocumentsOnly -and !$ViewsOnly) {
if (!$PlotsOnly) {
& $compiler /nologo /target:exe /main:Checks ('/out:' + (Join-Path $output 'Checks.exe')) $references $themeSource $plotSources (Join-Path $PSScriptRoot 'src\Common.cs') (Join-Path $PSScriptRoot 'src\Formats.cs') (Join-Path $PSScriptRoot 'src\SheetFiles.cs') (Join-Path $PSScriptRoot 'src\Tabelle.cs') (Join-Path $PSScriptRoot 'tests\Checks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tests konnten nicht gebaut werden.' }
& (Join-Path $output 'Checks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
$sheetSources=@('src/Common.cs','src/Formats.cs','src/SheetFiles.cs','src/Tabelle.cs','tests/SheetChecks.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler /nologo /target:exe /main:SheetChecks ('/out:'+(Join-Path $output 'SheetChecks.exe')) $references $themeSource $plotSources $sheetSources
if ($LASTEXITCODE -ne 0) {throw 'Tabellenprüfungen konnten nicht gebaut werden.'}
& (Join-Path $output 'SheetChecks.exe')
if ($LASTEXITCODE -ne 0) {throw 'Tabellenprüfungen fehlgeschlagen.'}
}
$plotCheckSources=@('src/Common.cs','src/Formats.cs','src/SheetFiles.cs','src/Tabelle.cs','tests/PlotChecks.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler /nologo /target:exe /main:PlotChecks ('/out:'+(Join-Path $output 'PlotChecks.exe')) $references $themeSource $plotSources $plotCheckSources
if ($LASTEXITCODE -ne 0) {throw 'Plotprüfungen konnten nicht gebaut werden.'}
& (Join-Path $output 'PlotChecks.exe')
if ($LASTEXITCODE -ne 0) {throw 'Plotprüfungen fehlgeschlagen.'}
if (!$TablesOnly -and !$PlotsOnly) {
& $compiler /nologo /target:exe /main:WritingChecks ('/out:' + (Join-Path $output 'WritingChecks.exe')) $references $themeSource (Join-Path $PSScriptRoot 'src\Common.cs') (Join-Path $PSScriptRoot 'src\Writing.cs') (Join-Path $PSScriptRoot 'src\Grammar.cs') (Join-Path $PSScriptRoot 'src\Synonyms.cs') (Join-Path $PSScriptRoot 'tests\WritingChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Schreibtests konnten nicht gebaut werden.' }
if ($Integration) { & (Join-Path $output 'WritingChecks.exe') --integration } else { & (Join-Path $output 'WritingChecks.exe') }
if ($LASTEXITCODE -ne 0) { throw 'Schreibtests fehlgeschlagen.' }
}
}
if (!$ViewsOnly -and !$TablesOnly -and !$PlotsOnly) {
$documentSources=@('src/Common.cs','src/Writing.cs','src/DocumentEditor.cs','src/References.cs','src/ReferencePanel.cs','src/ReferenceStore.cs','src/ReferenceLookup.cs','src/ReferencePickerWindow.cs','tests/DocumentChecks.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:exe /main:DocumentChecks ('/out:' + (Join-Path $output 'DocumentChecks.exe')) $references $themeSource $documentSources
if ($LASTEXITCODE -ne 0) { throw 'Dokumenttests konnten nicht gebaut werden.' }
& (Join-Path $output 'DocumentChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Dokumenttests fehlgeschlagen.' }
$lookupSources=@('src/Common.cs','src/References.cs','src/ReferenceStore.cs','src/ReferenceLookup.cs','tests/ReferenceLookupChecks.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler /nologo /target:exe /main:ReferenceLookupChecks ('/out:'+(Join-Path $output 'ReferenceLookupChecks.exe')) $references $themeSource $lookupSources
if ($LASTEXITCODE -ne 0) {throw 'Linktests konnten nicht gebaut werden.'}
if ($OnlineReferences) {& (Join-Path $output 'ReferenceLookupChecks.exe') --online} else {& (Join-Path $output 'ReferenceLookupChecks.exe')}
if ($LASTEXITCODE -ne 0) {throw 'Link- und Bibliothekstests fehlgeschlagen.'}
}
if (!$TablesOnly -and !$PlotsOnly) {
$viewSources=@('src/Common.cs','src/Formats.cs','src/DocumentEditor.cs','src/DocumentFiles.cs','src/References.cs','src/ReferenceStore.cs','tests/ViewFilesChecks.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler /nologo /target:exe /main:ViewFilesChecks ('/out:'+(Join-Path $output 'ViewFilesChecks.exe')) $references $themeSource $viewSources
if ($LASTEXITCODE -ne 0) {throw 'Ansichts- und Importtests konnten nicht gebaut werden.'}
& (Join-Path $output 'ViewFilesChecks.exe')
if ($LASTEXITCODE -ne 0) {throw 'Ansichts- und Importtests fehlgeschlagen.'}
}
