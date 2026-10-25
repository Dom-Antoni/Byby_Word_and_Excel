$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$references = @('System.dll','System.Core.dll','System.Data.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
& $compiler /nologo /target:exe /main:Checks ('/out:' + (Join-Path $PSScriptRoot 'dist\Checks.exe')) $references (Join-Path $PSScriptRoot 'src\Common.cs') (Join-Path $PSScriptRoot 'src\Formats.cs') (Join-Path $PSScriptRoot 'src\Tabelle.cs') (Join-Path $PSScriptRoot 'tests\Checks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tests konnten nicht gebaut werden.' }
& (Join-Path $PSScriptRoot 'dist\Checks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
