# Byby_Word_and_Excel

Zwei kleine, getrennte Windows-Desktop-Apps, um alltägliches Schreiben und einfache Tabellenarbeit ohne Microsoft Office und ohne Abonnement zu erledigen.

**Stand: Schreibassistenz mit lokaler Grammatik- und Stilprüfung; noch kein vollständiger Office-Ersatz.** Der Schwerpunkt liegt auf Schreiben und Sprachkorrektur für Deutsch und Englisch. Tabellen bleiben auf Ansicht, Bearbeitung und einfache Rechnungen begrenzt. Kein Konto, keine Cloud und keine Telemetrie. Die Grammatikprüfung kommuniziert ausschließlich mit einem mitgelieferten Prozess auf `127.0.0.1`; kein Text wird ins Internet geschickt. GitHub wird für Entwicklung und Downloads verwendet.

## Starten

Für die Entwicklung unter Windows x64 zunächst `setup-offline.ps1`, danach `build.ps1` mit PowerShell 7 ausführen. Der erste Schritt lädt die Sprachkomponenten einmalig herunter. Anschließend `dist/Schreib.exe` oder `dist/Tabelle.exe` doppelklicken. Benötigt wird das Windows .NET Framework mit WPF; kein Office und kein .NET SDK. Eine portable Java-Laufzeit für LanguageTool wird im App-Ordner mitgeliefert, ohne Systeminstallation.

`package.ps1` erstellt `dist/Byby_Word_and_Excel-Windows-x64-offline.zip`. Zum Weitergeben immer dieses vollständige Paket verwenden, vollständig entpacken und `Schreib.exe` starten. Die Ordner `runtime`, `languagetool` und `synonyms` müssen neben der EXE bleiben. Nach dem Download läuft die App ohne Internet. Die Programme sind unsigniert; ein Installer und ein als stabil freigegebener Release fehlen noch.

## Schreib

Die Oberfläche ist in **Datei, Bearbeiten, Format, Referenzen, Prüfen und Ansicht** gegliedert. Unter **Ansicht → Darstellung** stehen Hell, Dunkel und „Wie Windows“ bereit; die Dokumentfläche lässt sich separat abdunkeln. Ein Lesemodus und eine ausblendbare Seitenleiste erleichtern das Anzeigen fremder Texte. [Darstellung und unterstützte Dateiformate](docs/ANSICHT-UND-DATEIEN.md).

Zusätzlich zu Schreib/RTF/TXT lassen sich HTML, DOCX/DOCM/DOTX und ODT/OTT mit vereinfachter Formatierung importieren. Die Originaldatei wird dabei nicht überschrieben. Altes binäres DOC und PDF werden nicht direkt unterstützt. Dateien können auch per Drag-and-drop oder über `Schreib.exe "Dateipfad"` geöffnet werden.

Neu: Word-ähnliche Dokumentfläche mit Formatierung, lokale Wortvorschläge (Pfeiltasten + Tab), anklickbare farbige Zitate und ein Literaturbereich. Referenzmanager werden über EndNote-XML, RIS und CSL-JSON angebunden. EndNotes dokumentierter RTF-/Format-Paper-Weg steht für dessen Zitierstile bereit; es gibt keine direkte ENL-Datenbanksynchronisation und kein eingebettetes Word-Add-in. Details: [Referenzen, Dokumente und Wortvorschläge](docs/REFERENZEN.md).

**Referenz per Link hinzufügen:** Im Dokument rechtsklicken → „Referenz hinzufügen“ → „Aus Link / DOI“ → „Angaben abrufen“. Gefundene Metadaten prüfen und „Speichern & zitieren“ wählen. Eine eigene lokale Quellenbibliothek speichert die bestätigten Einträge dokumentübergreifend. Optionaler Internetzugriff findet nur beim ausdrücklich gestarteten Linkabruf statt; die Grammatikprüfung und bereits gespeicherte Quellen funktionieren offline. Fehlende Angaben lassen sich manuell ergänzen, ohne KI oder Onlinekonto. RIS-Export erlaubt die Weitergabe an EndNote, Zotero und Mendeley.

Zum Weiterbearbeiten mit Formatierung und Quellen `.schreib` verwenden. RTF-Export erstellt eine separate formatierte Datei; TXT/Markdown bleiben reine Textformate. Die App bleibt eine schlanke Schreibanwendung ohne vollständiges Word-Seitenlayout.

