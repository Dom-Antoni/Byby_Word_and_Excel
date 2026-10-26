# Schreibassistenz verwenden

1. Das vollständige Offline-Paket entpacken. `Schreib.exe`, `runtime`, `languagetool` und `synonyms` gehören zusammen.
   Formatierung, Referenzmanager und Tab-Vervollständigung sind in [REFERENZEN.md](REFERENZEN.md) beschrieben. Für Dokumente mit Quellen das Format `.schreib` verwenden.
2. Oben Deutsch, English (UK) oder English (US) wählen. Die Sprache gilt für den gesamten Text; gemischtsprachige Absätze werden noch nicht separat erkannt.
3. Schreiben oder vorhandene Texte öffnen. TXT/Markdown, HTML, DOCX und ODT werden unterstützt; bei HTML und Office-Dateien wird die Formatierung vereinfacht. Details stehen unter [Darstellung und Dateien](ANSICHT-UND-DATEIEN.md).
4. Nach einer kurzen Schreibpause erscheinen Stilhinweise und die lokale Grammatikprüfung startet. Der erste Start der Java-Prüfung dauert länger. Unten im Hinweisbereich steht, ob wirklich Grammatik geprüft wurde oder nur die eigenen Stilregeln verfügbar sind.
5. Einen Hinweis anklicken. Die zugehörige Textstelle wird markiert; darunter stehen Erklärung und angebotene Alternativen. Eine Alternative auswählen und „Übernehmen“ anklicken. Normales Rückgängig im Textfeld bleibt verfügbar.

## Hinweise steuern

- **Ignorieren** blendet genau diese Fundstelle auch bei automatischen und manuellen Folgeprüfungen aus. Einfügungen oder Löschungen davor verschieben die gespeicherte Position mit. Wird die Fundstelle selbst geändert, wird sie wieder geprüft. Andere Vorkommen derselben Regel bleiben sichtbar. Im `.schreib`-Format werden ignorierte Fundstellen für dieses Dokument und die gewählte Sprache mitgespeichert; TXT und RTF speichern diese Entscheidungen nicht.
- **Regel aus** deaktiviert genau diese Regel dauerhaft. Unter Einstellungen lässt sie sich wieder aktivieren.
- **Eigenes Wort** nimmt ein markiertes Wort oder das Wort eines Hinweises in das lokale Wörterbuch auf. Es wird bei Rechtschreib- und Wiederholungshinweisen berücksichtigt; Grammatik bleibt prüfbar.
- **Einstellungen** enthält Wortabstand, Satz-/Absatzlänge, Stilregeln, eigene Wörter und bevorzugte Schreibweisen. Beispiel: `Email=>E-Mail`. Jede Zuordnung steht auf einer eigenen Zeile; sie wird für den aktuellen Text unabhängig von der gewählten Sprache angewendet.
- Über den Kategorienfilter lassen sich Rechtschreibung, Grammatik, Wiederholungen, Stil, Lesbarkeit oder Einheitlichkeit getrennt ansehen.
- Alle aktiven Hinweise erscheinen direkt im Text: **rote Wellenlinie** für Rechtschreibung, **blaue Linie** für Grammatik, **gelbe Punktlinie** für Stil, Wiederholung, Lesbarkeit und Einheitlichkeit. Der Kategorienfilter begrenzt die Seitenleiste; die Textmarkierungen zeigen weiterhin alle aktiven Hinweise. Die Linien sind reine Anzeige, verändern keine Formatierung und landen nicht im Export.
- Enter trennt Absätze für die Absatzlängenprüfung. Jeder Absatz wird einzeln gezählt; die Statusleiste zeigt zusätzlich die Anzahl der nicht leeren Textabsätze.

## Quellen im Text

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
