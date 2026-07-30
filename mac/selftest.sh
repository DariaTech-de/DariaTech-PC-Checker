#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Doktor - Selbsttest
#
# Prüft die Teile, die ohne macOS testbar sind: Bewertungsregeln, Bericht und
# vor allem die Sicherheitsschranke der Aufräum-Funktionen. Läuft deshalb auch
# auf dem Build-Server (Linux) und in der GitHub-Action.
#
#   bash mac/selftest.sh
# ---------------------------------------------------------------------------

set -uo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
. "$SCRIPT_DIR/lib/rules.sh"
. "$SCRIPT_DIR/lib/report.sh"
. "$SCRIPT_DIR/lib/cleanup.sh"

PASS=0
FAIL=0

ok() { PASS=$((PASS + 1)); }
bad() { FAIL=$((FAIL + 1)); printf '  FEHLER: %s\n' "$1"; }

expect() {
    # expect <Beschreibung> <erwartet> <tatsächlich>
    if [ "$2" = "$3" ]; then ok; else bad "$1 – erwartet „$2“, war „$3“"; fi
}

expect_contains() {
    case "$3" in
        *"$2"*) ok ;;
        *) bad "$1 – „$2“ fehlt in der Ausgabe" ;;
    esac
}

expect_not_contains() {
    case "$3" in
        *"$2"*) bad "$1 – „$2“ hätte nicht vorkommen dürfen" ;;
        *) ok ;;
    esac
}

expect_true()  { if "$@"; then ok; else bad "Bedingung nicht erfüllt: $*"; fi; }
expect_false() { if "$@"; then bad "Bedingung hätte falsch sein müssen: $*"; else ok; fi; }

printf '\nDariaTech Mac-Doktor – Selbsttest\n\n'

# --------------------------------------------------------------------------
printf 'Bewertungsregeln\n'

expect "Speicherplatz 50 %% frei"  ok   "$(sev_disk_free_pct 50)"
expect "Speicherplatz 15 %% frei"  ok   "$(sev_disk_free_pct 15)"
expect "Speicherplatz 14 %% frei"  warn "$(sev_disk_free_pct 14)"
expect "Speicherplatz 10 %% frei"  warn "$(sev_disk_free_pct 10)"
expect "Speicherplatz 9 %% frei"   crit "$(sev_disk_free_pct 9)"
expect "Speicherplatz 0 %% frei"   crit "$(sev_disk_free_pct 0)"

expect "Akku Normal, wenig Zyklen"      ok   "$(sev_battery Normal 250)"
expect "Akku Normal, 1000 Zyklen"       ok   "$(sev_battery Normal 1000)"
expect "Akku Normal, 1001 Zyklen"       warn "$(sev_battery Normal 1001)"
expect "Akku Service Recommended"       warn "$(sev_battery "Service Recommended" 10)"
expect "Akku Zustand unbekannt (leer)"  ok   "$(sev_battery "" 0)"

expect "Laufzeit 14 Tage"  ok   "$(sev_uptime_days 14)"
expect "Laufzeit 15 Tage"  warn "$(sev_uptime_days 15)"

expect "0 Abstürze"   ok   "$(sev_crash_count 0)"
expect "2 Abstürze"   ok   "$(sev_crash_count 2)"
expect "3 Abstürze"   warn "$(sev_crash_count 3)"
expect "10 Abstürze"  crit "$(sev_crash_count 10)"

expect "0 Panics"  ok   "$(sev_panic_count 0)"
expect "1 Panic"   warn "$(sev_panic_count 1)"
expect "3 Panics"  crit "$(sev_panic_count 3)"

expect "keine Sicherung"        warn "$(sev_backup_age_days -1)"
expect "Sicherung heute"        ok   "$(sev_backup_age_days 0)"
expect "Sicherung 7 Tage alt"   ok   "$(sev_backup_age_days 7)"
expect "Sicherung 8 Tage alt"   info "$(sev_backup_age_days 8)"
expect "Sicherung 31 Tage alt"  warn "$(sev_backup_age_days 31)"

