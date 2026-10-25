# App-Symbole

- **Tabelle:** grüner Taschenrechner, Farbe **#16834A**.
- **Schreib:** blaue Tastatur, Farbe **#2463C7**.

Die Symbole bestehen jeweils aus einer einzigen Farbe auf transparentem Hintergrund. Bildschirm und Tasten sind transparente Aussparungen. Die Vorlagen sind eigene Vektorgrafiken; es werden keine Microsoft-Logos oder externen Bildpakete verwendet.

Die SVG-Quellen liegen unter **assets/icons/**. Beim normalen Build erzeugt **tools/IconBuilder.cs** daraus ICO-Dateien mit den Größen **16, 20, 24, 32, 40, 48, 64, 128 und 256 Pixel** sowie eine PNG-Vorschau. Dafür sind keine zusätzlichen Downloads oder Bildprogramme erforderlich.

Der Compiler bettet das jeweilige Symbol als Windows-EXE-Icon ein. Eine zweite eingebettete Ressource versorgt die WPF-Fenster einschließlich ihrer Dialoge. Die Icon-Dateien müssen dafür zur Laufzeit nicht separat neben der EXE liegen.

Das vollständige Downloadpaket enthält zusätzlich den Ordner **icons/** mit ICO, PNG und SVG für beide Apps. Die neuen Symbole werden beim Start der neu gebauten EXE-Dateien verwendet.
