# Schnelle Plots aus der geöffneten Tabelle

## Starten

1. Eine Tabelle öffnen und oben **Plot erstellen** anklicken.
2. Die App schlägt anhand der Spalten einen Plot vor. Unter **Vorschläge** stehen weitere passende Ausgangspunkte.
3. Diagrammtyp, X- und Y-Spalte im Plotbereich wählen. Alternativ auf einen **Spaltenkopf rechtsklicken → Als X-/Y-Achse verwenden**. Bei Wahl einer X-Spalte wird ein passender Typ gewählt: Datum als Linie, Zahlen als Punktdiagramm, Textkategorien als Säulen.
4. Die Vorschau steht rechts unten neben der Tabelle und passt sich der verfügbaren Fläche an. Die Trennlinie lässt sich verschieben. **↗** öffnet eine größere, frei skalierbare Plotansicht; **×** blendet den Plotbereich wieder aus.
5. Unter **Export…** PNG, JPG oder SVG wählen.

Zum Ausprobieren liegt eine erfundene kleine Beispieltabelle unter **examples/Plot-Demo.csv** bei.

## Nach Standort oder anderen Faktoren einfärben

Das statistische Wort ist **Gruppierungsvariable** beziehungsweise **kategorialer Faktor**. Die einzelnen Ausprägungen, etwa Nord, Süd und West, sind seine **Stufen**. Für solche Kategorien wird eine **qualitative Farbpalette** verwendet.

Für die Beispieldatei:

1. **Linie** wählen und **Datum** als X-Achse einstellen.
2. **Messung A** als Y-Achse und unter „Beschriftung und Optionen“ **Messung B** als zweite Y-Reihe wählen.
3. Unter **Gruppieren / Farbe nach (Faktor)** die Spalte **Standort** wählen. Alternativ: Rechtsklick auf den Spaltenkopf Standort → **Als Gruppierungsvariable / Farbe verwenden**.
4. Jeder Standort erhält eine Farbe. **Messung A** verwendet eine durchgezogene Linie und Kreise; **Messung B** eine gestrichelte Linie und Quadrate. Linien verbinden nur Werte derselben Gruppe und Messreihe. Die getrennten Legenden erklären Farbe und Messreihe.

Auch ein **Rechtsklick direkt im Plot** erlaubt die Auswahl oder das Entfernen der Farbgruppierung. Nach jeder Auswahl aktualisiert sich die Vorschau.

Beim geöffneten Rechtsklick-Menü wird der betroffene Spaltenkopf vorübergehend hervorgehoben. Nach der Zuordnung bleiben die Rollen sichtbar:

| Rolle | Markierung |
| --- | --- |
| X-Achse | Blau, Kennzeichnung X |
| Erste Y-Reihe | Grün, Kennzeichnung Y1 |
| Zweite Y-Reihe | Orange, Kennzeichnung Y2 |
| Farbgruppierung | Violett, Kennzeichnung Gruppe |

Eine Spalte kann mehrere Rollen haben; dann werden alle Rollen ausgeschrieben. Die Rollenfarben unterstützen Hell- und Dunkelmodus. Sie markieren die Funktion einer Spalte; die Farben einzelner Gruppen stehen in der Plotlegende.

Die Gruppierung funktioniert auch für Säulen/Balken, Punkte, Histogramme und Boxplots. Säulen/Balken fassen Werte **innerhalb jeder Kombination aus X-Kategorie und Farbgruppe** zusammen; die zweite Messreihe ist zusätzlich heller und am Rahmen unterscheidbar. Histogramme verwenden **gemeinsame Klassengrenzen** für alle Gruppen und zeigen deren Balken nebeneinander. Boxplots stehen je X-Kategorie nach Farbgruppe nebeneinander.

Gruppenfarben werden aus der alphabetisch geordneten vollständigen Gruppenspalte bestimmt. Dadurch bleiben sie beim Umsortieren oder Markieren einer Zeilenteilmenge gleich. Fehlende Gruppenangaben werden ausgelassen und in den Hinweisen gezählt. Standardmäßig werden die ersten **12 Farbgruppen** alphabetisch angezeigt; unter den Optionen lässt sich das auf **1–24** ändern. Eine Begrenzung wird in Vorschau und Exporthinweisen ausdrücklich genannt.

## Diagramme und Vorschläge

