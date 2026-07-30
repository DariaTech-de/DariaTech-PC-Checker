#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Check - Datenerhebung
#
# Alle Prüfungen sind REIN LESEND. Jede Funktion fängt ihre Fehler selbst ab:
# fehlt ein Werkzeug oder eine Berechtigung, wird das als "nicht prüfbar"
# gemeldet - der Durchlauf bricht nie ab (gleiche Regel wie in der Windows-App).
#
# Nur macOS-Bordmittel, keine Installation, kein Download.
# ---------------------------------------------------------------------------

A_SYSTEM="System & macOS"
A_CPU="Prozessor & Arbeitsspeicher"
A_DISK="Datenträger – Speicherplatz"
A_SMART="Datenträger – Gesundheit (SMART)"
A_BATTERY="Akku"
A_SECURITY="Sicherheit"
A_MALWARE="Schadsoftware & Fremdsoftware"
A_UPDATE="Software-Updates"
A_AUTOSTART="Automatischer Start"
A_CRASH="Abstürze & Stabilität"
A_NETWORK="Netzwerk"
A_BACKUP="Datensicherung (Time Machine)"
A_SEARCH="Spotlight-Suche"
A_CACHE="Zwischenspeicher & Ballast"

_have() { command -v "$1" >/dev/null 2>&1; }

# Erste Zahl aus einem Text (z. B. "Cycle Count: 312" -> 312).
_num() { printf '%s' "${1:-}" | tr -dc '0-9' | head -c 9; }

_field() {
    # _field "<Text>" "<Schlüssel>" -> Wert hinter dem ersten Doppelpunkt
    printf '%s\n' "$1" | awk -F': *' -v k="$2" '
        { line=$0; sub(/^[ \t]+/, "", line) }
        index(line, k ":") == 1 { sub(/^[^:]*: */, "", line); print line; exit }'
}

# --------------------------------------------------------------------------
collect_system() {
    local name ver build model chip serial hw boot now up_days up_hours

    if _have sw_vers; then
        name=$(sw_vers -productName 2>/dev/null)
        ver=$(sw_vers -productVersion 2>/dev/null)
        build=$(sw_vers -buildVersion 2>/dev/null)
        report_add "$A_SYSTEM" "Betriebssystem" "${name:-macOS} ${ver:-?} (Build ${build:-?})" ok
    else
        report_add "$A_SYSTEM" "Betriebssystem" "nicht prüfbar" info
    fi

    hw=$(system_profiler SPHardwareDataType 2>/dev/null)
    if [ -n "$hw" ]; then
        model=$(_field "$hw" "Model Name")
        chip=$(_field "$hw" "Chip")
        [ -z "$chip" ] && chip=$(_field "$hw" "Processor Name")
        serial=$(_field "$hw" "Serial Number (system)")
        [ -n "$model" ] && report_add "$A_SYSTEM" "Modell" "$model" ok
        [ -n "$chip" ] && report_add "$A_SYSTEM" "Prozessor" "$chip" ok
        [ -n "$serial" ] && report_add "$A_SYSTEM" "Seriennummer" "$serial" ok
    fi

    boot=$(sysctl -n kern.boottime 2>/dev/null | sed -n 's/.*sec = \([0-9]*\).*/\1/p')
    now=$(date +%s)
    if [ -n "$boot" ] && [ "$boot" -gt 0 ] 2>/dev/null; then
        up_days=$(( (now - boot) / 86400 ))
        up_hours=$(( ((now - boot) % 86400) / 3600 ))
        report_add "$A_SYSTEM" "Laufzeit ohne Neustart" "${up_days} Tage, ${up_hours} Std." \
            "$(sev_uptime_days "$up_days")" \
            "$([ "$up_days" -gt 14 ] && printf 'Der Mac läuft seit %s Tagen ohne Neustart.' "$up_days")" \
            "$([ "$up_days" -gt 14 ] && printf 'Ein Neustart räumt Arbeitsspeicher und hängende Dienste auf.')"
    fi
}

