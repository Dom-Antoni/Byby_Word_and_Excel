param([string]$OutputDirectory='dist')
$ErrorActionPreference='Stop'
# This test changes a disposable user's registry and shortcuts. Never run it on a real desktop.
if ($env:GITHUB_ACTIONS -ne 'true') {throw 'Installer-Systemtests laufen ausschließlich auf dem kurzlebigen GitHub-Actions-Rechner.'}
$output=Join-Path $PSScriptRoot $OutputDirectory
$installers=Join-Path $output 'installers'
$testRoot=Join-Path $output 'installer-smoke'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$count=0
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class InstallerWindowProbe {
  delegate bool WindowCallback(IntPtr window,IntPtr value);
  [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr value);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int maximum);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr a,IntPtr b);
  public static bool Find(int process,string title,bool close) {
    bool found=false;
    EnumWindows(delegate(IntPtr window,IntPtr value) {
      uint owner;GetWindowThreadProcessId(window,out owner);
      if(owner==(uint)process) {var text=new StringBuilder(1024);GetWindowText(window,text,text.Capacity);
        if(text.ToString().Contains(title)) {found=true;if(close)PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);return false;}}
      return true;
    },IntPtr.Zero);
    return found;
  }
}
'@
function Assert-Setup([bool]$value,[string]$message) {if (!$value) {throw $message};$script:count++}
function Run-Setup([string]$exe,[string]$folder,[string]$label,[bool]$failure=$false) {
    $argsForSetup=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$folder+'"'),'/TASKS="desktopicon,fileassoc,explorernew"',('/LOG="'+(Join-Path $testRoot ($label+'.log'))+'"'))
    $process=Start-Process -FilePath $exe -ArgumentList $argsForSetup -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(180000)) {$process.Kill();throw "Installer-Zeitlimit: $label"}
    if ($failure) {Assert-Setup ($process.ExitCode -ne 0) 'Manipuliertes Downloadpaket wird abgewiesen'} else {Assert-Setup ($process.ExitCode -eq 0) "Installation erfolgreich: $label ($($process.ExitCode))"}
}
function Check-Installed([string]$folder) {
    foreach ($file in @('Schreib.exe','Tabelle.exe','runtime/bin/java.exe','languagetool/languagetool-server.jar','synonyms/openthesaurus.txt','unins000.exe')) {Assert-Setup (Test-Path -LiteralPath (Join-Path $folder $file)) "Installiert: $file"}
    foreach ($app in @('Schreib','Tabelle')) {
        $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) ('Schreib und Tabelle\'+$app+'.lnk')
        Assert-Setup (Test-Path -LiteralPath $shortcut) "Im Startmenü: $app"
        $link=(New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
        Assert-Setup ($link.TargetPath -eq (Join-Path $folder ($app+'.exe'))) "Startmenü-Ziel korrekt: $app"
        Assert-Setup (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($app+'.lnk'))) "Desktop-Symbol: $app"
    }
    $uninstall='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C42AB24B-88E4-4B43-9508-C6E177D1F501}_is1'
    Assert-Setup ((Get-ItemProperty -LiteralPath $uninstall).DisplayName -eq 'Schreib und Tabelle') 'Windows-Deinstallationsliste enthält die Apps'
    foreach ($extension in @('.schreib','.tabelle')) {
        $shell=Get-Item -LiteralPath ('HKCU:\Software\Classes\'+$extension+'\ShellNew')
        Assert-Setup ($shell.GetValueNames() -contains 'NullFile') "Explorer/Neu: $extension"
    }
    foreach ($pair in @(@('Schreib','.schreib'),@('Tabelle','.tabelle'))) {
        $file=Join-Path $testRoot ('Neu mit Leerzeichen'+$pair[1]);[IO.File]::WriteAllText($file,'')
        $process=Start-Process -FilePath (Join-Path $folder ($pair[0]+'.exe')) -ArgumentList ('"'+$file+'"') -WindowStyle Hidden -PassThru
        try {
            $until=[DateTime]::UtcNow.AddSeconds(20)
            do {Start-Sleep -Milliseconds 200;$process.Refresh();$opened=[InstallerWindowProbe]::Find($process.Id,[IO.Path]::GetFileName($file),$false)} until ($process.HasExited -or $opened -or [DateTime]::UtcNow -ge $until)
            Assert-Setup (!$process.HasExited -and $opened) "Neue $($pair[1])-Datei wird ohne Fehlermeldung geöffnet"
            $null=[InstallerWindowProbe]::Find($process.Id,[IO.Path]::GetFileName($file),$true);Assert-Setup ($process.WaitForExit(10000)) 'Leeres Dokument schließt ohne Speicherrückfrage'
        } finally {if (!$process.HasExited) {$process.Kill()}}
    }
}
function Remove-Setup([string]$folder) {
    $sentinel=Join-Path $folder 'Mein-Dokument.txt';[IO.File]::WriteAllText($sentinel,'behalten')
    $process=Start-Process -FilePath (Join-Path $folder 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -Wait -PassThru
    Assert-Setup ($process.ExitCode -eq 0) 'Deinstallation erfolgreich'
    Assert-Setup (!(Test-Path -LiteralPath (Join-Path $folder 'Schreib.exe'))) 'Schreib entfernt'
    Assert-Setup (!(Test-Path -LiteralPath (Join-Path $folder 'Tabelle.exe'))) 'Tabelle entfernt'
    Assert-Setup ([IO.File]::ReadAllText($sentinel) -eq 'behalten') 'Eigene Dokumente bleiben erhalten'
    Assert-Setup (!(Test-Path -LiteralPath 'HKCU:\Software\Classes\Byby.Schreib.Document')) 'Eigene Dateitypregistrierung entfernt'
    Assert-Setup (!(Test-Path -LiteralPath 'HKCU:\Software\Classes\Byby.Tabelle.Document')) 'Eigener Tabellentyp entfernt'
    Assert-Setup (!(Test-Path -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{C42AB24B-88E4-4B43-9508-C6E177D1F501}_is1')) 'Eintrag aus Installierte Apps entfernt'
    foreach ($app in @('Schreib','Tabelle')) {Assert-Setup (!(Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) ('Schreib und Tabelle\'+$app+'.lnk')))) 'Startmenüeintrag entfernt'}
}
$null=New-Item -Path 'HKCU:\Software\Classes\.txt' -Force
Set-Item -LiteralPath 'HKCU:\Software\Classes\.txt' -Value 'Existing.Text.Editor'
$offline=Join-Path $testRoot 'Offline Apps'
Run-Setup (Join-Path $installers 'Schreib-und-Tabelle-Offline-Setup.exe') $offline 'offline'
Check-Installed $offline
Assert-Setup ((Get-Item 'HKCU:\Software\Classes\.txt').GetValue('') -eq 'Existing.Text.Editor') 'Vorhandene Standardzuordnung nicht überschrieben'
# A later choice made by the user must also survive our uninstall.
Set-Item -LiteralPath 'HKCU:\Software\Classes\.schreib' -Value 'Other.Writer'
Remove-Setup $offline
Assert-Setup ((Get-Item 'HKCU:\Software\Classes\.schreib').GetValue('') -eq 'Other.Writer') 'Spätere Dateizuordnung bleibt nach Deinstallation erhalten'
Assert-Setup ((Get-Item 'HKCU:\Software\Classes\.txt').GetValue('') -eq 'Existing.Text.Editor') 'Fremde Textzuordnung bleibt nach Deinstallation erhalten'
Remove-ItemProperty -LiteralPath 'HKCU:\Software\Classes\.schreib' -Name '(default)'

# Exercise real downloading, integrity rejection, extraction and uninstall over loopback.
$compiler=Join-Path $PSScriptRoot 'vendor\inno-6.7.3\ISCC.exe'
$frameworkCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$serverExe=Join-Path $testRoot 'DownloadServer.exe'
& $frameworkCompiler /nologo /target:exe ('/out:'+$serverExe) (Join-Path $PSScriptRoot 'tests\InstallerDownloadServer.cs')
if ($LASTEXITCODE -ne 0) {throw 'Download-Testserver konnte nicht gebaut werden.'}
$portFile=Join-Path $testRoot 'port.txt'
$server=Start-Process -FilePath $serverExe -ArgumentList @(('"'+(Join-Path $output 'Byby_Word_and_Excel-Windows-x64-offline.zip')+'"'),('"'+$portFile+'"')) -WindowStyle Hidden -PassThru
try {
    for ($attempt=0;$attempt -lt 100 -and !(Test-Path -LiteralPath $portFile);$attempt++) {Start-Sleep -Milliseconds 100}
    $port=[int](Get-Content -LiteralPath $portFile -Raw)
    $parameters=Get-Content -LiteralPath (Join-Path $installers 'build-parameters.json') -Raw | ConvertFrom-Json
    foreach ($kind in @('bad','good')) {
        $argsForCompiler=@($parameters | Where-Object {$_ -notlike '/DPayloadURL=*'})+@('/DWebSetup','/DDownloadTest',('/DPayloadURL=http://127.0.0.1:'+$port+'/'+$kind+'.zip'))
        & $compiler @argsForCompiler (Join-Path $PSScriptRoot 'installer\Apps.iss')
        if ($LASTEXITCODE -ne 0) {throw 'Download-Testinstaller konnte nicht gebaut werden.'}
        $webFolder=Join-Path $testRoot ('Download '+$kind)
        Run-Setup (Join-Path $installers 'Download-Test-Setup.exe') $webFolder $kind ($kind -eq 'bad')
        if ($kind -eq 'bad') {Assert-Setup (!(Test-Path -LiteralPath (Join-Path $webFolder 'Schreib.exe'))) 'Fehlerhafter Download installiert keine App'}
        else {Check-Installed $webFolder;Remove-Setup $webFolder}
    }
} finally {if (!$server.HasExited) {$server.Kill()}}
Write-Output "$count Installerprüfungen bestanden."