| Typ | Verwendung |
| --- | --- |
| Säulen / Balken | Kategorien vergleichen; wahlweise jede Zeile einzeln oder Summe, Mittelwert beziehungsweise Anzahl je Kategorie |
| Linie | Verlauf; Datum oder numerische X-Werte werden auf einer echten X-Skala sortiert. Bei Text-X bleibt die Zeilenreihenfolge erhalten. Verfügbare Werte werden verbunden. |
| Punktdiagramm | Zusammenhang zweier Zahlenspalten; ein Punkt pro gültigem Wertepaar |
| Histogramm | Häufigkeitsverteilung einer Zahlenspalte; automatische Klassenanzahl oder 1–100 eigene Klassen |
| Boxplot | Verteilungen insgesamt oder nach Gruppen: Median, linear interpolierte Quartile, Tukey-Whisker bis 1,5 × IQR und einzelne Ausreißer |

Die Vorschläge sind **lokale Datenerkennung und vorgefertigte Zeichenfunktionen**. Sie verwenden keinen KI-Dienst, kein Sprachmodell und keine Internetverbindung. Diagrammtyp, Datenreihen und Beschriftungen lassen sich anschließend anpassen.

## Bearbeiten

Unter **Beschriftung und Optionen** stehen bereit:

- Eine zweite Y-Reihe für Säulen, Balken, Linien und Punktdiagramme. Beide Reihen teilen dieselbe Skala; für unterschiedliche Einheiten selbst sinnvoll auswählen und beschriften. Zählen, Histogramm und Boxplot verwenden eine Reihe.
- Eigener Titel und Achsenbeschriftungen. Leere Felder verwenden automatisch erzeugte Beschriftungen.
- Summieren, Mitteln oder Zählen gleicher X-Kategorien bei Säulen/Balken. Die gewählte Zusammenfassung steht in der Beschriftung.
- **Nur markierte Zeilen:** Eine oder mehrere Zellen je gewünschter Datenzeile auswählen. Es werden die ganzen betroffenen Zeilen für die gewählten Achsen verwendet.
- Zahlenerkennung **Auto**, **Deutsch** oder **English**. Auto interpretiert ein einzelnes Komma oder einen einzelnen Punkt als Dezimalzeichen. Bei mehrdeutigen Tausendertrennzeichen das passende Zahlenformat ausdrücklich wählen.
- Anzahl der Histogrammklassen, Kategorienbegrenzung und Sortierung der Kategorien nach Betrag.
- Y-Achse ab null sowie ein optionaler dunkler Plot-Hintergrund. Säulen, Balken und Histogramme beziehen die Nulllinie immer ein.
- Exportgrößen 1200 × 800, 1800 × 1200 oder 2400 × 1600 Pixel.

Prozentzeichen werden als Prozentpunkte gelesen: **12,5 %** ergibt den Zahlenwert **12,5**. Fehlende Werte werden ausgelassen und nicht als null interpretiert. Bei einer zweiten Reihe werden fehlende Werte je Reihe behandelt.

Die Grafik ändert keine Zellen. Bei Blattwechseln erhält jedes Blatt seine eigenen Ploteinstellungen für die laufende Sitzung. Eine neu geöffnete Datei startet mit neuen Vorschlägen; Einstellungen werden derzeit nicht mit der Tabelle gespeichert. Geänderte Zellwerte aktualisieren den sichtbaren Plot nach einer kurzen Pause.

## Grenzen und Export

Die Hinweise unter der Vorschau nennen verwendete Zeilen, ungültige Werte und Anzeigegrenzen. Eine begrenzte Anzeige gilt auch für den Export:

- Standardmäßig maximal 20 X-Kategorien, einstellbar bis 100. Es erscheinen die ersten Kategorien oder, bei Sortierung, die größten Beträge. Das separate Limit der Farbgruppen ist oben beschrieben.
- Bis zu 5000 Punkte je Punktreihe und 2000 Punkte je Linienreihe und Farbgruppe. Größere Reihen werden gleichmäßig ausgewählt; erster und letzter gültiger Punkt bleiben erhalten.
- Bis zu 1000 gleichmäßig ausgewählte Ausreißer je Boxplotgruppe. Die Quartile und Whisker werden zuvor aus allen gültigen Werten berechnet.

Histogramme zählen alle gültigen ausgewählten Werte der angezeigten Farbgruppen. Die Vorschau zeigt zusätzliche Details beim Überfahren von Balken, Säulen oder Punkten mit der Maus.

**PNG/JPG** sind Rasterbilder mit der gewählten Größe. **SVG** enthält echte Vektorformen und Text und bleibt skalierbar; es enthält keine Skripte oder verknüpften Bilder. Der Export wird zunächst in eine temporäre Datei geschrieben und ersetzt die Zieldatei erst bei Erfolg.

Excel-/ODS-Daten werden so geplottet, wie sie der Werteimport liefert, einschließlich gespeicherter Formelergebnisse. Die Plot-Funktion fügt keinen vollständigen Excel-Rechenkern hinzu. Für komplexe Berechnungen bleibt R der passende nächste Schritt.