# --------------------------------------------------------------------------
collect_cpu_memory() {
    local cores load1 pct total_bytes free_pct swap used_swap sev

    cores=$(sysctl -n hw.ncpu 2>/dev/null)
    load1=$(sysctl -n vm.loadavg 2>/dev/null | awk '{print $2}')
    if [ -n "$cores" ] && [ -n "$load1" ]; then
        # Auslastung in Prozent = Last / Kerne. Über 90 % ist es eng.
        pct=$(awk -v l="$load1" -v c="$cores" 'BEGIN{ if(c<1)c=1; printf "%d", (l/c)*100 }')
        if [ "$pct" -gt 90 ]; then sev=warn; else sev=ok; fi
        report_add "$A_CPU" "Auslastung (1-Minuten-Mittel)" "${pct} % bei ${cores} Kernen" "$sev" \
            "$([ "$sev" = warn ] && printf 'Der Prozessor ist dauerhaft stark ausgelastet.')" \
            "$([ "$sev" = warn ] && printf 'In der Aktivitätsanzeige nachsehen, welches Programm die Last erzeugt.')"
    fi

    total_bytes=$(sysctl -n hw.memsize 2>/dev/null)
    [ -n "$total_bytes" ] && report_add "$A_CPU" "Arbeitsspeicher" "$(human_size "$total_bytes")" ok

    if _have memory_pressure; then
        free_pct=$(memory_pressure 2>/dev/null | sed -n 's/.*free percentage: *\([0-9]*\)%.*/\1/p' | head -1)
        if [ -n "$free_pct" ]; then
            if [ "$free_pct" -lt 10 ]; then sev=warn; else sev=ok; fi
            report_add "$A_CPU" "Freier Arbeitsspeicher" "${free_pct} %" "$sev" \
                "$([ "$sev" = warn ] && printf 'Weniger als 10 %% Arbeitsspeicher frei – der Mac lagert auf die SSD aus und wird langsam.')" \
                "$([ "$sev" = warn ] && printf 'Nicht benötigte Programme und Browser-Tabs schließen.')"
        fi
    fi

    swap=$(sysctl -n vm.swapusage 2>/dev/null)
    if [ -n "$swap" ]; then
        used_swap=$(printf '%s' "$swap" | sed -n 's/.*used = *\([0-9.]*\)M.*/\1/p')
        used_swap=${used_swap%%.*}
        [ -z "$used_swap" ] && used_swap=0
        if [ "$used_swap" -gt 4096 ]; then sev=warn; else sev=ok; fi
        report_add "$A_CPU" "Auslagerungsdatei (Swap)" "${used_swap} MB belegt" "$sev" \
            "$([ "$sev" = warn ] && printf 'Über 4 GB werden ausgelagert – der Arbeitsspeicher reicht für die Arbeitsweise nicht aus.')" \
            "$([ "$sev" = warn ] && printf 'Weniger Programme gleichzeitig öffnen oder Arbeitsspeicher aufrüsten (bei Apple Silicon nur beim Neukauf möglich).')"
    fi
}

# --------------------------------------------------------------------------
collect_disk_space() {
    local line mount used_pct free_pct size avail sev found=0

    while IFS= read -r line; do
        [ -n "$line" ] || continue
        mount=$(printf '%s' "$line" | awk '{ $1=$1; for(i=1;i<=8;i++) $i=""; sub(/^ +/,""); print }')
        [ -z "$mount" ] && mount=$(printf '%s' "$line" | awk '{print $NF}')
        size=$(printf '%s' "$line" | awk '{print $2}')
        avail=$(printf '%s' "$line" | awk '{print $4}')
        used_pct=$(printf '%s' "$line" | awk '{print $5}' | tr -d '%')
        case "$used_pct" in ''|*[!0-9]*) continue ;; esac

        free_pct=$((100 - used_pct))
        sev=$(sev_disk_free_pct "$free_pct")
        found=1
        report_add "$A_DISK" "$mount" \
            "$(human_size $((avail * 1024))) frei von $(human_size $((size * 1024))) (${free_pct} %)" \
            "$sev" \
            "$([ "$sev" != ok ] && printf 'Auf %s sind nur noch %s %% frei.' "$mount" "$free_pct")" \
            "$([ "$sev" != ok ] && printf 'Papierkorb leeren, große Dateien auslagern, unter „Systemeinstellungen > Allgemein > Speicher“ aufräumen.')"
    done <<EOF
$(df -kl 2>/dev/null | awk 'NR>1 && $1 ~ /^\/dev\// && $2+0 > 0')
EOF

    if [ "$found" -eq 0 ]; then
        report_add "$A_DISK" "Status" "nicht prüfbar" info
    fi
}

