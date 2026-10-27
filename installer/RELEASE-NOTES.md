Schreib und Tabelle 0.3.0 für den Alltagstest unter Windows 10/11 (64 Bit).

Neu in Schreib:

- Sichtbare Seitenränder auf einem fortlaufenden Blatt mit A4-Breite. Unter **Layout → Seitenränder** links, rechts, oben und unten in cm/mm einstellen, mit direkter Vorschau und Abbrechen. **Layout → Ränder mit der Maus einstellen** schaltet Linealgriffe und Hilfslinien ein. Ränder werden im `.schreib`-Dokument gespeichert.
- Rechtsklick auf markierte Textstellen zeigt Erklärungen, direkte Ersatzvorschläge, Ignorieren und den Sprung zur Schreibprüfung. Kopieren, Einfügen und Quellen hinzufügen bleiben im selben Menü erreichbar.
- Stil- und Grammatikprüfung laufen im Hintergrund nach standardmäßig 1,4 Sekunden Schreibpause. Höchstens eine Prüfung gleichzeitig; veraltete Ergebnisse werden verworfen. Pause und Automatik sind einstellbar.
- Wortvorschläge durchsuchen einen vorbereiteten Index. Dokumenttext und Textpositionen werden wiederverwendet; Statistik und Literaturanzeige werden nicht bei jedem Tastendruck neu aufgebaut.
- **Prüfen → Prüfverhalten und Laufzeiten** zeigt lokale Messwerte für die eigene Nutzung, ohne Textprotokolle.

- **Schreib-und-Tabelle-Setup.exe**: Empfohlener kleiner Setup-Assistent. Lädt das vollständige Paket einmalig von diesem Release herunter, prüft SHA-256 und installiert beide Apps.
- **Schreib-und-Tabelle-Offline-Setup.exe**: Vollständiger Installer, der während der Installation keinen Internetzugang benötigt.
- **Byby_Word_and_Excel-Windows-x64-offline.zip**: Portable Alternative ohne Installation. Vollständig entpacken und den ganzen Ordner behalten.

Die Installation erfolgt für das eigene Windows-Benutzerkonto, normalerweise ohne Administratorrechte. Schreib und Tabelle stehen danach im Startmenü und lassen sich unter Windows → Einstellungen → Apps → Installierte Apps gemeinsam deinstallieren. Desktop-Symbole, „Öffnen mit“ und „Neu → Schreib-Dokument / Tabelle“ im Explorer sind auswählbar.

Bestehende Standardprogramme für TXT, DOCX, CSV und XLSX werden nicht ersetzt. Die Auswahl erfolgt bei Bedarf über Windows → Standard-Apps oder „Öffnen mit → Immer“. Dokumente und persönliche Einstellungen bleiben beim Deinstallieren erhalten.

`.tabelle` speichert ein einzelnes Tabellenblatt mit Zelltexten und einfachen Formeln auf CSV-Basis. Excel- und Word-Dateien werden weiterhin mit den in der Dokumentation beschriebenen Einschränkungen importiert. Nach der Installation funktionieren beide Apps samt Grammatikprüfung offline; nur der ausdrücklich gestartete Quellenabruf nutzt das Internet.

Die eigenen EXE-Dateien sind noch nicht digital signiert. Windows kann deshalb einen unbekannten Herausgeber anzeigen. Prüfsummen der Downloads stehen in SHA256SUMS.txt.
