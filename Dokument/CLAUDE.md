# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Zweck

Dieser Ordner ist der Arbeitsbericht zur Anwendung `wissen` (im übergeordneten Ordner, siehe `../CLAUDE.md`) als mdBook.

**Der Inhalt stammt vom Nutzer.** Er schreibt seine Rohtexte in `RAW/`; Claude macht daraus fertige Artikel, erfindet aber nichts.

## Arbeitsablauf: von RAW zum fertigen Artikel

Wenn der Nutzer verlangt, die Rohtexte zu verarbeiten:

1. Jede Datei in `RAW/` lesen und als fertigen Artikel nach `src/` schreiben und in `src/SUMMARY.md` eintragen.
2. Dabei **nur Rechtschreibung, Grammatik und Zeichensetzung korrigieren**. Nichts hinzudichten: keine neuen Aussagen, Beispiele, Einleitungen, Zusammenfassungen oder Ausschmückungen, und nichts weglassen. Wortwahl und Stil des Nutzers bleiben erhalten. Ist eine Stelle unverständlich oder mehrdeutig, nachfragen statt raten.
3. Erst wenn der Artikel in `src/` geschrieben ist, die verarbeitete Rohdatei aus `RAW/` löschen. Dafür den absoluten Pfad verwenden (`rm /home/thorsten/wissen/Dokument/RAW/<Datei>`), als eigenen Befehl ohne Verkettung – nur so greift die Erlaubnisregel in `../.claude/settings.json`.

Außerhalb dieses Ablaufs keine Kapitel anlegen und keine Texte in `src/` schreiben oder umformulieren. In `RAW/` schreibt Claude nie etwas.

## Befehle

Aus diesem Ordner heraus (aus dem Projektstamm jeweils mit `Dokument` als Argument):

```bash
mdbook serve --open   # Vorschau unter http://localhost:3000, baut bei Änderungen neu
mdbook build          # HTML nach book/ erzeugen
```

Installiert ist mdbook 0.5.x über `cargo install mdbook`.

## Aufbau

- `book.toml` – Titel, Autor, Sprache (`de`); Quellen in `src/`, Ausgabe in `book/`.
- `src/SUMMARY.md` – Inhaltsverzeichnis. Nur dort eingetragene Kapitel erscheinen im Buch; ein eingetragenes, aber fehlendes Kapitel legt mdbook beim Build als leere Datei an.
- `RAW/` – Rohtexte, die von Menschen geschrieben werden (siehe Arbeitsablauf). Der Ordner liegt außerhalb von `src/` und erscheint deshalb nicht im Buch.
- `book/` – erzeugte Ausgabe, per `.gitignore` ausgeschlossen; nie von Hand bearbeiten.

Der gesamte Ordner liegt außerhalb der .NET-Projekte (`../src`, `../tests`) und gehört damit nicht zum Build.