- Texte schreiben und lesen, formatiert als `.schreib` oder RTF und unformatiert als TXT/Markdown speichern.
- DOCX mit Grundformatierung importieren. Die Originaldatei wird dabei nicht überschrieben. Bilder werden als Textmarkierungen und Tabellen vereinfacht dargestellt; exaktes Seitenlayout und komplexe Word-Funktionen werden nicht vollständig übernommen.
- Lokale Rechtschreib-, Grammatik- und Zeichensetzungsprüfung durch LanguageTool 6.6, mit Deutsch, britischem oder amerikanischem Englisch. „Prüfen“ startet eine Prüfung sofort; nach Textänderungen wird automatisch geprüft.
- Kategorisierte Hinweise mit Erklärung: Stelle auswählen, markierten Text prüfen, einen angebotenen Vorschlag übernehmen, Fundstelle ignorieren oder Regel abschalten. Ohne passende Ersatzformulierung bleibt der Hinweis eine Hilfestellung zum eigenen Überarbeiten.
- Wiederholungen mit einstellbarem Abstand; häufige Funktionswörter werden normalerweise ausgelassen, direkt doppelte Wörter bleiben sichtbar. Heuristische Wortstämme und einige unregelmäßige Formen erfassen mögliche Wortfamilien. Das ist keine vollständige morphologische Analyse.
- Füllwörter, umständliche Wendungen, mögliche Redundanzen, Passivformen und Nominalstil. Diese Hinweise sind kontextabhängige Stilvorschläge, keine verbindlichen Fehlerurteile.
- Hinweise auf lange Sätze und Absätze, mittlere Satzlänge und LIX als grobe Lesbarkeitsorientierung.
- Lokale Synonyme für Deutsch (OpenThesaurus) und Englisch (WordNet), nach Bedeutungsgruppen getrennt. Ein Wort markieren oder den Cursor hineinsetzen und „Synonyme“ wählen. Registerhinweise bleiben sichtbar; Bedeutung und Beugung müssen vor dem Übernehmen geprüft werden.
- Eigene Wörter, bevorzugte Schreibweisen wie `Email=>E-Mail`, typografische Anführungszeichen und mehrfache Leerzeichen. Wortlisten, Schwellen und deaktivierte Regeln sind direkt über „Einstellungen“ bearbeitbar.
- Wörter- und Zeichenzähler, Strg+S, Speicherrückfrage beim Schließen oder Wechseln. Einstellungen werden lokal unter `%LOCALAPPDATA%/Byby_Word_and_Excel/writing.json` gespeichert; Texte werden dort nicht abgelegt.

Die App verwendet regelbasierte lokale Sprachprüfung und Wörterbücher. Freie KI-Umformulierungen, Sprachmodelle und YAML-Stilpakete sind bewusst noch nicht integriert. Die cloudbasierten KI-Regeln von LanguageTool sind nicht enthalten. Satz- und Absatzanalyse, Passiverkennung und Wortstämme sind Näherungen; Synonyme werden nicht automatisch passend zum Kontext umgeschrieben. Lange Texte werden für LanguageTool an Absatz- oder Wortgrenzen in Abschnitte aufgeteilt, wodurch an Grenzen Kontext fehlen kann. Es gibt keine Garantie, dass jeder Fehler erkannt wird. Weitere Bedienhinweise stehen in [docs/SCHREIBEN.md](docs/SCHREIBEN.md).

## Tabelle

**Plot erstellen:** Automatische lokale Vorschläge für Säulen/Balken, Linien, Punktdiagramme, Histogramme und Boxplots. Die Vorschau steht rechts neben der Tabelle, lässt sich vergrößern und als PNG, JPG oder SVG exportieren. Spaltenköpfe unterstützen „Als X-/Y-Achse verwenden“ per Rechtsklick. Beschriftungen, zweite Datenreihe, Zusammenfassung und Zeilenauswahl sind anpassbar. Es wird noch kein Sprachmodell angebunden. [Plot-Funktionen und Grenzen](docs/PLOTS.md).

**Gruppieren / Farbe nach:** Eine Spalte wie Standort bestimmt die Farbe. Bei zwei Y-Reihen unterscheiden zusätzlich Linienart und Punktsymbol die Messungen. Spaltenköpfe zeigen X, Y1, Y2 und Gruppe mit eigenen Rollenfarben; der Rechtsklick hebt die aktuelle Spalte hervor. Die Gruppierung lässt sich auch direkt per Rechtsklick im Plot wählen.

