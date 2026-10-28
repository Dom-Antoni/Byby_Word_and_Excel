Schreib und Tabelle 0.4.0 für Windows 10/11 (64 Bit).

Neu in Schreib:

- Mehrseitiger PDF-Export mit A4-Seitenrändern, auswählbarem Text und eingebetteten Bildern, vollständig offline ohne PDF-Drucker.
- Bilder mit kompakter oder voller Auflösung, Breite in cm sowie Über- oder Unterschrift einfügen. Bildpixel werden nicht sprachlich geprüft. Bilder bleiben in Schreib-Dateien und RTF erhalten.
- Bilder samt Beschriftung rückgängig machen und wiederherstellen.

Neu in Tabelle:

- Aktuelles Blatt direkt als RStudio-Projekt exportieren und öffnen; der Datensatz wird beim Projektstart geladen.
- Alle sechs Plotarten als bearbeitbares ggplot-Skript einschließlich Daten, vorberechneten Plotwerten, Gruppenfarben, PNG und SVG exportieren.
- Wenn R und ggplot2 installiert sind, entstehen direkt echte plot.rds, Datensatz-RDS und eine gemeinsame analyse.RData. Keine automatische Paketinstallation.

Bedienung und Grenzen: docs/PDF-BILDER-UND-R.md.

- **Schreib-und-Tabelle-Setup.exe**: Empfohlener kleiner Setup-Assistent. Lädt das vollständige Paket einmalig von diesem Release herunter, prüft SHA-256 und installiert beide Apps.
- **Schreib-und-Tabelle-Offline-Setup.exe**: Vollständiger Installer, der während der Installation keinen Internetzugang benötigt.
- **Byby_Word_and_Excel-Windows-x64-offline.zip**: Portable Alternative ohne Installation. Vollständig entpacken und den ganzen Ordner behalten.

Die Installation erfolgt für das eigene Windows-Benutzerkonto, normalerweise ohne Administratorrechte. Schreib und Tabelle stehen danach im Startmenü und lassen sich unter Windows → Einstellungen → Apps → Installierte Apps gemeinsam deinstallieren. Desktop-Symbole, „Öffnen mit“ und „Neu → Schreib-Dokument / Tabelle“ im Explorer sind auswählbar.

Bestehende Standardprogramme für TXT, DOCX, CSV und XLSX werden nicht ersetzt. Die Auswahl erfolgt bei Bedarf über Windows → Standard-Apps oder „Öffnen mit → Immer“. Dokumente und persönliche Einstellungen bleiben beim Deinstallieren erhalten.

`.tabelle` speichert ein einzelnes Tabellenblatt mit Zelltexten und einfachen Formeln auf CSV-Basis. Excel- und Word-Dateien werden weiterhin mit den in der Dokumentation beschriebenen Einschränkungen importiert. Nach der Installation funktionieren beide Apps samt Grammatikprüfung offline; nur der ausdrücklich gestartete Quellenabruf nutzt das Internet.

Die eigenen EXE-Dateien sind noch nicht digital signiert. Windows kann deshalb einen unbekannten Herausgeber anzeigen. Prüfsummen der Downloads stehen in SHA256SUMS.txt.