# --------------------------------------------------------------------------
collect_smart() {
    local disk status sev found=0 info

    while IFS= read -r disk; do
        [ -n "$disk" ] || continue
        info=$(diskutil info "$disk" 2>/dev/null)
        [ -n "$info" ] || continue
        status=$(_field "$info" "SMART Status")
        [ -z "$status" ] && continue
        found=1
        sev=$(sev_smart "$status")
        report_add "$A_SMART" "$disk" "$status" "$sev" \
            "$([ "$sev" = crit ] && printf 'Der Datenträger %s meldet einen SMART-Fehler – ein Ausfall droht.' "$disk")" \
            "$([ "$sev" = crit ] && printf 'SOFORT eine vollständige Sicherung anlegen und den Datenträger tauschen. Nicht mehr lange weiterarbeiten.')"
    done <<EOF
$(diskutil list physical 2>/dev/null | sed -n 's|^/dev/\(disk[0-9]*\) .*|\1|p')
EOF

    if [ "$found" -eq 0 ]; then
        report_add "$A_SMART" "Status" \
            "nicht prüfbar (bei vielen NVMe-SSDs meldet macOS keinen SMART-Wert)" info
    fi
}

# --------------------------------------------------------------------------
collect_battery() {
    local power condition cycles maxcap sev charge

    power=$(system_profiler SPPowerDataType 2>/dev/null)
    if [ -z "$power" ] || ! printf '%s' "$power" | grep -q "Battery Information"; then
        report_add "$A_BATTERY" "Status" "kein Akku (Desktop-Mac)" ok
        return
    fi

    condition=$(_field "$power" "Condition")
    cycles=$(_num "$(_field "$power" "Cycle Count")")
    maxcap=$(_num "$(_field "$power" "Maximum Capacity")")
    charge=$(_num "$(_field "$power" "State of Charge (%)")")
    [ -z "$cycles" ] && cycles=0

    sev=$(sev_battery "${condition:-Normal}" "$cycles")
    report_add "$A_BATTERY" "Zustand" "${condition:-unbekannt}" "$sev" \
        "$([ "$sev" != ok ] && printf 'Apple meldet für den Akku den Zustand „%s“.' "${condition:-unbekannt}")" \
        "$([ "$sev" != ok ] && printf 'Akkutausch beim Apple-Service oder einem autorisierten Betrieb einplanen.')"

    report_add "$A_BATTERY" "Ladezyklen" "$cycles" "$(sev_battery Normal "$cycles")" \
        "$([ "$cycles" -gt 1000 ] && printf 'Über 1000 Ladezyklen – der Akku hat seine geplante Lebensdauer erreicht.')"

    if [ -n "$maxcap" ] && [ "$maxcap" -gt 0 ] 2>/dev/null; then
        if [ "$maxcap" -lt 60 ]; then sev=warn; else sev=ok; fi
        report_add "$A_BATTERY" "Maximale Kapazität" "${maxcap} %" "$sev" \
            "$([ "$sev" = warn ] && printf 'Der Akku hält nur noch %s %% der ursprünglichen Ladung.' "$maxcap")"
    fi

    if [ -n "$charge" ]; then
        report_add "$A_BATTERY" "Ladestand" "${charge} %" ok
    fi
}

