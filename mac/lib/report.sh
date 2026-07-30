#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Check - Ergebnissammlung, Konsolenausgabe und HTML-Bericht
#
# Der Bericht sieht bewusst genauso aus wie der Windows-Bericht (gleiche
# Marke, gleiche Farben, gleicher Gesundheits-Score), damit der Kunde nicht
# merkt, dass dahinter zwei verschiedene Werkzeuge stecken.
#
# Reine Textverarbeitung, keine Systemaufrufe -> auf jedem Rechner testbar.
# ---------------------------------------------------------------------------

# Trennzeichen zwischen den Feldern (Unit Separator, kommt in Systemausgaben
# nicht vor).
DT_US=$'\x1f'

# Firmendaten - identisch zu Models/CompanyInfo.cs der Windows-App.
DT_PRODUCT="Mac-Doktor"
DT_COMPANY="DariaTech IT-Systemhaus"
DT_STREET="Josef-Schmid-Weg 23"
DT_CITY="87700 Memmingen"
DT_PHONE="+49 8331 99 59 369"
DT_EMAIL="kontakt@dariatech.de"
DT_BRAND_DARK="#0E3B34"
DT_BRAND_GREEN="#2FA86A"
DT_BRAND_SHADOW="#1C6E46"
DT_BRAND_MINT="#6FE0A8"

DT_RESULTS=()

report_reset() { DT_RESULTS=(); }

# report_add <Bereich> <Bezeichnung> <Wert> [Ampel] [Detail] [Tipp]
report_add() {
    local area label value sev detail tip
    area=$(_one_line "$1")
    label=$(_one_line "$2")
    value=$(_one_line "$3")
    sev="${4:-info}"
    detail=$(_one_line "${5:-}")
    tip=$(_one_line "${6:-}")
    DT_RESULTS+=("${area}${DT_US}${label}${DT_US}${value}${DT_US}${sev}${DT_US}${detail}${DT_US}${tip}")
}

# Zeilenumbrueche entfernen - die Felder werden zeilenweise gespeichert.
_one_line() { printf '%s' "${1:-}" | tr '\n\r\t' '   ' | sed -e 's/  */ /g' -e 's/^ //' -e 's/ $//'; }

# Anzahl Befunde einer Ampelstufe.
report_count() {
    local want="$1" n=0 line sev
    for line in ${DT_RESULTS+"${DT_RESULTS[@]}"}; do
        sev=$(printf '%s' "$line" | cut -d"$DT_US" -f4)
        [ "$sev" = "$want" ] && n=$((n + 1))
    done
    printf '%s' "$n"
}

# Gesamtampel ueber alle Befunde.
report_overall() {
    if [ "$(report_count crit)" -gt 0 ]; then printf 'crit'
    elif [ "$(report_count warn)" -gt 0 ]; then printf 'warn'
    else printf 'ok'; fi
}

report_score() { health_score "$(report_count crit)" "$(report_count warn)"; }

# Bereiche in der Reihenfolge ihres ersten Auftretens (stabile Sortierung).
report_areas() {
    local line area seen=""
    for line in ${DT_RESULTS+"${DT_RESULTS[@]}"}; do
        area=$(printf '%s' "$line" | cut -d"$DT_US" -f1)
        case "${DT_US}${seen}" in
            *"${DT_US}${area}${DT_US}"*) continue ;;
        esac
        seen="${seen}${area}${DT_US}"
        printf '%s\n' "$area"
    done
}

# --------------------------------------------------------------------------
# Konsolenausgabe
# --------------------------------------------------------------------------

_color() {
    [ -t 1 ] || { printf '%s' "$2"; return; }
    case "$1" in
        ok)   printf '\033[32m%s\033[0m' "$2" ;;
        warn) printf '\033[33m%s\033[0m' "$2" ;;
        crit) printf '\033[31m%s\033[0m' "$2" ;;
        head) printf '\033[1;36m%s\033[0m' "$2" ;;
        *)    printf '%s' "$2" ;;
    esac
}

