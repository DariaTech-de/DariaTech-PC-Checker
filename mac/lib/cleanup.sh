#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DariaTech Mac-Check - Aufräum-Funktionen (Browser-Cache, Benutzer-Caches)
#
# Hintergrund: Auf dem Mac liegen Browser-Caches an ganz anderen Stellen als
# unter Windows. Genau deshalb blieb der Chrome-Cache beim ersten Versuch
# stehen. Die Ordnerliste unten deckt die tatsächlichen macOS-Pfade ab.
#
# Sicherheitsregeln (bewusst streng):
#   * Es wird ausschließlich der INHALT bekannter Cache-Ordner gelöscht.
#   * Jeder Pfad muss cleanup_is_safe_target() bestehen - sonst wird er
#     übersprungen, auch wenn er in der Liste steht.
#   * Läuft der Browser noch, wird NICHT gelöscht (der Browser schreibt die
#     Dateien sonst sofort neu und wir melden fälschlich Erfolg).
#   * Nach dem Löschen wird nachgemessen. Bleibt zu viel übrig, gilt der
#     Vorgang als fehlgeschlagen - keine Schönfärberei.
#
# NICHT angefasst werden (das sind Nutzdaten, kein Cache):
#   Local Storage, IndexedDB, Cookies, Login Data, Sessions, Bookmarks,
#   Extension-Einstellungen, Mail, Fotos.
# ---------------------------------------------------------------------------

# Ordnernamen, deren Inhalt bei Chromium-Browsern gefahrlos löschbar ist.
cleanup_cache_folder_names() {
    cat <<'EOF'
Cache
Cache_Data
Code Cache
GPUCache
DawnCache
DawnGraphiteCache
DawnWebGPUCache
GraphiteDawnCache
ShaderCache
GrShaderCache
Media Cache
Application Cache
Service Worker/CacheStorage
Service Worker/ScriptCache
component_crx_cache
extensions_crx_cache
EOF
}

# Prüft, ob ein Pfad gelöscht werden darf. Absichtlich restriktiv:
# nur absolute Pfade unterhalb von $HOME oder /Library, mindestens vier
# Ebenen tief, und der Pfad muss einen Cache-Bezug im Namen tragen.
# Rückgabe 0 = erlaubt, 1 = verboten.
cleanup_is_safe_target() {
    local path="${1:-}"
    local home="${2:-$HOME}"

    case "$path" in
        /*) ;;
        *) return 1 ;;                       # nur absolute Pfade
    esac
    case "$path" in
        *..*) return 1 ;;                    # keine Pfad-Tricks
        */) return 1 ;;                      # kein abschließender Schrägstrich
    esac
    [ "$path" = "$home" ] && return 1
    [ "$path" = "/" ] && return 1

    # Muss unter $HOME oder /Library liegen.
    case "$path" in
        "$home"/*) ;;
        /Library/*) ;;
        *) return 1 ;;
    esac

    # Diese Bereiche sind tabu, auch wenn "Cache" im Namen steht.
    case "$path" in
        "$home"/Documents/*|"$home"/Desktop/*|"$home"/Pictures/*|"$home"/Movies/*|"$home"/Music/*) return 1 ;;
        *"/Local Storage"*|*"/IndexedDB"*|*"/Cookies"*|*"/Login Data"*|*"/Sessions"*) return 1 ;;
        */Keychains/*|*/Mail/*|*/Photos*) return 1 ;;
    esac

    # Mindesttiefe 4 (z. B. /Users/max/Library/Caches).
    local depth
    depth=$(printf '%s' "$path" | awk -F/ '{print NF-1}')
    [ "$depth" -lt 4 ] && return 1

    # Der Pfad muss erkennbar ein Cache sein.
    case "$path" in
        *[Cc]ache*) return 0 ;;
        *) return 1 ;;
    esac
}

