# PDF, Bilder und R

## Schreib

**Datei → Exportieren → PDF** speichert ein mehrseitiges A4-Dokument mit den eingestellten Seitenrändern. Text bleibt auswählbar und durchsuchbar, Bilder werden eingebettet. Fett, kursiv, Schriftgröße, Ausrichtung, Listen und einfache Tabellen werden übernommen. Ein vorhandenes Literaturverzeichnis wird ergänzt. Seitenumbrüche berechnet der PDF-Export; das Schreibfenster zeigt weiterhin ein fortlaufendes Blatt. Der PDF-Umbruch kann deshalb von der Bildschirmansicht abweichen. Kein Internet, Word oder PDF-Drucker erforderlich. Exportiert wird auf hellem Papier, ohne Prüfunterstreichungen oder Lineale.

**Einfügen → Bild mit Beschriftung** nimmt PNG, JPG, BMP, GIF und TIFF an. Bei mehrseitigen TIFFs oder animierten GIFs wird das erste Bild übernommen. Wähle:

- **Kompakt**: höchstens 1600 Pixel an der längsten Seite; JPEG-Bilder mit Qualität 85 %, Bilder mit Alphakanal bleiben PNG.
- **Volle Auflösung**: Häkchen ausschalten; alle Pixel bleiben erhalten. Die Quelldatei wird nicht verändert.
- **Anzeigebreite in cm**: Das Seitenverhältnis bleibt erhalten. Zu große Bilder werden zusätzlich an die verfügbare A4-Fläche angepasst.
- **Beschriftung**: optional als Überschrift oder Unterschrift. Sie ist danach normaler, bearbeitbarer Text und wird sprachlich geprüft.

Die Bildpixel, Bilddateinamen und eingebetteten Metadaten werden nicht an Schreibprüfung oder Wortvorschläge übergeben. Es gibt keine OCR. Bilder bleiben in `.schreib` und RTF erhalten; TXT/Markdown speichern nur den Text und fragen vor dem Weglassen der Bilder nach. Einfügen einschließlich Beschriftung lässt sich rückgängig machen und wiederholen. Die ursprüngliche Dateikompression wird beim Speichern als RTF nicht bitgenau erhalten; die Pixel und die Anzeigegröße bleiben erhalten. Einzelne Bilder sind auf 50 MB und 40 Megapixel begrenzt. Für große Fotos empfiehlt sich die kompakte Option.

## Tabelle: Daten und Plots nach R

**In RStudio öffnen** in der oberen Leiste exportiert das aktuelle Blatt in einen neuen Projektordner. **Datei → R-Projekt exportieren** speichert dasselbe Projekt ohne Öffnen. Ein bestehender Ordner wird nicht überschrieben. Andere Arbeitsblätter bleiben unberührt.

Im Plotbereich gibt es zusätzlich **ggplot / R exportieren** und **In RStudio öffnen**. Diese nehmen den aktuell sichtbaren Plot mit.

Das Paket enthält:

| Datei | Inhalt |
| --- | --- |
| `Tabelle.Rproj` | Eigenständiges RStudio-Projekt |
| `start.R` | Bearbeitbares R-Skript; darunter Platz für eigenen Code |
| `.Rprofile` | Lädt beim Öffnen dieses Projekts das generierte Skript |
| `daten_roh.csv` | Ursprüngliche Zelltexte, einschließlich Formeln |
| `daten.csv` | Erkannte Zahlen, Datumswerte und berechnete Formelergebnisse |
| `plot_daten.csv` | Genau die im Plot dargestellten Werte in langem Format |
| `plot_ausreisser.csv` | Die im Boxplot angezeigten Ausreißer |
| `plot.png`, `plot.svg` | Aktueller Plot als Bild |

Wenn R installiert ist, führt Tabelle das eigene Skript einmal mit `Rscript --vanilla` im Hintergrund aus. Es lädt dabei keine Pakete herunter und verändert keine vorhandene R-Sitzung. Für reine Daten reichen die R-Grundfunktionen; für Plots wird ein installiertes **ggplot2** benötigt. Bei erfolgreicher Ausführung entstehen außerdem:

- `daten.rds`: Datensatz, mit `readRDS()` laden.
- `plot_daten.rds`: dargestellte Plotwerte.
- `plot.rds`: echtes ggplot-Objekt, mit `plot <- readRDS("plot.rds")` weiterbearbeiten.
- `analyse.RData`: `daten`, `daten_roh`, `plot_daten` und `plot` gemeinsam.

Ohne R bleiben CSV, Skript und Bilder verfügbar. Ohne ggplot2 werden die Daten trotzdem geladen; das Skript erklärt, welches Paket noch fehlt. Ein R-Objekt lässt sich erst mit R erzeugen. Öffne `Tabelle.Rproj` in RStudio oder führe in R `source("Pfad/zum/start.R", encoding = "UTF-8")` aus. RStudio legt `daten` und gegebenenfalls `plot` beim Projektstart in der neuen Sitzung an; bei deaktivierten Startprofilen `start.R` mit **Source** ausführen.

Der ggplot-Export unterstützt Säulen, Balken, Linien, Punkte, Histogramme und Boxplots. Auswahl, Aggregation, Kategorienlimits und Gruppenfarben sind bereits in `plot_daten` berücksichtigt. Histogrammklassen und Boxplot-Statistiken sind vorberechnet: R verwendet dieselben dargestellten Werte, statt neue Klassen oder Quartile zu schätzen. Die Gestaltung kann von der Vorschau abweichen. Für neue statistische Analysen verwende `daten`; `plot_daten` ist der reproduzierbare Stand des angezeigten Plots.

Spalten mit führenden Nullen wie `001` bleiben Text. Numerisch gemischte Spalten werden nicht stillschweigend umgewandelt. Leere numerische Zellen werden `NA`, Text `"NA"` bleibt Text. Datumswerte werden als UTC-Zeitpunkte eingelesen. Originale Spaltennamen, Umlaute, Anführungszeichen und mehrzeilige Zelltexte bleiben in `daten_roh` erhalten. Der Export enthält nur das aktuell geöffnete Blatt; eine Zeilenauswahl begrenzt den Plot, nicht den vollständigen Datensatz.

Alle Funktionen arbeiten lokal. R und RStudio sind optionale, separat installierte Programme.