# --------------------------------------------------------------------------
collect_security() {
    local fv fw sip gk state

    if _have fdesetup; then
        fv=$(fdesetup status 2>/dev/null)
        case "$fv" in
            *"FileVault is On"*) state=on ;;
            *) state=off ;;
        esac
        report_add "$A_SECURITY" "Festplattenverschlüsselung (FileVault)" \
            "$([ "$state" = on ] && printf 'aktiv' || printf 'AUS')" \
            "$(sev_switch_on "$state")" \
            "$([ "$state" = off ] && printf 'Die Festplatte ist unverschlüsselt – bei Verlust oder Diebstahl sind alle Daten lesbar.')" \
            "$([ "$state" = off ] && printf 'Systemeinstellungen > Datenschutz & Sicherheit > FileVault einschalten. Wiederherstellungsschlüssel unbedingt sichern!')"

        # Analog zur Windows-Prüfung: Verschlüsselung ohne gesicherten
        # Schlüssel ist gefährlicher als gar keine Verschlüsselung.
        if [ "$state" = on ]; then
            report_add "$A_SECURITY" "Wiederherstellungsschlüssel" \
                "vor Reparaturen prüfen" info \
                "Ist FileVault aktiv und der Wiederherstellungsschlüssel nicht auffindbar, kommt niemand mehr an die Daten – auch wir nicht." \
                "Beim Kunden nachfragen, ob der Schlüssel (oder das iCloud-Konto zur Entsperrung) verfügbar ist, BEVOR systemnah eingegriffen wird."
        fi
    fi

    if [ -x /usr/libexec/ApplicationFirewall/socketfilterfw ]; then
        fw=$(/usr/libexec/ApplicationFirewall/socketfilterfw --getglobalstate 2>/dev/null)
        case "$fw" in
            *"enabled"*|*"State = 1"*|*"State = 2"*) state=on ;;
            *) state=off ;;
        esac
        report_add "$A_SECURITY" "Firewall" \
            "$([ "$state" = on ] && printf 'aktiv' || printf 'AUS')" \
            "$(sev_switch_on "$state")" \
            "$([ "$state" = off ] && printf 'Die macOS-Firewall ist abgeschaltet.')" \
            "$([ "$state" = off ] && printf 'Systemeinstellungen > Netzwerk > Firewall einschalten.')"
    fi

    if _have csrutil; then
        sip=$(csrutil status 2>/dev/null)
        case "$sip" in
            *enabled*) state=on ;;
            *) state=off ;;
        esac
        report_add "$A_SECURITY" "Systemintegritätsschutz (SIP)" \
            "$([ "$state" = on ] && printf 'aktiv' || printf 'AUS')" \
            "$(sev_switch_on "$state")" \
            "$([ "$state" = off ] && printf 'SIP ist abgeschaltet – Schadsoftware kann Systemdateien verändern. Das ist kein Normalzustand.')" \
            "$([ "$state" = off ] && printf 'SIP im Wiederherstellungsmodus mit „csrutil enable“ wieder einschalten.')"
    fi

    if _have spctl; then
        gk=$(spctl --status 2>/dev/null)
        case "$gk" in
            *"assessments enabled"*) state=on ;;
            *) state=off ;;
        esac
        report_add "$A_SECURITY" "Gatekeeper (App-Prüfung)" \
            "$([ "$state" = on ] && printf 'aktiv' || printf 'AUS')" \
            "$(sev_switch_on "$state")" \
            "$([ "$state" = off ] && printf 'Gatekeeper ist aus – Programme aus unbekannter Quelle starten ungeprüft.')" \
            "$([ "$state" = off ] && printf 'Mit „sudo spctl --master-enable“ wieder einschalten.')"
    fi
}

# --------------------------------------------------------------------------
# Schadsoftware: macOS hat kein Virenschutz-Programm im klassischen Sinn.
# Wir prüfen deshalb das, was tatsächlich prüfbar ist - und behaupten nichts
# darüber hinaus.
collect_malware() {
    local xprotect ver mrt count profiles_out ext

    xprotect="/Library/Apple/System/Library/CoreServices/XProtect.bundle/Contents/Info.plist"
    [ -f "$xprotect" ] || xprotect="/System/Library/CoreServices/XProtect.bundle/Contents/Info.plist"
    if [ -f "$xprotect" ]; then
        ver=$(defaults read "${xprotect%.plist}" CFBundleShortVersionString 2>/dev/null)
        report_add "$A_MALWARE" "XProtect (Apple-Schutz)" "Version ${ver:-unbekannt}" ok \
            "XProtect ist Apples eingebaute Erkennung bekannter Schadsoftware. Sie arbeitet im Hintergrund und wird automatisch aktualisiert."
    fi

    if _have mdfind; then
        mrt=$(system_profiler SPInstallHistoryDataType 2>/dev/null | grep -c "MRTConfigData" 2>/dev/null)
        [ -n "$mrt" ] && [ "$mrt" -gt 0 ] 2>/dev/null && \
            report_add "$A_MALWARE" "Malware-Removal-Tool" "$mrt Aktualisierung(en) installiert" ok
    fi

    # Konfigurationsprofile sind der häufigste Weg, über den Adware auf dem
    # Mac Browser-Einstellungen kapert.
    if _have profiles; then
        profiles_out=$(profiles -P 2>/dev/null)
        count=$(printf '%s' "$profiles_out" | grep -c "attribute: name" 2>/dev/null)
        [ -z "$count" ] && count=0
        if [ "$count" -gt 0 ]; then
            report_add "$A_MALWARE" "Konfigurationsprofile" "$count Profil(e) installiert" warn \
                "Konfigurationsprofile können Startseite, Suchmaschine und Proxy erzwingen. Auf Privatgeräten ohne Firmenverwaltung gehören dort keine hin." \
                "Systemeinstellungen > Allgemein > Geräteverwaltung öffnen und unbekannte Profile entfernen."
        else
            report_add "$A_MALWARE" "Konfigurationsprofile" "keine" ok
        fi
    fi

    # Fremde Kernel-/Systemerweiterungen.
    if _have kmutil; then
        ext=$(kmutil showloaded --list-only 2>/dev/null | grep -vc "com.apple" 2>/dev/null)
        [ -z "$ext" ] && ext=0
        if [ "$ext" -gt 0 ]; then
            report_add "$A_MALWARE" "Fremde Kernel-Erweiterungen" "$ext geladen" info \
                "$(kmutil showloaded --list-only 2>/dev/null | grep -v 'com.apple' | awk '{print $6}' | tr '\n' ' ')" \
                "Kernel-Erweiterungen greifen tief ins System ein. Unbekannte Einträge prüfen – sie sind eine häufige Absturzursache."
        else
            report_add "$A_MALWARE" "Fremde Kernel-Erweiterungen" "keine" ok
        fi
    fi

    report_add "$A_MALWARE" "Hinweis" "keine Tiefen-Entfernung durch dieses Werkzeug" info \
        "Dieses Werkzeug erkennt auffällige Startobjekte und Profile, ersetzt aber keinen Malware-Scanner." \
        "Bei konkretem Verdacht Malwarebytes für Mac (kostenlose Prüfung) einsetzen."
}

