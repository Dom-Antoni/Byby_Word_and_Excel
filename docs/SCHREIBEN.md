# Schreibassistenz verwenden

1. Das vollständige Offline-Paket entpacken. `Schreib.exe`, `runtime`, `languagetool` und `synonyms` gehören zusammen.
   Formatierung, Referenzmanager und Tab-Vervollständigung sind in [REFERENZEN.md](REFERENZEN.md) beschrieben. Für Dokumente mit Quellen das Format `.schreib` verwenden.
2. Oben Deutsch, English (UK) oder English (US) wählen. Die Sprache gilt für den gesamten Text; gemischtsprachige Absätze werden noch nicht separat erkannt.
3. Schreiben oder vorhandene Texte öffnen. TXT/Markdown, HTML, DOCX und ODT werden unterstützt; bei HTML und Office-Dateien wird die Formatierung vereinfacht. Details stehen unter [Darstellung und Dateien](ANSICHT-UND-DATEIEN.md).
4. Nach standardmäßig 1,4 Sekunden Schreibpause erscheinen Stilhinweise und die lokale Grammatikprüfung startet. Stilregeln und Grammatik laufen im Hintergrund. Der erste Start der Java-Prüfung dauert länger. Unten im Hinweisbereich steht, ob wirklich Grammatik geprüft wurde oder nur die eigenen Stilregeln verfügbar sind.
5. Einen Hinweis anklicken. Die zugehörige Textstelle wird markiert; darunter stehen Erklärung und angebotene Alternativen. Eine Alternative auswählen und „Übernehmen“ anklicken. Normales Rückgängig im Textfeld bleibt verfügbar.

## Hinweise steuern

Rechtsklick auf eine rote, blaue oder gelbe Markierung zeigt die Erklärung und angebotene Ersatzformulierungen direkt an der Fundstelle. Ein Vorschlag ersetzt ausschließlich die geprüfte Stelle. Mehrere Hinweise stehen in getrennten Untermenüs. „Diesen Hinweis ignorieren“ entspricht der Seitenleiste; „In der Schreibprüfung anzeigen“ öffnet die ausführliche Ansicht. Im Lesemodus sind Änderungen gesperrt. Veraltete Vorschläge werden nicht angewendet. Ungeprüfte Stellen starten eine Hintergrundprüfung, deren Ergebnisse das noch geöffnete Menü aktualisieren. Ausschneiden, Kopieren, Einfügen und Referenzen bleiben erhalten.

- **Ignorieren** blendet genau diese Fundstelle auch bei automatischen und manuellen Folgeprüfungen aus. Einfügungen oder Löschungen davor verschieben die gespeicherte Position mit. Wird die Fundstelle selbst geändert, wird sie wieder geprüft. Andere Vorkommen derselben Regel bleiben sichtbar. Im `.schreib`-Format werden ignorierte Fundstellen für dieses Dokument und die gewählte Sprache mitgespeichert; TXT und RTF speichern diese Entscheidungen nicht.
- **Regel aus** deaktiviert genau diese Regel dauerhaft. Unter Einstellungen lässt sie sich wieder aktivieren.
- **Eigenes Wort** nimmt ein markiertes Wort oder das Wort eines Hinweises in das lokale Wörterbuch auf. Es wird bei Rechtschreib- und Wiederholungshinweisen berücksichtigt; Grammatik bleibt prüfbar.
- **Einstellungen** enthält Wortabstand, Satz-/Absatzlänge, Stilregeln, eigene Wörter und bevorzugte Schreibweisen. Beispiel: `Email=>E-Mail`. Jede Zuordnung steht auf einer eigenen Zeile; sie wird für den aktuellen Text unabhängig von der gewählten Sprache angewendet.
- Über den Kategorienfilter lassen sich Rechtschreibung, Grammatik, Wiederholungen, Stil, Lesbarkeit oder Einheitlichkeit getrennt ansehen.
- Alle aktiven Hinweise erscheinen direkt im Text: **rote Wellenlinie** für Rechtschreibung, **blaue Linie** für Grammatik, **gelbe Punktlinie** für Stil, Wiederholung, Lesbarkeit und Einheitlichkeit. Der Kategorienfilter begrenzt die Seitenleiste; die Textmarkierungen zeigen weiterhin alle aktiven Hinweise. Die Linien sind reine Anzeige, verändern keine Formatierung und landen nicht im Export.
- Enter trennt Absätze für die Absatzlängenprüfung. Jeder Absatz wird einzeln gezählt; die Statusleiste zeigt zusätzlich die Anzahl der nicht leeren Textabsätze.

## Seitenränder und Blatt

Unter **Layout → Seitenränder…** lassen sich links, rechts, oben und unten jeweils 0–5 cm beziehungsweise 0–50 mm einstellen. Dezimalkomma und Dezimalpunkt funktionieren, auch Eingaben mit Einheit wie `2,5 cm` oder `18 mm`. Die Vorschau verschiebt den Text sofort. „Abbrechen“ stellt die vorherigen Werte wieder her. Normal, Schmal und Breit sind Voreinstellungen.

**Layout → Ränder mit der Maus einstellen** zeigt Griffe am horizontalen und vertikalen Lineal. Ziehen verändert den jeweiligen Rand direkt; Hilfslinien markieren die Textfläche. Für den unteren Rand zum Ende des Blatts scrollen. Die cm-Lineale haben mm-Unterteilungen.

