# Tabelle ansehen und importieren

Tabelle ist vor allem zum Lesen und Scrollen gedacht. Vorhandene Dateien öffnen in der **Leseansicht**. Zum Ändern unter **Bearbeiten → Bearbeitung aktivieren** umschalten. Neue Tabellen sind direkt bearbeitbar.

## Übersicht und Navigation

- **Datei:** neue Tabelle, Öffnen/Importieren, CSV-/TSV-Speicherung und Schließen.
- **Bearbeiten:** Bearbeitung einschalten, Auswahl kopieren, Suchen und einfache Erweiterung um Zeilen/Spalten.
- **Ansicht:** Spaltennamen, erste Spalte fixieren, Textumbruch, Spaltenbreiten, Zoom, Formelergebnisse sowie Hell/Dunkel/Windows-Darstellung.
- **Daten:** Import und automatische oder manuelle Wahl des Text-Trennzeichens.

Die Zellen haben größere Innenabstände und mindestens 38 Pixel Zeilenhöhe bei 100 % Zoom. Abwechselnde Zeilenfarben, sichtbare Gitterlinien und deutliche Zeilennummern erleichtern das Lesen. Spaltenbreiten werden anhand der ersten 200 Datenzeilen angepasst und lassen sich am rechten Rand des Spaltenkopfs ziehen.

Beim Öffnen einer Datei mit mindestens zwei Zeilen wird zunächst die **erste Zeile als Spaltennamen** angezeigt. Die Namen stehen mit den Spaltenbuchstaben im festen Kopf und bleiben beim vertikalen Scrollen sichtbar. Die ursprüngliche Zeilennummerierung bleibt erhalten: Die erste Datenzeile hat deshalb die Nummer 2. Über Ansicht kann man die erste Zeile wieder als normale Datenzeile anzeigen; es werden keine Daten entfernt. Bei Dateien ohne Kopfzeile diese Option ausschalten.

Die erste Spalte kann beim horizontalen Scrollen zusätzlich fixiert werden. Blattreiter unten wechseln zwischen den Blättern einer Arbeitsmappe. Das Adressfeld springt mit einer Eingabe wie **D25** und Enter zu einer Zelle. Die Formelzeile zeigt deren vollständigen Inhalt. **Strg+F** sucht Zellinhalte im aktuellen Blatt; Enter beziehungsweise „Weitersuchen“ springt zum nächsten Treffer.

## Öffnen und Formate

**Datei → Öffnen**, **Strg+O**, Drag-and-drop auf das Fenster oder Aufruf von Tabelle.exe mit einem Dateipfad als Argument.

| Format | Import |
| --- | --- |
| CSV, TSV, TAB, TXT | Komma, Semikolon oder Tabulator automatisch erkannt; Zitate, maskierte Anführungszeichen und mehrzeilige Felder werden berücksichtigt |
| XLSX, XLSM, XLTX, XLTM | Mehrere Blätter, Texte, Zahlen, Wahrheitswerte, gängige Datums-/Uhrzeitformate und gespeicherte Formelergebnisse; Makros werden nicht ausgeführt |
| ODS, FODS | OpenDocument-Blätter und gespeicherte Zellwerte, einschließlich wiederholter Zeilen/Spalten; leere Restbereiche werden nicht riesig aufgefüllt |
| Excel-XML 2003 | SpreadsheetML-Arbeitsmappen, einschließlich expliziter Zeilen-/Spaltenindizes |
| HTML, HTM | Einfache HTML-Tabellen als Blätter, einschließlich grundlegender verbundener Zellen; keine Skripte oder externen Inhalte |
| XLS mit XML-/HTML-Inhalt | Einige Programme verwenden diese Endung für die beiden zuvor genannten Textformate; ihr Inhalt wird erkannt |

**Alte binäre XLS-Dateien und XLSB werden nicht direkt unterstützt.** Dafür eine XLSX-, ODS- oder CSV-Version verwenden. Die App verwendet kein installiertes Excel.

Textimporte erkennen Unicode-BOMs, UTF-8 und gängiges UTF-16; ältere westliche Dateien werden bei Bedarf als Windows-1252 gelesen. Eine Excel-Zeile **sep=;** oder **sep=,** wird berücksichtigt. Die Fußzeile zeigt das gewählte Trennzeichen. Unter **Daten → Text-Trennzeichen** kann man eine mehrdeutige Datei neu einlesen. Bei gleichzeitig vorkommenden, gleichmäßig verteilten Trennzeichen bleibt eine automatische Wahl grundsätzlich mehrdeutig; die manuelle Wahl steht deshalb bereit.

## Werte, Berechnungen und Speichern

Der Import übernimmt Zellwerte und keine vollständige Excel-Gestaltung. Diagramme, Bilder, bedingte Formatierungen und komplexe Layouts werden nicht wiedergegeben. Datumsanzeige wird für gängige Excel-Formate vereinfacht; sonstige Zahlenformate wie Währung und Prozent werden nicht vollständig nachgebildet.

Excel-/ODS-Formeln werden **nicht neu berechnet**. Es erscheinen ihre in der Datei gespeicherten Ergebnisse. Fehlt ein Ergebnis, zeigt die Zelle **#KEIN_CACHE!**. Importierte Textwerte mit führendem **=** bleiben in der normalen Ansicht unverändert.

Für eigene einfache Eingaben sind die Grundrechenarten und SUMME/SUM, MITTELWERT/AVERAGE, MIN und MAX vorhanden. Unter **Ansicht → Ergebnisse statt Formeln anzeigen** werden diese ausgewertet. Das ist kein vollständiger Excel-Rechenkern.

**CSV-/TSV-Speicherung exportiert das aktuelle Blatt**, einschließlich einer gegebenenfalls als Spaltenkopf dargestellten ersten Zeile. XLSX/ODS-Originale werden dabei nicht überschrieben. Die Exportdatei enthält keine Formatierungen und keine weiteren Blätter. Beim Verlassen einer Arbeitsmappe mit mehreren geänderten Blättern bietet die Speicherabfrage für jedes geänderte Blatt einen separaten Export an.

Grenzen: maximal **10.000 Zeilen und 512 Spalten pro Blatt**, maximal **64 Blätter**; Text-/XML-Bestandteile bis 30 MB. Nicht unterstützte Formate oder größere Bereiche werden mit einer Fehlermeldung abgewiesen.