expect "Schalter an"       ok   "$(sev_switch_on on)"
expect "Schalter enabled"  ok   "$(sev_switch_on enabled)"
expect "Schalter aus"      warn "$(sev_switch_on off)"
expect "Schalter leer"     warn "$(sev_switch_on "")"

expect "SMART Verified"  ok   "$(sev_smart Verified)"
expect "SMART Failing"   crit "$(sev_smart "Failing!")"
expect "SMART unbekannt" info "$(sev_smart "")"

expect "keine Updates"  ok   "$(sev_updates 0)"
expect "2 Updates"      warn "$(sev_updates 2)"

# --------------------------------------------------------------------------
printf 'Gesundheits-Score (identisch zur Windows-App)\n'

expect "sauberes System"          100 "$(health_score 0 0)"
expect "eine Warnung"              93 "$(health_score 0 1)"
expect "ein kritischer Befund"     80 "$(health_score 1 0)"
expect "zwei kritisch, drei warn"  39 "$(health_score 2 3)"
expect "Score nie unter 0"          0 "$(health_score 9 9)"

expect "schlimmere Ampel gewinnt (ok/warn)"   warn "$(worse_severity ok warn)"
expect "schlimmere Ampel gewinnt (crit/warn)" crit "$(worse_severity crit warn)"
expect "gleiche Ampel"                        info "$(worse_severity info info)"

# --------------------------------------------------------------------------
printf 'Formatierung\n'

expect "512 Byte"   "512 B"    "$(human_size 512)"
expect "1 KB"       "1.0 KB"   "$(human_size 1024)"
expect "1,5 MB"     "1.5 MB"   "$(human_size 1572864)"
expect "2 GB"       "2.0 GB"   "$(human_size 2147483648)"
expect "negativ"    "0 B"      "$(human_size -5)"

expect "HTML-Escape"  "a &lt;b&gt; &amp; c" "$(html_escape 'a <b> & c')"

# --------------------------------------------------------------------------
printf 'Bericht\n'

report_reset
report_add "Bereich A" "Etikett 1" "Wert 1" ok
report_add "Bereich B" "Etikett 2" "Wert 2" warn "Detailtext B" "Tipp B"
report_add "Bereich A" "Etikett 3" "Wert 3" crit "Detailtext A"
report_add "Bereich A" "Etikett 4" "Wert <script>" info

expect "kritische Befunde gezählt" 1 "$(report_count crit)"
expect "Warnungen gezählt"         1 "$(report_count warn)"
expect "Gesamtampel"            crit "$(report_overall)"
expect "Score aus Befunden"       73 "$(report_score)"

AREAS=$(report_areas | tr '\n' '|')
expect "Bereiche in Reihenfolge, ohne Dopplung" "Bereich A|Bereich B|" "$AREAS"

HTML=$(report_html "Testmac" "01.01.2026 10:00 Uhr")
expect_contains "Kopfzeile"        "DariaTech"            "$HTML"
expect_contains "Rechnername"      "Testmac"              "$HTML"
expect_contains "Score im Kopf"    "Gesundheit 73/100"    "$HTML"
expect_contains "Markenfarbe"      "#0E3B34"              "$HTML"
expect_contains "Bereichstitel"    "<h2>Bereich A</h2>"   "$HTML"
expect_contains "Detail erscheint" "Detailtext B"         "$HTML"
expect_contains "Tipp erscheint"   "Tipp B"               "$HTML"
expect_contains "Impressum"        "kontakt@dariatech.de" "$HTML"
expect_contains "Zusammenfassung crit" "ampel crit"       "$HTML"
expect_not_contains "kein ungeschütztes Script-Tag" "<script>" "$HTML"
expect_contains "escaptes Script-Tag" "&lt;script&gt;"    "$HTML"

