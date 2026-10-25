$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Data.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Xml.Linq.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $wpf $_) }
foreach ($app in @('Schreib', 'Tabelle')) {
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ ('/out:' + (Join-Path $output ($app + '.exe'))) $references (Join-Path $PSScriptRoot 'src\Common.cs') (Join-Path $PSScriptRoot 'src\Formats.cs') (Join-Path $PSScriptRoot ('src\' + $app + '.cs'))
    if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen: $app" }
}
Write-Output "Fertig: $output"

