#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Doktor
#
# Schnellprüfung für macOS mit HTML-Bericht zum Aushändigen - im gleichen
# Design wie der Windows-Bericht.
#
# Aufruf:   bash dariatech-mac-check.sh [Optionen]
#
# Es wird nichts installiert und nichts nachgeladen. Alle Prüfungen sind
# rein lesend; Reparaturen laufen nur, wenn sie ausdrücklich angefordert
# werden, und fragen vorher nach.
# ---------------------------------------------------------------------------

set -uo pipefail

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
# shellcheck source=lib/rules.sh
. "$SCRIPT_DIR/lib/rules.sh"
# shellcheck source=lib/report.sh
. "$SCRIPT_DIR/lib/report.sh"
# shellcheck source=lib/cleanup.sh
. "$SCRIPT_DIR/lib/cleanup.sh"
# shellcheck source=lib/collect.sh
. "$SCRIPT_DIR/lib/collect.sh"

OUT_DIR="$HOME/Desktop"
SKIP_UPDATES=0
WRITE_REPORT=1
OPEN_REPORT=0
FIX_CACHE=0
FIX_DNS=0
ASSUME_YES=0

usage() {
    cat <<'EOF'
DariaTech Mac-Doktor – Schnellprüfung für macOS

  bash dariatech-mac-check.sh [Optionen]

Optionen
  --out ORDNER      Zielordner für den Bericht (Standard: ~/Desktop)
  --no-report       nur Ausgabe im Terminal, keine HTML-Datei
  --open            Bericht nach dem Erstellen öffnen
  --skip-updates    Update-Suche überspringen (die dauert am längsten)

  --fix-cache       Browser-Zwischenspeicher leeren (Browser müssen zu sein)
  --fix-dns         DNS-Zwischenspeicher leeren (fragt nach dem Kennwort)
  --yes             Rückfragen bei Reparaturen überspringen

  -h, --help        diese Hilfe

Rückgabewert: 0 = alles in Ordnung, 1 = Warnungen, 2 = kritische Befunde.
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT_DIR="${2:-}"; shift 2 ;;
        --no-report) WRITE_REPORT=0; shift ;;
        --open) OPEN_REPORT=1; shift ;;
        --skip-updates) SKIP_UPDATES=1; shift ;;
        --fix-cache) FIX_CACHE=1; shift ;;
        --fix-dns) FIX_DNS=1; shift ;;
        --yes|-y) ASSUME_YES=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) printf 'Unbekannte Option: %s\n\n' "$1" >&2; usage >&2; exit 64 ;;
    esac
done
export SKIP_UPDATES

if [ "$(uname -s)" != "Darwin" ]; then
    printf 'Dieses Werkzeug läuft nur auf macOS. Für Windows gibt es den DariaTech PC-Doktor.\n' >&2
    exit 65
fi

confirm() {
    [ "$ASSUME_YES" = "1" ] && return 0
    local answer
    printf '%s [j/N] ' "$1"
    read -r answer </dev/tty || return 1
    case "$answer" in j|J|y|Y|ja|Ja) return 0 ;; *) return 1 ;; esac
}

step() { printf '  ... %s\n' "$1"; }

# --------------------------------------------------------------------------
printf '\n'
printf '  DariaTech %s – Schnellprüfung\n' "$DT_PRODUCT"
printf '  %s\n\n' "$(date '+%d.%m.%Y %H:%M') Uhr"

report_reset
step "System & macOS";            collect_system
step "Prozessor & Speicher";      collect_cpu_memory
step "Speicherplatz";             collect_disk_space
step "Datenträger-Gesundheit";    collect_smart
step "Akku";                      collect_battery
step "Sicherheit";                collect_security
step "Schadsoftware-Hinweise";    collect_malware
step "Automatischer Start";       collect_autostart
step "Abstürze";                  collect_crashes
step "Netzwerk";                  collect_network
step "Datensicherung";            collect_backup
step "Spotlight-Suche";           collect_search
step "Zwischenspeicher";          collect_cache
if [ "$SKIP_UPDATES" = "1" ]; then
    step "Updates (übersprungen)"
else
    step "Software-Updates (dauert etwas)"
fi
collect_updates

report_console

printf '\n  ------------------------------------------------------------\n'
printf '  Gesundheit: %s/100   ·   %s kritisch, %s Warnung(en)\n' \
    "$(report_score)" "$(report_count crit)" "$(report_count warn)"
printf '  ------------------------------------------------------------\n'

# --------------------------------------------------------------------------
# Reparaturen (nur auf ausdrücklichen Wunsch)
# --------------------------------------------------------------------------
if [ "$FIX_CACHE" = "1" ]; then
    printf '\n  Browser-Zwischenspeicher leeren\n'
    if confirm "  Fortfahren? Gelöscht wird ausschließlich Zwischenspeicher – Lesezeichen, Kennwörter und Sitzungen bleiben erhalten."; then
        while IFS= read -r b; do
            [ -n "$b" ] || continue
            printf '    %s' "$(cleanup_browser "$b")"
        done <<EOF
$(cleanup_all_browsers)
EOF
    else
        printf '    abgebrochen.\n'
    fi
fi

if [ "$FIX_DNS" = "1" ]; then
    printf '\n  DNS-Zwischenspeicher leeren\n'
    if confirm "  Fortfahren? Dafür wird das Administrator-Kennwort abgefragt."; then
        if sudo dscacheutil -flushcache 2>/dev/null && sudo killall -HUP mDNSResponder 2>/dev/null; then
            printf '    DNS-Zwischenspeicher geleert.\n'
        else
            printf '    Fehlgeschlagen – bitte manuell ausführen: sudo dscacheutil -flushcache\n'
        fi
    else
        printf '    abgebrochen.\n'
    fi
fi

# --------------------------------------------------------------------------
if [ "$WRITE_REPORT" = "1" ]; then
    mkdir -p "$OUT_DIR" 2>/dev/null
    computer=$(scutil --get ComputerName 2>/dev/null)
    [ -z "$computer" ] && computer=$(hostname)
    file="$OUT_DIR/DariaTech_${DT_PRODUCT}_$(printf '%s' "$computer" | tr -c 'A-Za-z0-9' '_')_$(date '+%Y-%m-%d_%H%M').html"

    if report_html "$computer" "$(date '+%d.%m.%Y %H:%M') Uhr" > "$file" 2>/dev/null; then
        printf '\n  Bericht: %s\n' "$file"
        [ "$OPEN_REPORT" = "1" ] && open "$file" 2>/dev/null
    else
        printf '\n  Bericht konnte nicht geschrieben werden (Ordner: %s)\n' "$OUT_DIR" >&2
    fi
fi

printf '\n'
case "$(report_overall)" in
    crit) exit 2 ;;
    warn) exit 1 ;;
    *)    exit 0 ;;
esac