# --------------------------------------------------------------------------
collect_updates() {
    local out count

    if [ "${SKIP_UPDATES:-0}" = "1" ]; then
        report_add "$A_UPDATE" "Status" "übersprungen (--skip-updates)" info
        return
    fi
    if ! _have softwareupdate; then
        report_add "$A_UPDATE" "Status" "nicht prüfbar" info
        return
    fi

    out=$(softwareupdate -l 2>&1)
    case "$out" in
        *"No new software available"*)
            report_add "$A_UPDATE" "Ausstehende Updates" "keine" ok
            return ;;
    esac

    count=$(printf '%s\n' "$out" | grep -c '^\* Label:' 2>/dev/null)
    [ -z "$count" ] && count=0
    if [ "$count" -eq 0 ]; then
        count=$(printf '%s\n' "$out" | grep -c '^\s*\*' 2>/dev/null)
        [ -z "$count" ] && count=0
    fi

    report_add "$A_UPDATE" "Ausstehende Updates" "$count" "$(sev_updates "$count")" \
        "$([ "$count" -gt 0 ] && printf '%s Aktualisierung(en) sind noch nicht installiert.' "$count")" \
        "$([ "$count" -gt 0 ] && printf 'Systemeinstellungen > Allgemein > Softwareupdate ausführen. Sicherheitsupdates schließen bekannte Lücken.')"
}

# --------------------------------------------------------------------------
collect_autostart() {
    local dir count total=0 names="" sev

    for dir in "$HOME/Library/LaunchAgents" /Library/LaunchAgents /Library/LaunchDaemons; do
        [ -d "$dir" ] || continue
        count=$(find "$dir" -maxdepth 1 -name '*.plist' 2>/dev/null | wc -l | tr -d ' ')
        [ -z "$count" ] && count=0
        total=$((total + count))
        report_add "$A_AUTOSTART" "$dir" "$count Eintrag/Einträge" ok
        names="$names $(find "$dir" -maxdepth 1 -name '*.plist' -exec basename {} .plist \; 2>/dev/null | tr '\n' ' ')"
    done

    if [ "$total" -gt 15 ]; then sev=warn; else sev=ok; fi
    report_add "$A_AUTOSTART" "Startobjekte gesamt" "$total" "$sev" \
        "$(printf '%s' "$names" | cut -c1-400)" \
        "$([ "$sev" = warn ] && printf 'Viele Startobjekte verlängern den Anmeldevorgang. Nicht benötigte unter „Systemeinstellungen > Allgemein > Anmeldeobjekte“ abschalten.')"
}