# Ein sauberes System darf keine Ampel-Kacheln zeigen.
report_reset
report_add "Bereich" "alles gut" "ja" ok
CLEAN=$(report_html "Testmac" "heute")
expect_contains "Grünmeldung" "sieht gesund aus" "$CLEAN"
expect "Score sauber" 100 "$(report_score)"
expect "Gesamtampel sauber" ok "$(report_overall)"

# Zeilenumbrüche dürfen die Feldstruktur nicht zerreißen.
report_reset
report_add "Bereich" "mehrzeilig" "$(printf 'Zeile1\nZeile2')" warn
expect "Anzahl Einträge trotz Umbruch" 1 "${#DT_RESULTS[@]}"
expect_contains "beide Zeilen erhalten" "Zeile1 Zeile2" "$(report_html M h)"

# --------------------------------------------------------------------------
printf 'Sicherheitsschranke beim Aufräumen\n'

FAKE_HOME="/Users/testkunde"

expect_true  cleanup_is_safe_target "$FAKE_HOME/Library/Caches/Google/Chrome/Default/Cache" "$FAKE_HOME"
expect_true  cleanup_is_safe_target "$FAKE_HOME/Library/Caches/com.apple.Safari" "$FAKE_HOME"
expect_true  cleanup_is_safe_target "$FAKE_HOME/Library/Caches/Firefox/Profiles/abc.default/cache2" "$FAKE_HOME"

# Alles, was kein Cache ist, muss abgelehnt werden.
expect_false cleanup_is_safe_target "$FAKE_HOME" "$FAKE_HOME"
expect_false cleanup_is_safe_target "/" "$FAKE_HOME"
expect_false cleanup_is_safe_target "/Users" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Documents" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Documents/Cache" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Desktop/Projekt/Cache" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Pictures/Fotos/Cache" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Library" "$FAKE_HOME"
expect_false cleanup_is_safe_target "relativ/Cache" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Library/Caches/../../Documents" "$FAKE_HOME"
expect_false cleanup_is_safe_target "$FAKE_HOME/Library/Caches/Chrome/Cache/" "$FAKE_HOME"
expect_false cleanup_is_safe_target "/etc/passwd" "$FAKE_HOME"
expect_false cleanup_is_safe_target "" "$FAKE_HOME"

# Nutzdaten sind tabu, selbst wenn der Pfad unter einem Cache-Zweig liegt.
expect_false cleanup_is_safe_target \
    "$FAKE_HOME/Library/Application Support/Google/Chrome/Default/Local Storage" "$FAKE_HOME"
expect_false cleanup_is_safe_target \
    "$FAKE_HOME/Library/Application Support/Google/Chrome/Default/IndexedDB" "$FAKE_HOME"
expect_false cleanup_is_safe_target \
    "$FAKE_HOME/Library/Application Support/Google/Chrome/Default/Cookies" "$FAKE_HOME"

# --------------------------------------------------------------------------
printf 'Cache-Ordner werden tatsächlich gefunden (Testbaum)\n'

TREE=$(mktemp -d)
trap 'rm -rf "$TREE"' EXIT

# Realistischer Chrome-Aufbau auf dem Mac - genau hier lag der ursprüngliche
# Fehler: unter Windows liegt alles in einem Ordner, auf dem Mac in zwei.
mkdir -p "$TREE/Library/Caches/Google/Chrome/Default/Cache/Cache_Data"
mkdir -p "$TREE/Library/Caches/Google/Chrome/Default/Code Cache/js"
mkdir -p "$TREE/Library/Caches/Google/Chrome/Profile 1/Cache/Cache_Data"
mkdir -p "$TREE/Library/Application Support/Google/Chrome/Default/GPUCache"
mkdir -p "$TREE/Library/Application Support/Google/Chrome/Default/DawnCache"
mkdir -p "$TREE/Library/Application Support/Google/Chrome/ShaderCache"
mkdir -p "$TREE/Library/Application Support/Google/Chrome/Default/Local Storage/leveldb"
mkdir -p "$TREE/Library/Caches/Firefox/Profiles/xyz.default-release/cache2/entries"

