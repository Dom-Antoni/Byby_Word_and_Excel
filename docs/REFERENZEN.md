# Referenzen und Wortvorschläge

## Einfachster Weg: Rechtsklick und Link

1. Im Dokument an der gewünschten Stelle rechtsklicken und **Referenz hinzufügen** wählen.
2. **Aus Bibliothek** wählt eine gespeicherte Quelle. Für eine neue Quelle **Aus Link / DOI** öffnen und die Adresse oder DOI einfügen.
3. **Angaben abrufen** ruft ausschließlich den Link bzw. DOI-Metadaten ab. Dokumenttext, Benutzerkonten, Browser-Cookies und Passwörter werden nicht übertragen. Es wird keine KI benötigt und kein Browser geöffnet.
4. Titel, Autorinnen/Autoren und Jahr kontrollieren. Unter **Weitere Angaben** stehen Publikation, Verlag, Datum, Band, Heft, Seiten, DOI, URL, Abrufdatum und Quellentyp. Fehlende Angaben werden nicht erfunden; bei blockierten Seiten oder offline können sie manuell ergänzt werden.
5. **Speichern & zitieren** speichert die Quelle in der lokalen Bibliothek und setzt ein farbiges Zitat. **Nur in Bibliothek speichern** legt sie ohne Zitat ab. Ausgewählter Text wird bei der Einfügung nicht gelöscht.

Die gleiche Funktion ist rechts im Referenzbereich über **Referenz hinzufügen** erreichbar. Die lokale Bibliothek steht auch beim nächsten Start und in anderen Dokumenten bereit. Speicherort: `%LOCALAPPDATA%/Byby_Word_and_Excel/references.json`. Eine `.bak`-Datei enthält den vorherigen Stand. Einträge werden über DOI, URL oder passende bibliografische Angaben zusammengeführt; ihre internen Verknüpfungen bleiben erhalten. Mehrere App-Instanzen werden beim Speichern durch eine Dateibibliotheks-Sperre koordiniert.

`.schreib`-Dateien enthalten nur die Quellen, die im jeweiligen Dokument zitiert oder ausdrücklich ins Literaturverzeichnis aufgenommen wurden, nicht automatisch die gesamte persönliche Bibliothek. Quellen eines geöffneten Schreib-Dokuments werden in die lokale Bibliothek übernommen. Neue/leere Dokumente löschen die Bibliothek nicht.

Unterstützt werden DOI-Register per CSL-Metadaten, bibliografische HTML-Metatags, JSON-LD sowie OpenGraph/Seitentitel als Rückfall. Reine PDF-Links, Anmeldeseiten, dynamisch nachgeladene Seiten und Webseiten ohne passende Metadaten liefern möglicherweise keine vollständigen Angaben. Bei fehlender automatischer Erkennung die Angaben selbst ergänzen. Der Import ist keine allgemeine Browser-Erweiterung mit allen seitenbezogenen Zotero-Übersetzern.

## Warum die Bibliothek in Schreib liegt

Für den direkten Rechtsklick-Ablauf ist eine integrierte Bibliothek einfacher als ein zusätzliches Programm. Eine dritte App ist dafür nicht nötig. EndNote bleibt nutzbar: **RIS exportieren** schreibt die Bibliothek als standardisierte RIS-Datei. In EndNote über **File → Import → File → Reference Manager (RIS)** importieren; bei eingerichteter EndNote-Dateizuordnung genügt ein Doppelklick auf die Datei. Derselbe Export eignet sich für Zotero und Mendeley.

Die App behauptet dabei keine erfolgreiche automatische Übernahme in eine geöffnete EndNote-Bibliothek und verändert deren Datenbank nicht direkt. Quellen aus dem Web haben keine EndNote-Datensatznummer; um sie mit dem EndNote-RTF-/Format-Paper-Weg zu nutzen, zunächst in EndNote importieren und anschließend dessen XML-Export wieder in Schreib einlesen.

## Dokumentansicht und Speichern

Die Schreib-App bietet eine weiße Dokumentfläche auf grauem Hintergrund, eine Werkzeugleiste mit Schriftwahl, Schriftgröße, Fett/Kursiv/Unterstreichen, Überschriften und Aufzählungen sowie die Bereiche Schreibprüfung und Referenzen. Ein vollständiges Word-Seitenlayout ist nicht vorgesehen.

Das Format **`.schreib`** speichert formatierten Text, importierte Quellen, Zitatverknüpfungen und den einfachen Zitiermodus gemeinsam. TXT/Markdown speichern nur den Text. RTF erhält Formatierungen, aber nicht die vollständige Quellenbibliothek und den bearbeitbaren Zusammenhang wie `.schreib`. DOCX wird weiterhin nur als Text importiert.

„RTF exportieren“ erstellt eine separate Datei einschließlich Literaturverzeichnis. „Speichern“ einer normalen RTF-Datei speichert dagegen den aktuellen Dokumenttext, ohne erneut ein Verzeichnis anzuhängen.

## EndNote unter Windows

EndNotes Word-Add-in (Cite While You Write) ist keine allgemeine Schnittstelle für beliebige Schreibprogramme. Diese App verwendet deshalb den vom Hersteller beschriebenen Austauschweg für andere Textverarbeitungen und ersetzt nicht EndNotes Datenbankverwaltung.