- Leseansicht mit größeren Zellen, abwechselnden Zeilenfarben, festen Spaltennamen und deutlichen Zeilennummern. Menüs für Datei, Bearbeiten, Ansicht und Daten sowie Blattreiter, Suche, Zoom und Hell-/Dunkelmodus.
- CSV/TSV/TXT öffnen: Semikolon, Komma oder Tabulator werden automatisch erkannt. Zitate, mehrzeilige Felder, Excel-sep-Angaben und gängige Textkodierungen werden berücksichtigt; die Trennzeichenwahl lässt sich korrigieren.
- XLSX/XLSM/XLTX/XLTM, ODS/FODS, Excel-XML und einfache HTML-Tabellen als Zellwerte importieren. Mehrere Blätter und gängige Excel-Datumsformate werden unterstützt. Gespeicherte Formelergebnisse werden gelesen; fehlende Ergebnisse sind markiert. Keine Neuberechnung beliebiger Excel-Formeln.
- Der Import verändert Excel-/ODS-/HTML-Originale nicht. Speichern exportiert das aktuelle Blatt als CSV oder TSV; andere Blätter und Formatierungen werden nicht exportiert. [Bedienung und unterstützte Formate](docs/TABELLE.md).
- Zellen bearbeiten, zusätzliche Zeilen und Spalten hinzufügen, zwischen Eingaben und berechneten Ergebnissen wechseln.
- Eigene Formeln: `=A1+B1*2`, `=(A1+3)/2`, `=SUMME(A1:A10)`, `=MITTELWERT(A1:A10)`, `=MIN(A1:A10)`, `=MAX(A1:A10)`. Die englischen Namen SUM und AVERAGE funktionieren ebenfalls. Funktionen akzeptieren in dieser Version genau eine Zelle oder einen rechteckigen Bereich.
- Dezimalpunkt und Dezimalkomma, Zellbezüge, Klammern, Fehlerhinweise bei Zirkelbezügen und Division durch null.
- CSV speichert die eingegebenen Formeln, nicht deren Ergebnisse. Leere Randzeilen und Randspalten werden beim Export entfernt. Zahlen mit Tausendertrennzeichen werden nicht unterstützt.

Nicht unterstützt: alte DOC/XLS-Binärdateien, XLSB, Makros, Diagramme, komplexe Excel-Funktionen, Formatierungstreue und große Datenanalysen. Tabellenimporte sind auf 10.000 Zeilen und 512 Spalten pro Blatt begrenzt. Die Anzeige verwendet virtuelle Zeilen und Spalten; repräsentative eigene Alltagsdateien müssen weiterhin ausprobiert werden. Mehrzellen-Einfügen und ein eigener Arbeitsmappen-Speichertyp fehlen noch.

## Qualität und Entwicklung

`test.ps1` baut beide Programme und prüft CSV, Formeln, atomaren Dateiersatz, Dokumentimporte, Quellenverwaltung sowie die Schreibregeln und Parser. Auch Zeichensätze, Hell-/Dunkeldarstellung und der Erhalt von Formatierung und Rückgängig beim Darstellungswechsel werden geprüft. `test.ps1 -Integration` prüft zusätzlich echte deutsche und englische LanguageTool-Korrekturen, UTF-16-Positionen nach Emoji, Abbruch und die realen Synonymdaten. Am 25.10.2026 haben alle 228 Prüfungen bestanden: 25 Basis-, 49 Tabellen-, 43 Schreib-, 61 Dokument-, 29 Bibliotheks- und 21 Ansichtsprüfungen. Nach einem Zeitlimit beim ersten Grammatiktest war dessen Wiederholung erfolgreich. Die Plot-Funktion umfasst inzwischen 96 gezielte Prüfungen zu Datenaufbereitung, Diagrammen, Gruppierung, Legenden, farbigen Spaltenrollen und PNG-/JPG-/SVG-Export. Die Tests ersetzen keine umfassende Prüfung mit eigenen Alltagstexten.

Quellcode: `src/Schreib.cs`, `src/WritingPanel.cs`, `src/Writing.cs`, `src/Grammar.cs`, `src/Synonyms.cs`, `src/WritingSettingsWindow.cs`, `src/Tabelle.cs`, `src/Formats.cs`, `src/Common.cs`. Beide Programme verwenden native WPF-Oberflächen. Der Grammatikclient verwendet nur eine lokale Adresse, ohne Proxy oder Weiterleitungen. Es werden keine Makros oder Formeln als Programmcode ausgeführt. Grammatikprüfungen laufen asynchron; veraltete Ergebnisse werden verworfen. Beim normalen Beenden wird der zugehörige LanguageTool-Prozess gestoppt.

Geplante Entwicklungsschritte stehen in [ROADMAP.md](ROADMAP.md). Technische Grundlage: [LanguageTool lokal betreiben](https://dev.languagetool.org/http-server.html). Quellen und Lizenzen aller mitgelieferten Komponenten stehen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); der eigene MIT-Code ändert deren Lizenzen nicht.

## GitHub und Downloads

Öffentliches Repository: [Dom-Antoni/Byby_Word_and_Excel](https://github.com/Dom-Antoni/Byby_Word_and_Excel). Der Workflow lädt die Offline-Komponenten, baut und testet auf Windows und hinterlegt das vollständige Offline-Paket als Build-Artefakt. Ein Tag wie `v0.1.0` veröffentlicht die vollständige ZIP-Datei als Release. Einzelne EXE-Dateien werden nicht als vollständiger Schreibassistent angeboten.

Lizenz: MIT, siehe [LICENSE](LICENSE).
