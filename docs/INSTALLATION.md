# Schreib und Tabelle installieren

## Einfach herunterladen

[Setup herunterladen](https://github.com/Dom-Antoni/Byby_Word_and_Excel/releases/latest/download/Schreib-und-Tabelle-Setup.exe) oder die [Downloadseite mit allen Varianten](https://github.com/Dom-Antoni/Byby_Word_and_Excel/releases/latest) öffnen. Kein GitHub-Konto nötig. „Code → Download ZIP“ enthält den Quellcode und ist nicht der App-Download.

1. `Schreib-und-Tabelle-Setup.exe` starten.
2. Zielordner und gewünschte Optionen bestätigen. Standard ist `%LOCALAPPDATA%\Programs\Schreib-und-Tabelle`.
3. Der Assistent lädt das vollständige Paket einmalig von GitHub, kontrolliert die SHA-256-Prüfsumme und installiert es. Danach sind die Apps über das Startmenü suchbar. Beide Programme werden gemeinsam installiert und deinstalliert.

Der große [Offline-Installer](https://github.com/Dom-Antoni/Byby_Word_and_Excel/releases/latest/download/Schreib-und-Tabelle-Offline-Setup.exe) enthält alle Komponenten bereits. Er eignet sich zum Weitergeben oder für Computer ohne Internet. Die portable ZIP bleibt ebenfalls verfügbar. Nach der Installation sind weder Office, PowerShell 7, ein Entwicklerwerkzeug noch eine separate Java-Installation nötig. Voraussetzung ist Windows 10 ab Version 1809 oder Windows 11, 64 Bit, mit .NET Framework 4.8. Auf verwalteten Arbeitsrechnern können die dort geltenden Installationsregeln gelten.

Die eigenen Apps und Setup-Dateien sind noch nicht digital signiert. Windows kann deshalb einen unbekannten Herausgeber melden. Die Prüfsumme des Pakets schützt den Download vor Beschädigung und Vertauschung, ersetzt aber keine Herausgebersignatur.

## Windows-Integration

- Startmenü: **Schreib**, **Tabelle**, **Schreib und Tabelle deinstallieren**.
- Optional: blaue Schreib- und grüne Tabelle-Symbole auf dem Desktop.
- Rechtsklick auf eine freie Stelle im Explorer oder auf dem Desktop → **Neu → Schreib-Dokument** beziehungsweise **Neu → Tabelle**. Falls Windows das Menü noch zwischenspeichert, das Explorer-Fenster schließen und erneut öffnen; gegebenenfalls einmal ab- und anmelden.
- Neue Dateien enden auf `.schreib` oder `.tabelle`, können zunächst umbenannt und anschließend doppelt angeklickt werden. `.tabelle` ist ein einzelnes Blatt auf CSV-Basis (Semikolon, UTF-8), keine Excel-Arbeitsmappe; Zelltexte und einfache Formeln werden gespeichert, Plot-Einstellungen und Formatierungen nicht.
- „Öffnen mit“ und Windows → Einstellungen → Apps → Standard-Apps bieten die Programme für unterstützte Dateitypen an. Vorhandene Standards werden nicht automatisch umgestellt. Windows entscheidet anhand deiner Auswahl, welche App per Doppelklick öffnet.

Die Installation und Registrierung gelten nur für das eigene Benutzerkonto. Administratorrechte werden nicht angefordert. Bereits geöffnete Schreib-/Tabelle-Fenster vor einer Aktualisierung speichern und schließen.

## Aktualisieren und deinstallieren

Ein neuer Setup-Assistent verwendet denselben Installationsordner und aktualisiert die Programme. Es gibt keinen Hintergrund-Updater. Eigene Texte, Tabellen, Wörterbücher, Quellen und Einstellungen werden bei einer Aktualisierung nicht gelöscht.

Zum Entfernen: Windows → Einstellungen → Apps → Installierte Apps → **Schreib und Tabelle** → Deinstallieren. Alternativ den Deinstallationseintrag im Startmenü öffnen. App-Dateien, eigene Verknüpfungen und Registrierungen werden entfernt. Persönliche Einstellungen unter `%LOCALAPPDATA%\Byby_Word_and_Excel` und eigene Dokumente bleiben erhalten. Auch zusätzlich im Installationsordner gespeicherte, nicht mitgelieferte Dateien werden nicht pauschal gelöscht.

## Installer entwickeln

`setup-installer.ps1` lädt den offiziellen Inno-Setup-Compiler 6.7.3 mit festgelegter SHA-256-Prüfsumme und entpackt ihn im offiziellen portablen Modus unter `vendor/`. Auf dem Entwicklungscomputer wird dadurch keine Compiler-App registriert.

Nach `build.ps1` und `package.ps1` erzeugt `build-installer.ps1` beide Assistenten unter `dist/installers/`. Die kleine Setup-Datei enthält eine feste Release-Adresse und Paketprüfsumme. Beide Varianten nutzen denselben Installations- und Deinstallationsmechanismus.

`test-installer.ps1` läuft ausschließlich auf dem kurzlebigen GitHub-Actions-Rechner: Offline-Installation, Startmenü, Desktop, „Neu“, Dateistandards, Öffnen neuer Dateien, Deinstallation und Erhalt eigener Daten. Ein lokaler Testserver prüft außerdem einen erfolgreichen echten Download und die Ablehnung eines manipulierten Pakets.

Für ein neues öffentliches Update `VERSION` erhöhen. Der Workflow veröffentlicht erst nach erfolgreichen App- und Installerprüfungen ein Release samt Setups, portabler ZIP und SHA256SUMS.txt. Bestehende Versionsdownloads werden nicht überschrieben; dadurch bleiben Adresse und Prüfsumme in älteren Setup-Assistenten zusammenpassend.

Technische Grundlage: [Inno-Setup-Dateidownload und Archivinstallation](https://jrsoftware.org/ishelp/topic_filessection.htm), [Windows-Explorer-Neu-Menü](https://learn.microsoft.com/en-us/windows/win32/shell/context#extending-the-new-submenu).
