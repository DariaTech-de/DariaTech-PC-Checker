#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Check - Bewertungsregeln
#
# Reine Funktionen: Werte rein, Ampel raus. Keine Systemaufrufe, damit sie sich
# auf jedem Rechner (auch im Build-Server) automatisiert testen lassen.
# Siehe mac/selftest.sh
#
# Ampelstufen (wie in der Windows-App): ok | info | warn | crit
# ---------------------------------------------------------------------------

# Schlechtere von zwei Ampeln ermitteln.
worse_severity() {
    local a="$1" b="$2"
    local rank_a rank_b
    rank_a=$(severity_rank "$a")
    rank_b=$(severity_rank "$b")
    if [ "$rank_a" -ge "$rank_b" ]; then printf '%s' "$a"; else printf '%s' "$b"; fi
}

severity_rank() {
    case "$1" in
        ok)   printf '0' ;;
        info) printf '1' ;;
        warn) printf '2' ;;
        crit) printf '3' ;;
        *)    printf '1' ;;
    esac
}

# Freier Speicherplatz in Prozent -> Ampel (wie Windows: <15 % warn, <10 % kritisch).
sev_disk_free_pct() {
    local pct="${1:-100}"
    if [ "$pct" -lt 10 ]; then printf 'crit'
    elif [ "$pct" -lt 15 ]; then printf 'warn'
    else printf 'ok'; fi
}

# Akku: Zustand und Ladezyklen. Apple nennt alles ausser "Normal" auffaellig.
sev_battery() {
    local condition="${1:-Normal}" cycles="${2:-0}"
    case "$condition" in
        Normal|normal|"") ;;
        *) printf 'warn'; return ;;
    esac
    if [ "$cycles" -gt 1000 ]; then printf 'warn'; else printf 'ok'; fi
}

# Laufzeit ohne Neustart (Tage) - wie Windows: ab 14 Tagen Hinweis.
sev_uptime_days() {
    local days="${1:-0}"
    if [ "$days" -gt 14 ]; then printf 'warn'; else printf 'ok'; fi
}

# Abstuerze der letzten 30 Tage.
sev_crash_count() {
    local n="${1:-0}"
    if [ "$n" -ge 10 ]; then printf 'crit'
    elif [ "$n" -ge 3 ]; then printf 'warn'
    else printf 'ok'; fi
}

# Kernel Panics: schon einer ist ein ernster Befund.
sev_panic_count() {
    local n="${1:-0}"
    if [ "$n" -ge 3 ]; then printf 'crit'
    elif [ "$n" -ge 1 ]; then printf 'warn'
    else printf 'ok'; fi
}

# Alter der letzten Time-Machine-Sicherung in Tagen; -1 = keine vorhanden.
sev_backup_age_days() {
    local days="${1:--1}"
    if [ "$days" -lt 0 ]; then printf 'warn'      # gar keine Sicherung
    elif [ "$days" -gt 30 ]; then printf 'warn'
    elif [ "$days" -gt 7 ]; then printf 'info'
    else printf 'ok'; fi
}

# Sicherheitsschalter (FileVault, Firewall, SIP, Gatekeeper): an = ok, aus = warn.
sev_switch_on() {
    case "${1:-}" in
        on|On|ON|enabled|Enabled|aktiv|yes|Yes|1) printf 'ok' ;;
        *) printf 'warn' ;;
    esac
}

# SMART-Status des Datentraegers.
sev_smart() {
    case "${1:-}" in
        Verified|verified|OK|ok) printf 'ok' ;;
        "") printf 'info' ;;
        *) printf 'crit' ;;
    esac
}

# Ausstehende Systemupdates.
sev_updates() {
    local n="${1:-0}"
    if [ "$n" -gt 0 ]; then printf 'warn'; else printf 'ok'; fi
}

# Gesundheits-Score wie in der Windows-App: 100 - 20 je kritisch - 7 je Warnung.
health_score() {
    local crit="${1:-0}" warn="${2:-0}" score
    score=$(( 100 - crit * 20 - warn * 7 ))
    if [ "$score" -lt 0 ]; then score=0; fi
    if [ "$score" -gt 100 ]; then score=100; fi
    printf '%s' "$score"
}

# Bytes menschenlesbar (B/KB/MB/GB/TB) - Dezimalstelle wie im Windows-Bericht.
human_size() {
    local bytes="${1:-0}"
    if [ "$bytes" -lt 0 ]; then bytes=0; fi
    if [ "$bytes" -lt 1024 ]; then printf '%s B' "$bytes"; return; fi
    awk -v b="$bytes" 'BEGIN {
        split("KB MB GB TB PB", u, " ");
        v = b / 1024; i = 1;
        while (v >= 1024 && i < 5) { v /= 1024; i++ }
        printf "%.1f %s", v, u[i];
    }'
}

# HTML-Sonderzeichen entschaerfen (Reihenfolge: & zuerst) - wie im Windows-Bericht.
html_escape() {
    printf '%s' "${1:-}" | sed -e 's/&/\&amp;/g' -e 's/</\&lt;/g' -e 's/>/\&gt;/g'
}
