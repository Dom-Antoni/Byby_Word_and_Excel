# Byby_Word_and_Excel

Zwei kleine, getrennte Windows-Desktop-Apps, um alltägliches Schreiben und einfache Tabellenarbeit ohne Microsoft Office und ohne Abonnement zu erledigen.

**Stand: erste Grundversion, noch kein vollständiger Office-Ersatz.** Die höchste Priorität ist ein zuverlässiger Umgang mit eigenen Texten und Tabellen. Kein Konto, keine Cloud, keine Telemetrie und keine Netzwerkanfragen im Anwendungscode. GitHub wird nur für Entwicklung und Downloads verwendet.

## Starten

Unter Windows `build.ps1` in PowerShell ausführen. Anschließend `dist/Schreib.exe` oder `dist/Tabelle.exe` doppelklicken. Benötigt wird das Windows .NET Framework mit WPF; kein Office, kein .NET SDK und keine zusätzlichen NuGet-Pakete. Die Programme werden mit dem vorhandenen Framework-Compiler gebaut. Auf diesem Entwicklungsrechner erfolgreich gebaut; die Oberflächenprüfung steht noch aus.

Die EXE-Dateien sind portabel. Derzeit sind sie unsigniert. Es gibt noch keinen veröffentlichten Download und keinen Installer.

## Schreib

- Texte schreiben und lesen, TXT und Markdown in UTF-8 speichern.
- DOCX als reinen Text importieren. Die Originaldatei wird dabei nicht überschrieben. Bilder, Layout, Kommentare, Kopf-/Fußzeilen und komplexe Inhalte werden nicht übernommen.
- Lokale WPF-Rechtschreibprüfung mit Deutsch, britischem oder amerikanischem Englisch; Korrekturvorschläge über das Kontextmenü. Wörterbuchverfügbarkeit und Qualität müssen auf dem Zielrechner praktisch geprüft werden.
- Hinweise auf gleiche Wörter innerhalb von sieben Wortpositionen; ein Klick markiert die betreffende Stelle. Auch häufige Funktionswörter werden derzeit angezeigt.
- Wörter- und Zeichenzähler, Strg+S, Speicherrückfrage beim Schließen oder Wechseln.

Die Rechtschreibprüfung ist noch kein umfassender Grammatik- oder Stilassistent. Die Wiederholungshinweise sind eine lokale Regel, kein KI-Modell. Als nächster Ausbau ist eine vollständig lokal gebündelte Grammatikprüfung zu evaluieren. Cloud-Dienste sind dafür nicht vorgesehen.

## Tabelle

- CSV/TSV öffnen, bearbeiten und speichern. Vor CSV-Import das passende Trennzeichen wählen; Semikolon ist voreingestellt. UTF-8 ist das vorgesehene Textformat.
- XLSX-Blätter als Zellwerte importieren und auswählen. Gespeicherte Formelergebnisse werden gelesen; fehlende Ergebnisse werden mit `#KEIN_CACHE!` markiert. Keine Neuberechnung beliebiger Excel-Formeln, kein XLSX-Rückexport. Datum/Zeit und Formatierungen werden noch nicht interpretiert, Datumswerte können als Seriennummer erscheinen.
- XLSX-Import verändert die Originaldatei nicht. Speichern exportiert das aktuelle Blatt als CSV oder TSV; andere Blätter und Excel-Funktionen werden nicht exportiert.
- Zellen bearbeiten, zusätzliche Zeilen und Spalten hinzufügen, zwischen Eingaben und berechneten Ergebnissen wechseln.
- Eigene Formeln: `=A1+B1*2`, `=(A1+3)/2`, `=SUMME(A1:A10)`, `=MITTELWERT(A1:A10)`, `=MIN(A1:A10)`, `=MAX(A1:A10)`. Die englischen Namen SUM und AVERAGE funktionieren ebenfalls. Funktionen akzeptieren in dieser Version genau eine Zelle oder einen rechteckigen Bereich.
- Dezimalpunkt und Dezimalkomma, Zellbezüge, Klammern, Fehlerhinweise bei Zirkelbezügen und Division durch null.
- CSV speichert die eingegebenen Formeln, nicht deren Ergebnisse. Leere Randzeilen und Randspalten werden beim Export entfernt. Zahlen mit Tausendertrennzeichen werden nicht unterstützt.

Nicht unterstützt: alte DOC/XLS-Binärdateien, Makros, Diagramme, komplexe Excel-Funktionen, Formatierungstreue und große Datenanalysen. Der XLSX-Import ist auf 10.000 Zeilen und 512 Spalten begrenzt; große Tabellen sind noch nicht auf Leistung getestet. Mehrzellen-Einfügen und ein eigener Arbeitsmappen-Speichertyp fehlen noch.

## Qualität und Entwicklung

`test.ps1` baut beide Programme und prüft CSV mit mehrzeiligen Feldern und Anführungszeichen, Formeln, Zirkelbezüge, Schreibfehlerbehandlung, atomaren Dateiersatz sowie DOCX-/XLSX-Import anhand kleiner Testdateien. Am 25.10.2026 haben alle 25 Prüfungen bestanden. Die Tests ersetzen keine praktische Prüfung der Oberflächen und Wörterbücher.

Quellcode: `src/Schreib.cs`, `src/Tabelle.cs`, `src/Formats.cs`, `src/Common.cs`. Beide Programme verwenden native WPF-Oberflächen. Der Code enthält keine Netzwerkclients und führt keine Makros oder Formeln als Programmcode aus.

Geplante Entwicklungsschritte stehen in [ROADMAP.md](ROADMAP.md). Technische Quellen: [WPF-Rechtschreibprüfung](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.spellcheck) und [LanguageTool lokal betreiben](https://dev.languagetool.org/http-server.html). Lokales LanguageTool enthält laut Dokumentation keine cloudbasierten KI-Regeln; die Integration ist noch nicht implementiert.

## GitHub und Downloads

Vorgesehen: öffentliches Repository `Dom-Antoni/Byby_Word_and_Excel`. Der Workflow baut und testet auf Windows. Ein Tag wie `v0.1.0` veröffentlicht beide EXE-Dateien und eine ZIP-Datei als Release. Vor einem Release müssen Oberflächen und Sprachprüfung praktisch getestet sein.

Lizenz: MIT, siehe [LICENSE](LICENSE).