# Grösse eines Ordners in Bytes (0, wenn nicht vorhanden).
cleanup_dir_size() {
    local path="${1:-}"
    [ -d "$path" ] || { printf '0'; return; }
    local kb
    kb=$(du -sk "$path" 2>/dev/null | awk '{print $1}')
    [ -z "$kb" ] && kb=0
    printf '%s' $((kb * 1024))
}

# Löscht den INHALT eines Ordners (der Ordner selbst bleibt stehen).
# Gibt die freigewordenen Bytes aus.
cleanup_purge_dir() {
    local path="${1:-}"
    cleanup_is_safe_target "$path" || { printf '0'; return 1; }
    [ -d "$path" ] || { printf '0'; return 0; }

    local before after
    before=$(cleanup_dir_size "$path")
    find "$path" -mindepth 1 -maxdepth 1 -exec rm -rf {} + 2>/dev/null
    after=$(cleanup_dir_size "$path")
    local freed=$((before - after))
    [ "$freed" -lt 0 ] && freed=0
    printf '%s' "$freed"
}

# --------------------------------------------------------------------------
# Browser
# --------------------------------------------------------------------------

# Prozessnamen der unterstützten Browser (für die Laufend-Prüfung).
cleanup_browser_process() {
    case "$1" in
        chrome)  printf 'Google Chrome' ;;
        edge)    printf 'Microsoft Edge' ;;
        brave)   printf 'Brave Browser' ;;
        vivaldi) printf 'Vivaldi' ;;
        opera)   printf 'Opera' ;;
        firefox) printf 'firefox' ;;
        safari)  printf 'Safari' ;;
        *)       printf '' ;;
    esac
}

cleanup_browser_label() {
    case "$1" in
        chrome)  printf 'Google Chrome' ;;
        edge)    printf 'Microsoft Edge' ;;
        brave)   printf 'Brave' ;;
        vivaldi) printf 'Vivaldi' ;;
        opera)   printf 'Opera' ;;
        firefox) printf 'Mozilla Firefox' ;;
        safari)  printf 'Safari' ;;
        *)       printf '%s' "$1" ;;
    esac
}

# Basisordner je Browser: erst der Cache-Zweig (~/Library/Caches), dann der
# Datenzweig (~/Library/Application Support) - beide enthalten Cache-Ordner.
cleanup_browser_roots() {
    local browser="$1" home="${2:-$HOME}"
    case "$browser" in
        chrome)
            printf '%s\n' "$home/Library/Caches/Google/Chrome" \
                          "$home/Library/Application Support/Google/Chrome" ;;
        edge)
            printf '%s\n' "$home/Library/Caches/Microsoft Edge" \
                          "$home/Library/Application Support/Microsoft Edge" ;;
        brave)
            printf '%s\n' "$home/Library/Caches/BraveSoftware/Brave-Browser" \
                          "$home/Library/Application Support/BraveSoftware/Brave-Browser" ;;
        vivaldi)
            printf '%s\n' "$home/Library/Caches/Vivaldi" \
                          "$home/Library/Application Support/Vivaldi" ;;
        opera)
            printf '%s\n' "$home/Library/Caches/com.operasoftware.Opera" \
                          "$home/Library/Application Support/com.operasoftware.Opera" ;;
        firefox)
            printf '%s\n' "$home/Library/Caches/Firefox/Profiles" ;;
        safari)
            printf '%s\n' "$home/Library/Caches/com.apple.Safari" \
                          "$home/Library/Containers/com.apple.Safari/Data/Library/Caches" ;;
    esac
}