# --------------------------------------------------------------------------
# Absturzberichte gruppieren - dieselbe Logik wie AppCrashAnalyzer der
# Windows-App: nicht die Einzelmeldung zählt, sondern welches Programm
# wiederholt abstürzt.
collect_crashes() {
    local dir files crashes=0 panics=0 sev top

    files=""
    for dir in "$HOME/Library/Logs/DiagnosticReports" /Library/Logs/DiagnosticReports; do
        [ -d "$dir" ] || continue
        files="$files
$(find "$dir" -maxdepth 1 -type f \( -name '*.ips' -o -name '*.crash' -o -name '*.panic' \) -mtime -30 2>/dev/null)"
    done

    # Kernel Panics liegen je nach macOS-Version als *.panic oder als *.ips mit
    # "panic" im Namen vor – deshalb erst die Panics abgreifen und den Rest als
    # gewöhnliche Programmabstürze zählen.
    panics=$(printf '%s\n' "$files" | grep -i 'panic' | grep -c '' 2>/dev/null)
    crashes=$(printf '%s\n' "$files" | grep -vi 'panic' | grep -c '\.\(ips\|crash\)$' 2>/dev/null)
    [ -z "$crashes" ] && crashes=0
    [ -z "$panics" ] && panics=0

    sev=$(sev_crash_count "$crashes")
    top=$(printf '%s\n' "$files" | sed -n 's|.*/||p' | sed 's/-[0-9]\{4\}-[0-9]\{2\}-[0-9]\{2\}.*//' \
        | grep -v '^$' | sort | uniq -c | sort -rn | head -3 \
        | awk '{ n=$1; $1=""; sub(/^ /,""); printf "%s (%sx) ", $0, n }')

    report_add "$A_CRASH" "Programmabstürze (30 Tage)" "$crashes" "$sev" \
        "$([ -n "$top" ] && printf 'Am häufigsten: %s' "$top")" \
        "$([ "$sev" != ok ] && printf 'Das am häufigsten betroffene Programm zuerst aktualisieren oder neu installieren.')"

    sev=$(sev_panic_count "$panics")
    report_add "$A_CRASH" "Kernel Panics (30 Tage)" "$panics" "$sev" \
        "$([ "$panics" -gt 0 ] && printf 'Ein Kernel Panic ist ein vollständiger Systemabsturz – meist Hardware, Speicher oder eine fremde Systemerweiterung.')" \
        "$([ "$panics" -gt 0 ] && printf 'Fremde Kernel-Erweiterungen entfernen, Apple-Hardwaretest ausführen (Einschalten + D), Arbeitsspeicher prüfen.')"
}

# --------------------------------------------------------------------------
collect_network() {
    local iface ip gw dns ping_ok

    iface=$(route -n get default 2>/dev/null | awk '/interface:/{print $2}')
    if [ -n "$iface" ]; then
        ip=$(ipconfig getifaddr "$iface" 2>/dev/null)
        gw=$(route -n get default 2>/dev/null | awk '/gateway:/{print $2}')
        report_add "$A_NETWORK" "Aktive Verbindung" "$iface${ip:+ · $ip}" ok \
            "${gw:+Router: $gw}"
    else
        report_add "$A_NETWORK" "Aktive Verbindung" "keine Standardroute gefunden" warn \
            "Der Mac hat aktuell keine funktionierende Netzwerkverbindung." \
            "WLAN/Kabel prüfen, Router neu starten."
    fi

    dns=$(scutil --dns 2>/dev/null | awk '/nameserver\[0\]/{print $3; exit}')
    [ -n "$dns" ] && report_add "$A_NETWORK" "Namensauflösung (DNS)" "$dns" ok

    if ping -c 1 -t 3 1.1.1.1 >/dev/null 2>&1; then ping_ok=1; else ping_ok=0; fi
    if [ "$ping_ok" -eq 1 ]; then
        report_add "$A_NETWORK" "Internet erreichbar" "ja" ok
    else
        report_add "$A_NETWORK" "Internet erreichbar" "nein" warn \
            "1.1.1.1 antwortet nicht – es besteht keine Internetverbindung." \
            "Router und Kabel prüfen. Antwortet die IP-Adresse, aber Namen nicht, liegt es am DNS."
    fi
}