_symbol() {
    case "$1" in
        ok)   printf '[ok]  ' ;;
        warn) printf '[!]   ' ;;
        crit) printf '[!!]  ' ;;
        *)    printf '[i]   ' ;;
    esac
}

report_console() {
    local area line l_area label value sev detail tip
    while IFS= read -r area; do
        printf '\n'
        _color head "== $area"
        printf '\n'
        for line in ${DT_RESULTS+"${DT_RESULTS[@]}"}; do
            IFS="$DT_US" read -r l_area label value sev detail tip <<EOF
$line
EOF
            [ "$l_area" = "$area" ] || continue
            printf '  %s' "$(_color "$sev" "$(_symbol "$sev")")"
            printf '%-34s %s\n' "$label" "$value"
            [ -n "$detail" ] && printf '        %s\n' "$detail"
            [ -n "$tip" ] && printf '        -> %s\n' "$tip"
        done
    done <<EOF
$(report_areas)
EOF
}

# --------------------------------------------------------------------------
# HTML-Bericht
# --------------------------------------------------------------------------

_logo_svg() {
    cat <<EOF
<svg width="42" height="42" viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg" aria-label="DariaTech">
  <polygon points="32,8 32,33 32,56 10.5,32" fill="$DT_BRAND_GREEN"/>
  <polygon points="32,8 53.5,32 32,56 32,33" fill="$DT_BRAND_SHADOW"/>
  <polygon points="32,8 42.75,20 32,33 21.25,20" fill="$DT_BRAND_MINT"/>
</svg>
EOF
}

