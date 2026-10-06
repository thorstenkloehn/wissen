# Dokument – Arbeitsbericht

Dieser Ordner enthält den Arbeitsbericht zur Anwendung `wissen` als mdBook.

## Neuen Artikel erstellen

### 1. Rohtext schreiben

Eine neue Markdown-Datei in `RAW/` anlegen, zum Beispiel:

```
Dokument/RAW/datenbank.md
```

Den Text einfach herunterschreiben. Rechtschreibung, Grammatik und Zeichensetzung müssen noch nicht stimmen.

Eine Datei ergibt einen Artikel. Mehrere Rohtexte können gleichzeitig in `RAW/` liegen.

### 2. Claude die Rohtexte verarbeiten lassen

In Claude Code schreiben:

```
Verarbeite die Rohtexte
```

Claude macht dann für jede Datei in `RAW/` Folgendes:

1. Die Rohdatei lesen.
2. Den fertigen Artikel nach `src/` schreiben.
3. Den Artikel in `src/SUMMARY.md` eintragen, damit er im Buch erscheint.
4. Die Rohdatei aus `RAW/` löschen.

### 3. Was Claude am Text ändert

Claude korrigiert **nur Rechtschreibung, Grammatik und Zeichensetzung**.

- Es wird nichts hinzugefügt: keine neuen Aussagen, Beispiele, Einleitungen oder Zusammenfassungen.
- Es wird nichts weggelassen.
- Wortwahl und Stil bleiben erhalten.
- Ist eine Stelle unverständlich oder mehrdeutig, fragt Claude nach.

### 4. Ergebnis ansehen

Aus diesem Ordner heraus:

```bash
mdbook serve --open   # Vorschau unter http://localhost:3000, baut bei Änderungen neu
mdbook build          # HTML nach book/ erzeugen
```

Aus dem Projektstamm heraus jeweils mit `Dokument` als Argument, zum Beispiel `mdbook serve Dokument --open`.

## Aufbau des Ordners

| Pfad | Inhalt |
| --- | --- |
| `RAW/` | Rohtexte. Hier schreibt nur der Mensch; Claude schreibt dort nie etwas. |
| `src/` | Fertige Artikel. Sie entstehen nur über den Ablauf oben. |
| `src/SUMMARY.md` | Inhaltsverzeichnis. Nur dort eingetragene Artikel erscheinen im Buch. |
| `book/` | Erzeugte HTML-Ausgabe. Nie von Hand bearbeiten. |
| `book.toml` | Titel, Autor und Sprache des Buchs. |
| `CLAUDE.md` | Regeln für Claude zu diesem Ordner. |
