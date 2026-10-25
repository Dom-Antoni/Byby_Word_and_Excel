# Ziel und nächste Schritte

Ziel sind zwei eigenständige lokale Windows-Anwendungen für den tatsächlichen Alltagsbedarf. Kein Office-Paket, keine Kontopflicht, kein Abo, keine Cloud-Prüfung, keine aufwendige Seitengestaltung und keine Statistikfunktionen, die bereits in R erledigt werden.

## Vor einer ersten Veröffentlichung

Bereits eingebaut: lokales LanguageTool für Deutsch/Englisch, eigene Stilregeln, einstellbare Wiederholungen, lokale Synonyme beider Sprachen, eigene Wörter, bevorzugte Schreibweisen und deaktivierbare Regeln. Sprachmodelle und YAML-Regelpakete bleiben für später.

Außerdem umgesetzt: formatierter Dokumenteditor, Tab-Vervollständigung, anklickbare Zitate, EndNote-XML/RIS/CSL-JSON-Import, EndNote-RTF-Export und eine dokumentübergreifende lokale Quellenbibliothek. Der Rechtsklick-Dialog kann Quellen aus einem Link oder einer DOI erfassen; nur dieser ausdrücklich gestartete Abruf benötigt Internet. Eine separate Referenzmanager-App ist dafür vorerst nicht nötig.

- Beide Oberflächen auf dem Zielrechner prüfen: Öffnen, Schreiben, Speichern, Abbrechen, Schließen und Wiedereinlesen.
- Deutsche und englische Wörterbücher, Vorschläge und Sprachumschaltung mit realistischen Texten testen.
- Tabellenbearbeitung samt Formelzeile und Blattwechsel prüfen; Mehrzellen-Einfügen ergänzen.
- Import an repräsentativen vorhandenen DOCX- und XLSX-Dateien prüfen, insbesondere Datumswerte, Sonderzeichen, Formelergebnisse und mehrere Tabellenblätter.
- GitHub-Repository anlegen, Quellcode pushen, Windows-Build prüfen, erst dann einen Release veröffentlichen.

## Schreiben ausbauen

- Die integrierte lokale Grammatikprüfung mit repräsentativen Alltagstexten vergleichen: Fehlalarme, übersehene Fehler und Geschwindigkeit.
- Direkte Unterstreichungen im Editor ergänzen; derzeit markieren anklickbare Hinweise die Textstelle. Morphologische Wiederholungserkennung und Erkennung komplexer Satzstrukturen verbessern.
- Sprachmodelle erst nach praktischer Nutzung evaluieren: optionale Umformulierungen mit ausdrücklicher Übernahme, keine stillen Textänderungen.
- Wiederherstellung nach Absturz, Suchen/Ersetzen, Zoom und zuletzt geöffnete Dateien.
- Einfachen DOCX-Export und zuverlässigeren Textimport ergänzen, ohne Anspruch auf Word-Layouttreue.

## Tabellen ausbauen

- Umgesetzt: übersichtliche Leseansicht mit festen Spaltennamen und virtuellen Zeilen/Spalten, automatische Text-Trennzeichen, ODS-/HTML-/Excel-XML-Import sowie gängige Excel-Datumsformate. Weitere Zahlenformate und repräsentative Alltagsdateien prüfen.
- Eigene Arbeitsmappen mit mehreren Blättern und Formeln verlustfrei speichern; klarer Export zu CSV und XLSX.
- Kopieren/Einfügen rechteckiger Bereiche, Rückgängig/Wiederholen, Sortieren, Filtern, Suchen und Spaltenbreiten.
- Nur die im Alltag benötigten zusätzlichen Formeln ergänzen.

## Fertig für den persönlichen Umstieg

Die Apps sind erst dann ein ausreichender Ersatz, wenn vorhandene Alltagsdateien gelesen werden können, Speichern und Wiederherstellung zuverlässig funktionieren und die lokale Sprachprüfung den persönlichen Qualitätsanspruch erfüllt. Der aktuelle Prototyp erreicht diese Abnahme noch nicht.