# Alle konkreten Cache-Ordner eines Browsers auflisten (nur vorhandene).
cleanup_browser_dirs() {
    local browser="$1" home="${2:-$HOME}"
    local root name

    while IFS= read -r root; do
        [ -d "$root" ] || continue
        case "$browser" in
            firefox)
                # ~/Library/Caches/Firefox/Profiles/<profil>/cache2 + startupCache
                find "$root" -mindepth 2 -maxdepth 2 -type d \
                    \( -name 'cache2' -o -name 'startupCache' -o -name 'OfflineCache' \) 2>/dev/null
                ;;
            safari)
                # Der gesamte Safari-Cache-Container darf geleert werden.
                printf '%s\n' "$root"
                ;;
            *)
                while IFS= read -r name; do
                    [ -n "$name" ] || continue
                    find "$root" -mindepth 1 -maxdepth 4 -type d -path "*/$name" 2>/dev/null
                done <<EOF
$(cleanup_cache_folder_names)
EOF
                ;;
        esac
    done <<EOF
$(cleanup_browser_roots "$browser" "$home")
EOF
}

# Läuft der Browser gerade? 0 = ja.
cleanup_browser_running() {
    local proc
    proc=$(cleanup_browser_process "$1")
    [ -n "$proc" ] || return 1
    pgrep -x "$proc" >/dev/null 2>&1
}

# Belegter Cache-Platz eines Browsers in Bytes.
cleanup_browser_size() {
    local browser="$1" home="${2:-$HOME}" total=0 dir size
    while IFS= read -r dir; do
        [ -n "$dir" ] || continue
        size=$(cleanup_dir_size "$dir")
        total=$((total + size))
    done <<EOF
$(cleanup_browser_dirs "$browser" "$home")
EOF
    printf '%s' "$total"
}

# Cache eines Browsers leeren. Gibt eine Statuszeile aus und liefert
# 0 = erfolgreich, 1 = übersprungen/fehlgeschlagen.
cleanup_browser() {
    local browser="$1" home="${2:-$HOME}"
    local label freed=0 dir gained before after skipped=0

    label=$(cleanup_browser_label "$browser")
    before=$(cleanup_browser_size "$browser" "$home")

    if [ "$before" -eq 0 ]; then
        printf '%s: kein Cache gefunden (nicht installiert oder bereits leer)\n' "$label"
        return 0
    fi

    if cleanup_browser_running "$browser"; then
        printf '%s: läuft noch – Cache (%s) NICHT gelöscht. Bitte Browser beenden.\n' \
            "$label" "$(human_size "$before")"
        return 1
    fi

    while IFS= read -r dir; do
        [ -n "$dir" ] || continue
        if ! cleanup_is_safe_target "$dir" "$home"; then
            skipped=$((skipped + 1))
            continue
        fi
        gained=$(cleanup_purge_dir "$dir")
        freed=$((freed + gained))
    done <<EOF
$(cleanup_browser_dirs "$browser" "$home")
EOF

    after=$(cleanup_browser_size "$browser" "$home")

    # Ehrliche Erfolgskontrolle: Wenn mehr als 10 MB übrig sind, hat es nicht
    # funktioniert - dann sagen wir das auch.
    if [ "$after" -gt $((10 * 1024 * 1024)) ]; then
        printf '%s: nur teilweise geleert – %s frei, %s bleiben belegt.\n' \
            "$label" "$(human_size "$freed")" "$(human_size "$after")"
        return 1
    fi

    if [ "$skipped" -gt 0 ]; then
        printf '%s: %s freigegeben (%s Ordner aus Sicherheitsgründen ausgelassen).\n' \
            "$label" "$(human_size "$freed")" "$skipped"
    else
        printf '%s: %s freigegeben.\n' "$label" "$(human_size "$freed")"
    fi
    return 0
}

cleanup_all_browsers() {
    printf '%s\n' chrome edge brave vivaldi opera firefox safari
}

# --------------------------------------------------------------------------
# Sonstige Ablagen
# --------------------------------------------------------------------------

# Benutzer-Caches ausserhalb der Browser (Xcode, Spotify, Slack ...).
cleanup_user_cache_size() {
    local home="${1:-$HOME}"
    cleanup_dir_size "$home/Library/Caches"
}

cleanup_trash_size() {
    local home="${1:-$HOME}"
    cleanup_dir_size "$home/.Trash"
}