Die Schreibfläche ist ein fortlaufendes Blatt mit A4-Breite. Lange Texte verlängern es; eine automatische Aufteilung in Druckseiten ist nicht enthalten. Cursor und ausgewählte Hinweise werden bei Bedarf ins sichtbare Blatt gescrollt. Ränder bleiben in `.schreib`-Dateien erhalten; ältere Dokumente erhalten zunächst 2,5 cm. TXT/Markdown speichern keinen Seitenaufbau. Der vereinfachte RTF-Weg speichert Text und Grundformatierung; die sichtbaren Seitenränder werden beim RTF-Öffnen auf den Standard gesetzt.

## Reaktionszeit und Prüfverhalten

Die Prüfung startet nach einer Schreibpause und läuft höchstens einmal gleichzeitig. Schnelle Textänderungen brechen die Java-Prüfung nicht ständig ab und erzeugen keine Warteschlange aus ganzen Dokumentprüfungen. Überholte Ergebnisse werden verworfen; nach einer Pause folgt der aktuelle Stand. Bei unverändertem Text entfällt eine erneute automatische Prüfung. „Jetzt prüfen“ bleibt ausführbar.

Unter **Prüfen → Prüfeinstellungen** kann die Pause zwischen 800 und 5000 ms eingestellt und die Automatik abgeschaltet werden. **Prüfen → Automatisch nach Schreibpause prüfen** bietet den Schalter direkt an. Manuelles Prüfen, Synonyme und Korrekturvorschläge bleiben verfügbar.

Wortvorschläge verwenden einen Suchindex, der im Hintergrund geladen wird. Änderungen lösen erst nach einer kurzen Pause eine Vorschlagssuche aus. Tab übernimmt weiterhin; Enter erzeugt einen Absatz. Text und Positionen werden innerhalb desselben Dokumentstands wiederverwendet. Statistik und Literaturanzeige aktualisieren sich nach einer kurzen Pause. Bei sehr vielen Hinweisen zeigt die Seitenleiste zunächst bis zu 200 Einträge; Textmarkierungen und Kontextmenü berücksichtigen weiterhin alle aktiven Hinweise.

**Prüfen → Prüfverhalten und Laufzeiten…** zeigt Prüfungszahlen, Laufzeit und Cache-Aufbauten für die aktuelle Sitzung. Das sind lokale Messwerte, keine Telemetrie oder Leistungszusage für jeden Computer. Es werden keine Texte protokolliert. Die Bedienprüfungen erfassen außerdem Eingabezeiten mit einem längeren Beispielpost und prüfen, dass dabei keine Vollprüfung pro Tastendruck startet.

## Quellen im Text verwenden

Quellenverweise verwenden die normale Textfarbe mit dezenter Unterstreichung und ohne hellen Farbkasten. Nach dem Einfügen steht der Cursor außerhalb der Quelle zum Weiterschreiben. Die Pfeiltasten überspringen Quellen als Ganzes; ein Klick öffnet weiterhin die zugehörige Literaturangabe. Rücktaste unmittelbar hinter einer Quelle oder Entf unmittelbar davor löscht den gesamten Verweis. Eine Auswahl, die in eine Quelle hineinreicht, umfasst automatisch den ganzen Verweis. Das Löschen kann mit Rückgängig aufgehoben werden. Die Quelle bleibt in der lokalen Bibliothek erhalten.

## Synonyme

Ein Wort markieren oder den Cursor in das Wort setzen und „Synonyme“ wählen. Links bleibt der Text, rechts erscheinen Bedeutungsgruppen und Alternativen. Deutsche Registerhinweise wie „ugs.“ werden angezeigt, beim Übernehmen aber nicht in den Text geschrieben. Englische Gruppen enthalten eine kurze WordNet-Bedeutungserklärung.

Synonyme sind keine kontextbewussten KI-Umformulierungen. Prüfe Bedeutung, Register, Großschreibung und Beugung. Nicht jede gebeugte Form steht im Wörterbuch; dann die Grundform suchen. „Prüfen“ kehrt zu den allgemeinen Hinweisen zurück.

## Was die Werte bedeuten

Die Wiederholungserkennung erkennt identische Wörter und mögliche Wortfamilien über vereinfachte Wortstämme sowie einige unregelmäßige Formen. Häufige Funktionswörter werden bei Abstand normalerweise ausgelassen, direkt doppelte Wörter bleiben sichtbar. Eine erkannte Wortfamilie kann ein Fehlalarm sein.

Lesbarkeit nutzt Satz-/Absatzlänge und den LIX: mittlere Satzlänge plus Anteil langer Wörter. Das ist eine grobe Orientierung, kein Qualitätsurteil. Abkürzungen, Fachtexte und gemischte Sprachen können die Werte verzerren.

Füllwörter, Passiv und Nominalstil sind nicht grundsätzlich falsch. Die Hinweise sollen bewusste Entscheidungen erleichtern und lassen sich einzeln abschalten. Es wird nichts ungefragt geändert.

## Daten und Betrieb

Texte bleiben lokal. Die App verwendet eine ausschließlich lokale Verbindung zu ihrer eigenen LanguageTool-Instanz. Sie benötigt keine Cloud-Anmeldung und prüft ohne Internet; Updates erfolgen durch ein neues Paket. Eigene Einstellungen liegen unter `%LOCALAPPDATA%/Byby_Word_and_Excel/writing.json`. Es werden keine Dokumenttexte in dieser Datei gespeichert.

Bei fehlenden Grammatikkomponenten steht ausdrücklich „Nur Stilregeln geprüft“. Dann das vollständige Paket verwenden oder als Entwickler `setup-offline.ps1` und `build.ps1` ausführen. Die App fällt nicht auf einen Cloud-Dienst zurück.
