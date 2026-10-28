param([string]$OutputDirectory='dist',[switch]$RIntegration)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework=Split-Path $compiler
$refs=@('System.dll','System.Core.dll','System.Data.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
$refs+=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object {'/reference:'+(Join-Path $framework ('WPF/'+$_))}
$refs+=@('PdfSharp-wpf.dll','MigraDoc.DocumentObjectModel-wpf.dll','MigraDoc.Rendering-wpf.dll') | ForEach-Object {'/reference:'+(Join-Path $PSScriptRoot ($OutputDirectory+'/'+$_))}
$sources=@('src/Common.cs','src/Theme.cs','src/Proofing.cs','src/DocumentLayout.cs','src/Formats.cs','src/SheetFiles.cs','src/Tabelle.cs','src/Writing.cs','src/DocumentEditor.cs','src/DocumentImages.cs','src/PdfExport.cs','src/References.cs','src/ReferenceStore.cs','src/PlotData.cs','src/PlotGrouping.cs','src/PlotDrawing.cs','src/PlotPanel.cs','src/RExport.cs','tests/ExportChecks.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$exe=Join-Path $PSScriptRoot ($OutputDirectory+'/ExportChecks.exe')
& $compiler /nologo /target:exe /main:ExportChecks ('/out:'+$exe) $refs $sources
if($LASTEXITCODE -ne 0){throw 'Exportprüfungen konnten nicht gebaut werden.'}
if($RIntegration){& $exe --r-integration}else{& $exe}
if($LASTEXITCODE -ne 0){throw 'Bild-, PDF- und R-Exportprüfungen fehlgeschlagen.'}