1. In EndNote die gewünschten Referenzen auswählen und über **File → Export → XML** exportieren. Für alle Quellen „Export Selected References“ abwählen. Die Originalbibliothek wird nicht verändert.
2. In Schreib rechts **Referenzen → Importieren** wählen und die XML-Datei öffnen. Alternativ wird RIS unterstützt, bei EndNote mit dem Ausgabestil „RefMan (RIS) Export“.
3. Autor, Titel, Jahr oder DOI im Suchfeld suchen, eine Quelle auswählen und **Zitieren** drücken. Das Zitat wird an der aktuellen Textauswahl eingefügt und farbig hervorgehoben.
4. Ein Klick auf das Zitat zeigt den vollständigen Eintrag im Literaturbereich. **Strg+Z direkt nach dem Sprung** kehrt zur Textstelle zurück. Nach einer Textänderung gilt wieder das normale Rückgängig. Andere Rückgängig-Aktionen ohne vorherigen Zitiersprung bleiben ebenfalls unverändert.
5. **Alle hinzufügen** nimmt alle importierten Quellen in das Literaturverzeichnis auf; es werden nicht alle Quellen ungefragt in den Fließtext geschrieben.

Erneuter Import derselben Datei aktualisiert passende Einträge und speichert sie in der integrierten lokalen Bibliothek. Gleiche DOI, URL oder Titel/Jahr/Autoren dienen der Zuordnung. Das ist ein dateibasierter Austausch, keine automatische Live-Synchronisation einer geöffneten ENL-Datei. Das Programm schreibt nicht in die EndNote-Datenbank.

**EndNotes Zitierstile verwenden:** Als `.schreib` speichern, dann **EndNote-RTF** exportieren. Dafür müssen die Quellen aus EndNote-XML stammen und gültige Datensatznummern besitzen. In EndNote die passende Originalbibliothek öffnen und **Tools → Format Paper** verwenden. EndNote erzeugt eine separate Datei mit seinen formatierten Zitaten und seinem Literaturverzeichnis. Der Export verändert das Schreib-Dokument nicht. Seitenzahlen, Präfixe, Suffixe und komplexe Mehrfachzitate sind in dieser ersten Integration nicht bearbeitbar.

Aus EndNote kopierte einfache Platzhalter wie `{Müller, 2024 #42}` können nach XML-Import über **EndNote-Platzhalter verbinden** verknüpft werden. Mehrdeutige Datensatznummern oder mehrteilige Platzhalter bleiben unverändert. Ein beliebiges RIS-/Zotero-ID wird nicht als EndNote-Datensatznummer ausgegeben.

## Andere Referenzmanager

RIS ist der gemeinsame Austauschweg, etwa für Zotero und Mendeley. **CSL-JSON** mit Autoren und `issued/date-parts` wird ebenfalls eingelesen. Im jeweiligen Referenzmanager exportieren und die Datei in Schreib importieren. BibTeX/BibLaTeX und proprietäre Bibliotheksdatenbanken werden derzeit nicht direkt gelesen.

Die eingebauten Modi „Autor–Jahr (einfach)“ und „Nummeriert (einfach)“ sind bewusst einfache Darstellungen, keine vollständige APA-/Vancouver-/CSL-Engine. Nummern bleiben an die Reihenfolge der importierten Quellen gebunden; dadurch sind Lücken möglich. Für verbindliche EndNote-Stile den EndNote-RTF-Weg verwenden. Wichtige bibliografische Felder werden übernommen; nicht sämtliche Spezialfelder aller Referenztypen.

Für eine erste Bedienprobe liegt `examples/Referenzen-demo.xml` bei. Die enthaltene Musterquelle ist fiktiv und keine wissenschaftliche Quelle. Den EndNote-Export mit der eigenen Bibliothek praktisch prüfen, bevor er für eine Einreichung eingesetzt wird.

## Wortvorschläge mit Tab

Die Vorschläge entstehen vollständig lokal aus deutschen/englischen Wörterbüchern und bereits geschriebenen Wörtern bzw. Wortfolgen im aktuellen Text. Ab zwei Buchstaben werden bis zu drei passende Ergänzungen angezeigt. **Pfeil hoch/runter** wählt, **Tab** übernimmt. **Enter** übernimmt keinen Vorschlag und bleibt für den Absatzwechsel. Escape verwirft die Vorschläge. Über „Wortvorschläge · Tab übernimmt“ lassen sie sich abschalten.

Das ist eine Wörterbuch- und Textvervollständigung, kein Sprachmodell. Einige Mehrwortfolgen werden ebenfalls angeboten; eine freie inhaltliche Satzvorhersage ist nicht enthalten.

## Herstellerdokumentation

- EndNote: https://docs.endnote.com/docs/endnote/2025/v1/windows/en/content/09word/word_processor_comptblty.htm
- EndNote-XML exportieren: https://docs.endnote.com/docs/endnote/2025/v1/windows/en/content/15independentbibs_export/exporting_to_endnote_xml.htm
- EndNote-RTF/Format Paper: https://docs.endnote.com/docs/endnote/2025/v1/windows/en/content/00working_with_documents/00working_with_documents.htm
- RIS und CSL-JSON in Zotero: https://www.zotero.org/support/dev/data_formats
- DOI-Metadaten: https://www.crossref.org/documentation/retrieve-metadata/content-negotiation
- EndNote Direct Export/RIS: https://docs.endnote.com/docs/endnote/2025/v1/windows/en/content/appendices/enabling_direct_export.htm