# --------------------------------------------------------------------------
collect_backup() {
    local latest stamp epoch now days sev dest

    if ! _have tmutil; then
        report_add "$A_BACKUP" "Status" "nicht prüfbar" info
        return
    fi

    dest=$(tmutil destinationinfo 2>/dev/null | awk -F': *' '/^Name/{print $2; exit}')
    latest=$(tmutil latestbackup 2>/dev/null)

    if [ -z "$latest" ]; then
        report_add "$A_BACKUP" "Letzte Sicherung" "keine gefunden" "$(sev_backup_age_days -1)" \
            "Es ist keine Time-Machine-Sicherung vorhanden${dest:+ (Ziel: $dest)}. Bei einem Defekt wären alle Daten weg." \
            "Externe Festplatte anschließen und Time Machine einrichten – die wichtigste Einzelmaßnahme überhaupt."
        return
    fi

    stamp=$(printf '%s' "$latest" | sed -n 's|.*/\([0-9]\{4\}-[0-9]\{2\}-[0-9]\{2\}\).*|\1|p')
    if [ -n "$stamp" ]; then
        epoch=$(date -j -f "%Y-%m-%d" "$stamp" +%s 2>/dev/null)
        now=$(date +%s)
        if [ -n "$epoch" ]; then
            days=$(( (now - epoch) / 86400 ))
            sev=$(sev_backup_age_days "$days")
            report_add "$A_BACKUP" "Letzte Sicherung" "$stamp (vor $days Tag(en))" "$sev" \
                "$([ "$sev" = warn ] && printf 'Die letzte Sicherung ist %s Tage alt.' "$days")" \
                "$([ "$sev" != ok ] && printf 'Sicherungsmedium anschließen und „Backup jetzt erstellen“ ausführen.')"
            [ -n "$dest" ] && report_add "$A_BACKUP" "Sicherungsziel" "$dest" ok
            return
        fi
    fi

    report_add "$A_BACKUP" "Letzte Sicherung" "$latest" info
}

# --------------------------------------------------------------------------
# Spotlight: genau das Problem, das beim Windows-Kunden zur Suche ohne
# Eingabemöglichkeit führte, heißt auf dem Mac "Index kaputt".
collect_search() {
    local state

    if ! _have mdutil; then
        report_add "$A_SEARCH" "Status" "nicht prüfbar" info
        return
    fi

    state=$(mdutil -s / 2>/dev/null | tail -1 | sed 's/^[ \t]*//')
    case "$state" in
        *"Indexing enabled"*)
            report_add "$A_SEARCH" "Spotlight-Index" "aktiv" ok ;;
        *"Indexing disabled"*)
            report_add "$A_SEARCH" "Spotlight-Index" "abgeschaltet" warn \
                "Der Suchindex ist deaktiviert – die Spotlight-Suche findet nichts." \
                "Mit „sudo mdutil -i on /“ wieder einschalten." ;;
        *)
            report_add "$A_SEARCH" "Spotlight-Index" "${state:-unbekannt}" info ;;
    esac
}

# --------------------------------------------------------------------------
collect_cache() {
    local browser label size total=0 trash user_cache running

    while IFS= read -r browser; do
        [ -n "$browser" ] || continue
        size=$(cleanup_browser_size "$browser")
        [ "$size" -eq 0 ] && continue
        label=$(cleanup_browser_label "$browser")
        total=$((total + size))
        if cleanup_browser_running "$browser"; then running=" · läuft gerade"; else running=""; fi
        report_add "$A_CACHE" "$label" "$(human_size "$size")${running}" info
    done <<EOF
$(cleanup_all_browsers)
EOF

    user_cache=$(cleanup_user_cache_size)
    trash=$(cleanup_trash_size)

    report_add "$A_CACHE" "Browser-Zwischenspeicher gesamt" "$(human_size "$total")" \
        "$([ "$total" -gt $((3 * 1024 * 1024 * 1024)) ] && printf 'warn' || printf 'info')" \
        "$([ "$total" -gt $((3 * 1024 * 1024 * 1024)) ] && printf 'Über 3 GB nur an Browser-Zwischenspeicher.')" \
        "Mit „--fix-cache“ leeren (Browser vorher beenden)."

    report_add "$A_CACHE" "Benutzer-Zwischenspeicher (~/Library/Caches)" "$(human_size "$user_cache")" info \
        "Enthält auch Daten, die Programme zum Arbeiten brauchen – wird von diesem Werkzeug NICHT gelöscht."
    report_add "$A_CACHE" "Papierkorb" "$(human_size "$trash")" info \
        "" \
        "$([ "$trash" -gt $((1024 * 1024 * 1024)) ] && printf 'Über 1 GB im Papierkorb – nach Rücksprache mit dem Kunden leeren.')"
}