# Inhalte anlegen (1 MB je Cache-Datei, damit du sie messen kannst).
for d in "$TREE/Library/Caches/Google/Chrome/Default/Cache/Cache_Data" \
         "$TREE/Library/Caches/Google/Chrome/Profile 1/Cache/Cache_Data" \
         "$TREE/Library/Application Support/Google/Chrome/Default/GPUCache" \
         "$TREE/Library/Caches/Firefox/Profiles/xyz.default-release/cache2/entries"; do
    dd if=/dev/zero of="$d/blob.bin" bs=1024 count=1024 >/dev/null 2>&1
done
printf 'wichtig' > "$TREE/Library/Application Support/Google/Chrome/Default/Local Storage/leveldb/000003.log"

FOUND=$(cleanup_browser_dirs chrome "$TREE" | sort)
expect_contains "Chrome: Cache-Zweig gefunden" \
    "Library/Caches/Google/Chrome/Default/Cache" "$FOUND"
expect_contains "Chrome: Code Cache gefunden" \
    "Library/Caches/Google/Chrome/Default/Code Cache" "$FOUND"
expect_contains "Chrome: zweites Profil gefunden" \
    "Library/Caches/Google/Chrome/Profile 1/Cache" "$FOUND"
expect_contains "Chrome: GPUCache im Datenzweig gefunden" \
    "Library/Application Support/Google/Chrome/Default/GPUCache" "$FOUND"
expect_contains "Chrome: DawnCache gefunden" \
    "Library/Application Support/Google/Chrome/Default/DawnCache" "$FOUND"
expect_contains "Chrome: ShaderCache gefunden" \
    "Library/Application Support/Google/Chrome/ShaderCache" "$FOUND"
expect_not_contains "Chrome: Local Storage wird NICHT angefasst" \
    "Local Storage" "$FOUND"

FFFOUND=$(cleanup_browser_dirs firefox "$TREE")
expect_contains "Firefox: cache2 gefunden" "cache2" "$FFFOUND"

SIZE=$(cleanup_browser_size chrome "$TREE")
expect_true test "$SIZE" -gt $((2 * 1024 * 1024))

# Löschen: nur der Cache verschwindet, die Nutzdaten bleiben.
while IFS= read -r d; do
    [ -n "$d" ] || continue
    cleanup_is_safe_target "$d" "$TREE" || continue
    find "$d" -mindepth 1 -maxdepth 1 -exec rm -rf {} + 2>/dev/null
done <<EOF
$(cleanup_browser_dirs chrome "$TREE")
EOF

expect_false test -f "$TREE/Library/Caches/Google/Chrome/Default/Cache/Cache_Data/blob.bin"
expect_false test -f "$TREE/Library/Caches/Google/Chrome/Profile 1/Cache/Cache_Data/blob.bin"
expect_false test -f "$TREE/Library/Application Support/Google/Chrome/Default/GPUCache/blob.bin"
expect_true  test -f "$TREE/Library/Application Support/Google/Chrome/Default/Local Storage/leveldb/000003.log"
expect_true  test -f "$TREE/Library/Caches/Firefox/Profiles/xyz.default-release/cache2/entries/blob.bin"
# Übrig bleiben nur die leeren Ordnergerüste (wenige KB) - die Inhalte sind weg.
expect_true test "$(cleanup_browser_size chrome "$TREE")" -lt 102400

# --------------------------------------------------------------------------
printf 'Syntaxprüfung aller Skripte\n'
for f in "$SCRIPT_DIR"/dariatech-mac-check.sh "$SCRIPT_DIR"/lib/*.sh "$SCRIPT_DIR"/selftest.sh; do
    if bash -n "$f" 2>/dev/null; then ok; else bad "Syntaxfehler in $f"; fi
done

# --------------------------------------------------------------------------
printf '%s\n' "" "------------------------------------------------------------"
printf '  %s Prüfungen bestanden, %s fehlgeschlagen\n' "$PASS" "$FAIL"
printf '%s\n' "------------------------------------------------------------" ""
[ "$FAIL" -eq 0 ] || exit 1
