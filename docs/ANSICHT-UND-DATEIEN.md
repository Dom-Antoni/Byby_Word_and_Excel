# Darstellung und Dateien öffnen

## Menü statt überfüllter Werkzeugleiste

- **Datei:** Neu, Öffnen, Speichern, Speichern unter, RTF-/EndNote-Export, Schließen.
- **Bearbeiten:** Rückgängig/Wiederholen, Ausschneiden, Kopieren, Einfügen, Alles auswählen.
- **Format:** Fett, Kursiv, Unterstreichen, Überschrift, Normaltext, Aufzählung und Ausrichtung.
- **Referenzen:** Quelle hinzufügen, Quellenbibliothek anzeigen und EndNote-Export.
- **Prüfen:** Sprachprüfung, Synonyme, Einstellungen, Sprache und Wortvorschläge.
- **Ansicht:** Hell/Dunkel/Windows-Einstellung, Dokumentfläche, Seitenleisten und Lesemodus.

Die kleine Schnellleiste enthält nur Speichern, Rückgängig/Wiederholen, Schrift, Schriftgröße sowie Fett/Kursiv. Die Sprachwahl steht unter **Prüfen → Sprache**.

## Dunkelmodus

Unter **Ansicht → Darstellung** stehen „Wie Windows“, „Hell“ und „Dunkel“ zur Auswahl. Die Wahl wird lokal unter `%LOCALAPPDATA%/Byby_Word_and_Excel/appearance.json` gespeichert. „Wie Windows“ folgt der Windows-Appdarstellung, auch wenn sie während der Nutzung wechselt.

**Dunkle Dokumentfläche im Dunkelmodus** lässt sich unabhängig abschalten. Das ist besonders bei importierten Dokumenten mit individuellen Schriftfarben nützlich. Standardtext folgt der gewählten Ansicht; bewusst gesetzte Schriftfarben bleiben erhalten. Die Darstellung wird nicht als dunkle Textfarbe in das gespeicherte Dokument eingebrannt und erzeugt keine Rückgängig-Aktion.

Die Seitenleiste kann ausgeblendet werden. Im **Lesemodus** bleibt der Text gegen Bearbeitung geschützt; zum Übernehmen von Sprachkorrekturen oder Zitaten den Lesemodus wieder ausschalten. Die App ändert keine Windows-Darstellungseinstellungen.

## Dateien öffnen

Über **Datei → Öffnen**, Strg+O, Ziehen einer Datei auf das Fenster oder Aufruf von `Schreib.exe "Pfad zur Datei"`. Damit kann Windows die App über „Öffnen mit“ verwenden; die Anwendung registriert keine Dateizuordnungen automatisch.

| Format | Verhalten |
| --- | --- |
| `.schreib` | Text, Formatierung, Zitate und die Quellen des Dokuments |
| `.rtf` | Formatierter Text; keine vollständige Schreib-Quellenbibliothek |
| `.txt`, `.md`, `.markdown`, `.log`, `.csv`, `.tsv`, `.tex`, `.json`, `.xml` | Lesbarer Quelltext, keine Ausführung oder Interpretation als Programm |
| `.html`, `.htm`, `.xhtml` | Grundlegende Überschriften, Absätze, Fett/Kursiv, Listen, Umbrüche und Bildbeschreibungen; keine Skripte, externen Bilder, CSS oder vollständige Browseransicht |
| `.docx`, `.docm`, `.dotx` | Word-Text mit grundlegender Formatierung, Überschriften, Listen, lesbar vereinfachten Tabellen sowie Fuß-/Endnoten am Ende; Makros werden nicht ausgeführt |
| `.odt`, `.ott` | OpenDocument-Text, Überschriften und Umbrüche mit vereinfachter Formatierung |

Bei TXT-Dateien werden Unicode-BOMs, gängige UTF-16-Dateien und UTF-8 erkannt; für ältere westliche Texte gibt es Windows-1252 als Rückfall. HTML berücksichtigt eine angegebene Zeichenkodierung. Unbekannte Dateien lassen sich über „Alle Dateien“ als Text versuchen; erkennbare Binärdaten werden abgewiesen.

Word-, HTML- und ODT-Dateien werden **importiert**. Die Originaldatei wird nicht überschrieben; beim Speichern wird ein neues Schreib-Dokument angeboten. Bilder erscheinen gegebenenfalls als Textmarkierungen. Textfelder, komplexe Tabellen, mathematische Formeln, Änderungsverfolgung und exaktes Seitenlayout sind nicht vollständig unterstützt. Die App ist deshalb kein formatgetreuer Word-Renderer.

Das alte binäre **`.doc`**-Format und **PDF** werden nicht direkt unterstützt. Dafür eine DOCX-/RTF- beziehungsweise Textversion verwenden. Diese Grenze wird beim Öffnen verständlich angezeigt.

Die KI-Anbindung ist weiterhin ein späterer Entwicklungsschritt und wurde in diesem Ausbau nicht begonnen.
