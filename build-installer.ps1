param([string]$OutputDirectory='dist',[string]$CompilerPath='')
$ErrorActionPreference='Stop'
$version=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {throw 'VERSION muss eine numerische Version wie 0.2.0 enthalten.'}
if (!$CompilerPath) {$CompilerPath=Join-Path $PSScriptRoot 'vendor\inno-6.7.3\ISCC.exe'}
if (!(Test-Path -LiteralPath $CompilerPath)) {throw 'Zunächst setup-installer.ps1 ausführen.'}
$output=Join-Path $PSScriptRoot $OutputDirectory
$zip=Join-Path $output 'Byby_Word_and_Excel-Windows-x64-offline.zip'
if (!(Test-Path -LiteralPath $zip)) {throw 'Zunächst build.ps1 und package.ps1 ausführen.'}
$installers=Join-Path $output 'installers'
New-Item -ItemType Directory -Path $installers -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {$size=($archive.Entries | Measure-Object -Property Length -Sum).Sum} finally {$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$registry=[Collections.Generic.List[string]]::new()
function RegistryLine([string]$key,[string]$name,[string]$value,[string]$flags='uninsdeletevalue',[string]$tasks='fileassoc',[string]$check='') {
    $line='Root: HKCU; Subkey: "'+$key+'"; ValueType: string; ValueName: "'+$name+'"; ValueData: "'+$value+'"'
    if ($flags) {$line+='; Flags: '+$flags}
    if ($tasks) {$line+='; Tasks: '+$tasks}
    if ($check) {$line+='; Check: '+$check}
    $registry.Add($line)
}
$associations=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'installer\associations.json') -Raw | ConvertFrom-Json
foreach ($app in @('Schreib','Tabelle')) {
    $prog='Byby.'+$app+'.Document'
    $own=if ($app -eq 'Schreib') {'.schreib'} else {'.tabelle'}
    $label=if ($app -eq 'Schreib') {'Schreib-Dokument'} else {'Tabelle'}
    # Own types also support Explorer/New when optional foreign associations are off.
    $tasks='fileassoc or explorernew'
    RegistryLine ('Software\Classes\'+$prog) '' $label 'uninsdeletekey' $tasks
    RegistryLine ('Software\Classes\'+$prog+'\DefaultIcon') '' ('{app}\'+$app+'.exe,0') 'uninsdeletekey' $tasks
    RegistryLine ('Software\Classes\'+$prog+'\shell\open\command') '' ('""{app}\'+$app+'.exe"" ""%1""') 'uninsdeletekey' $tasks
    RegistryLine ('Software\Classes\'+$own) '' $prog 'createvalueifdoesntexist' $tasks
    RegistryLine ('Software\Classes\'+$own+'\OpenWithProgids') $prog '' 'uninsdeletevalue uninsdeletekeyifempty' $tasks
    $check="OwnTypeAvailable('$own', '$prog')"
    RegistryLine ('Software\Classes\'+$own+'\ShellNew') 'NullFile' '' 'uninsdeletekey' 'explorernew' $check
    RegistryLine ('Software\Classes\'+$own+'\ShellNew') 'IconPath' ('{app}\'+$app+'.exe,0') 'uninsdeletekey' 'explorernew' $check
    RegistryLine ('Software\Classes\Applications\'+$app+'.exe') 'FriendlyAppName' $app 'uninsdeletekey'
    RegistryLine ('Software\Classes\Applications\'+$app+'.exe\shell\open\command') '' ('""{app}\'+$app+'.exe"" ""%1""') 'uninsdeletekey'
    RegistryLine 'Software\RegisteredApplications' ('Byby.'+$app) ('Software\Byby_Word_and_Excel\Capabilities\'+$app)
    RegistryLine ('Software\Byby_Word_and_Excel\Capabilities\'+$app) 'ApplicationName' $app 'uninsdeletekey'
    RegistryLine ('Software\Byby_Word_and_Excel\Capabilities\'+$app) 'ApplicationDescription' ('Lokale '+$label+'-App ohne Office-Abonnement') 'uninsdeletekey'
    RegistryLine ('Software\Byby_Word_and_Excel\Capabilities\'+$app) 'ApplicationIcon' ('{app}\'+$app+'.exe,0') 'uninsdeletekey'
    foreach ($extension in $associations.$app) {
        if ($extension -notmatch '^\.[a-z0-9]+$') {throw 'Ungültige Dateiendung in associations.json.'}
        RegistryLine ('Software\Classes\'+$extension+'\OpenWithProgids') $prog '' 'uninsdeletevalue uninsdeletekeyifempty'
        RegistryLine ('Software\Classes\Applications\'+$app+'.exe\SupportedTypes') $extension '' 'uninsdeletekey'
        RegistryLine ('Software\Byby_Word_and_Excel\Capabilities\'+$app+'\FileAssociations') $extension $prog 'uninsdeletekey'
    }
}
$registryFile=Join-Path $installers 'associations.iss'
[IO.File]::WriteAllLines($registryFile,$registry,[Text.UTF8Encoding]::new($true))
$argsForCompiler=@('/Qp',('/DAppVersion='+$version),('/DSourceRoot='+$PSScriptRoot),('/DPayloadDir='+$output),('/DPayloadZip='+$zip),('/DPayloadHash='+$hash),('/DPayloadSize='+[long]$size),('/DPayloadURL=https://github.com/Dom-Antoni/Byby_Word_and_Excel/releases/download/v'+$version+'/Byby_Word_and_Excel-Windows-x64-offline.zip'),('/DInstallerOutput='+$installers),('/DGeneratedRegistry='+$registryFile))
$argsForCompiler+=('/DCompilerLicense='+(Join-Path (Split-Path $CompilerPath) 'license.txt'))
[IO.File]::WriteAllText((Join-Path $installers 'build-parameters.json'),(ConvertTo-Json -InputObject @($argsForCompiler)),[Text.UTF8Encoding]::new($false))
$script=Join-Path $PSScriptRoot 'installer\Apps.iss'
& $CompilerPath @argsForCompiler $script
if ($LASTEXITCODE -ne 0) {throw 'Offline-Installer konnte nicht gebaut werden.'}
& $CompilerPath @argsForCompiler /DWebSetup $script
if ($LASTEXITCODE -ne 0) {throw 'Download-Installer konnte nicht gebaut werden.'}
$assets=@((Join-Path $installers 'Schreib-und-Tabelle-Setup.exe'),(Join-Path $installers 'Schreib-und-Tabelle-Offline-Setup.exe'),$zip)
$checksums=$assets | ForEach-Object { ((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+[IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $installers 'SHA256SUMS.txt'),$checksums,[Text.UTF8Encoding]::new($false))
Write-Output "Setup-Assistenten fertig: $installers"