# report_html <Rechnername> <Zeitstempel-Text>
report_html() {
    local computer="${1:-Mac}" stamp="${2:-}"
    local crit warn score scoreclass
    crit=$(report_count crit)
    warn=$(report_count warn)
    score=$(report_score)
    if [ "$score" -ge 80 ]; then scoreclass=ok
    elif [ "$score" -ge 50 ]; then scoreclass=warn
    else scoreclass=crit; fi

    cat <<EOF
<!DOCTYPE html><html lang="de"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>DariaTech $DT_PRODUCT - $(html_escape "$computer")</title>
<style>
  body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Arial,sans-serif;background:#f4f6f9;color:#1a2433;margin:0;padding:32px;}
  .wrap{max-width:860px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);}
  header{background:$DT_BRAND_DARK;color:#fff;padding:20px 32px;display:flex;justify-content:space-between;align-items:center;gap:16px;}
  .brand{display:flex;align-items:center;gap:12px;}
  .brand .mark{line-height:0;display:flex;}
  .brand .name{font-size:21px;font-weight:700;letter-spacing:.3px;line-height:1;}
  .brand .tag{font-size:10px;letter-spacing:3px;color:$DT_BRAND_MINT;text-transform:uppercase;margin-top:3px;}
  .meta{text-align:right;font-size:12.5px;color:#bfe3d6;}
  .meta .doc{font-size:15px;color:#fff;font-weight:600;margin-bottom:2px;}
  .content{padding:24px 32px;}
  .ampel{padding:10px 14px;border-radius:6px;margin:6px 0;font-size:14px;}
  .ampel.ok{background:#e6f6ea;color:#1a7f37;border-left:4px solid #2da44e;}
  .ampel.warn{background:#fff8e1;color:#9a6700;border-left:4px solid #e0b000;}
  .ampel.crit{background:#fdecea;color:#b3261e;border-left:4px solid #d32f2f;}
  h2{font-size:15px;color:$DT_BRAND_DARK;margin:22px 0 6px;border-bottom:2px solid #eef1f5;padding-bottom:4px;}
  table{width:100%;border-collapse:collapse;font-size:13.5px;}
  td{padding:6px 8px;border-bottom:1px solid #f0f2f5;}
  td.label{color:#5a6877;width:230px;vertical-align:top;}
  td.ok{color:#1a7f37;} td.warn{color:#9a6700;font-weight:600;} td.crit{color:#b3261e;font-weight:600;}
  td .detail{color:#6b7782;font-weight:400;font-size:12px;margin-top:3px;}
  td .tip{color:#1a5e4a;font-weight:400;font-size:12px;margin-top:4px;background:#eef7f3;border-left:3px solid #2f9e7a;padding:5px 8px;border-radius:4px;}
  footer{padding:16px 32px;font-size:12px;border-top:1px solid #eef1f5;background:#f7faf9;}
  footer .pub{color:$DT_BRAND_DARK;font-size:12.5px;}
  footer .disclaimer{color:#9aa6b0;margin-top:6px;}
  .score{display:inline-block;margin-top:6px;padding:3px 10px;border-radius:12px;font-size:12.5px;font-weight:600;}
  .score.ok{background:#1f7a46;color:#eafff2;}
  .score.warn{background:#9a6700;color:#fff7e6;}
  .score.crit{background:#a32b22;color:#ffeceb;}
</style></head>
<body><div class="wrap">
<header>
  <div class="brand">
    <span class="mark">$(_logo_svg)</span>
    <div><div class="name">DariaTech</div><div class="tag">IT-Systemhaus</div></div>
  </div>
  <div class="meta">
    <div class="doc">$DT_PRODUCT &middot; Kundenbericht</div>
    <div>$(html_escape "$computer") &middot; $(html_escape "$stamp")</div>
    <div class="score $scoreclass">Gesundheit $score/100</div>
  </div>
</header>
<div class="content">
<h2>Zusammenfassung</h2>
EOF

    if [ "$crit" -eq 0 ] && [ "$warn" -eq 0 ]; then
        printf '%s\n' "<div class='ampel ok'>Keine Auffälligkeiten gefunden – der Mac sieht gesund aus.</div>"
    else
        _summary_lines crit
        _summary_lines warn
    fi

    _html_sections

    cat <<EOF
</div>
<footer>
  <div class="pub"><strong>$(html_escape "$DT_COMPANY")</strong> &middot; $(html_escape "$DT_STREET") &middot; $(html_escape "$DT_CITY")</div>
  <div class="pub">Telefon: $(html_escape "$DT_PHONE") &middot; E-Mail: $(html_escape "$DT_EMAIL")</div>
  <div class="disclaimer">Automatisch erstellt mit dem DariaTech $DT_PRODUCT. Werte ohne Gewähr.</div>
</footer>
</div></body></html>
EOF
}

_summary_lines() {
    local want="$1" line area label value sev detail tip text
    for line in ${DT_RESULTS+"${DT_RESULTS[@]}"}; do
        IFS="$DT_US" read -r area label value sev detail tip <<EOF
$line
EOF
        [ "$sev" = "$want" ] || continue
        if [ -n "$detail" ]; then text="$detail"; else text="$area – $label: $value"; fi
        printf "<div class='ampel %s'>%s</div>\n" "$want" "$(html_escape "$text")"
    done
}

_html_sections() {
    local area line l_area label value sev detail tip
    while IFS= read -r area; do
        [ -n "$area" ] || continue
        printf '<h2>%s</h2><table>\n' "$(html_escape "$area")"
        for line in ${DT_RESULTS+"${DT_RESULTS[@]}"}; do
            IFS="$DT_US" read -r l_area label value sev detail tip <<EOF
$line
EOF
            [ "$l_area" = "$area" ] || continue
            printf "<tr><td class='label'>%s</td><td class='%s'>%s" \
                "$(html_escape "$label")" "$sev" "$(html_escape "$value")"
            [ -n "$detail" ] && printf "<div class='detail'>%s</div>" "$(html_escape "$detail")"
            [ -n "$tip" ] && printf "<div class='tip'>&#128161; %s</div>" "$(html_escape "$tip")"
            printf '</td></tr>\n'
        done
        printf '</table>\n'
    done <<EOF
$(report_areas)
EOF
}
