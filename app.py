#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GreenLuma Modern Web UI & Game Manager
Backend server with full customization, DLC management, settings persistence, and stealth session orchestration.
"""

import os
import sys
import json
import time
import shutil
import urllib.request
import urllib.parse
import threading
import subprocess
from pathlib import Path
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler

try:
    import winreg
except ImportError:
    winreg = None

try:
    import psutil
except ImportError:
    psutil = None

if getattr(sys, 'frozen', False):
    BASE_DIR = Path(sys.executable).resolve().parent
    BUNDLE_DIR = Path(getattr(sys, '_MEIPASS', BASE_DIR))
    STATIC_DIR = BUNDLE_DIR / "static" if (BUNDLE_DIR / "static").exists() else BASE_DIR / "static"
else:
    BASE_DIR = Path(__file__).resolve().parent
    STATIC_DIR = BASE_DIR / "static"

GAMES_FILE = BASE_DIR / "games.json"
SETTINGS_FILE = BASE_DIR / "settings.json"
APP_LIST_DIR = BASE_DIR / "AppList"
STEALTH_DIR = BASE_DIR / "StealthMode"

PORT = 58421
SESSION_STATE = {
    "injected": False,
    "active_mode": "standard",
    "monitor_thread": None,
    "stop_requested": False,
    "cleaned_after_login": False
}

DEFAULT_SETTINGS = {
    "customSteamPath": "",
    "mode": "standard",            # "standard" | "families"
    "cleanTiming": "on_exit",       # "on_exit" | "after_login" | "manual"
    "clearAppCache": False,
    "steamArgs": "-inhibitbootstrap",
    "theme": "emerald",            # "emerald" | "cyan" | "purple" | "gold" | "crimson"
    "viewMode": "grid",            # "grid" | "compact"
    "lang": "en",                  # "en" | "ar"
    "autoKillSteam": True
}

DEFAULT_SLOTS = [
    90, 205, 219, 310, 410, 570, 575, 635, 640, 740, 1213, 1273, 1840, 2145, 2403, 4270, 4940, 
    8680, 8710, 8730, 8770, 13180, 17505, 17515, 17525, 17535, 17555, 17575, 17585, 18010, 18030, 
    22150, 34120, 41005, 41015, 41040, 41080, 42300, 42320, 42750, 43210, 55280, 63220, 70010, 
    72310, 72780, 91720, 96810, 111710, 203300, 203600, 208050, 212542, 215350, 215360, 216280, 
    216840, 220070, 221410, 222840, 223160, 223240, 223250, 223350, 223910, 224620, 229950, 230030, 
    231390, 233780, 236600, 236650, 237410, 238670, 238690, 255470, 258680, 261020, 261140, 261310, 
    265360, 266910, 294420, 302530, 302550, 312070, 313250, 315420, 316000, 319070, 320420, 321770, 
    322050, 323010, 332850, 366490, 373300, 374980, 381690, 382030, 401530, 405270, 407350, 443030, 
    476580, 551410, 568880, 613220, 733580, 807210, 858280, 875860, 944490, 961940, 1042420, 1054830, 
    1070560, 1070910, 1113280, 1161040, 1182480, 1245040, 1391110, 1420170, 1493710, 1580130, 1628350, 
    1635560, 1826330, 1874900, 1887720, 1977700, 2180100, 2230260, 2348590, 2676230, 2738040, 2805730, 
    3029110, 3043620, 3086180, 3127680, 3340990, 3658110, 4183110, 4185400, 4333400, 4427310, 4628710, 
    4628740, 4690330, 4862110
]

def load_settings():
    """Load settings from settings.json with default fallback."""
    if not SETTINGS_FILE.exists():
        save_settings(DEFAULT_SETTINGS)
        return dict(DEFAULT_SETTINGS)
    try:
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
            merged = dict(DEFAULT_SETTINGS)
            merged.update(data)
            return merged
    except Exception:
        return dict(DEFAULT_SETTINGS)

def save_settings(settings):
    """Save settings to settings.json."""
    with open(SETTINGS_FILE, "w", encoding="utf-8") as f:
        json.dump(settings, f, indent=2, ensure_ascii=False)

def get_steam_path():
    """Detect Steam installation directory from Settings, Registry or standard paths."""
    cfg = load_settings()
    custom = cfg.get("customSteamPath", "").strip()
    if custom and os.path.exists(os.path.join(custom, "steam.exe")):
        return os.path.normpath(custom)

    candidates = []
    if winreg:
        try:
            k = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam")
            val, _ = winreg.QueryValueEx(k, "SteamPath")
            candidates.append(os.path.normpath(val))
        except OSError:
            pass
        try:
            k = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam")
            val, _ = winreg.QueryValueEx(k, "InstallPath")
            candidates.append(os.path.normpath(val))
        except OSError:
            pass

    candidates.extend([
        r"E:\steam",
        r"C:\Program Files (x86)\Steam",
        r"D:\Steam",
        r"C:\Steam"
    ])

    for p in candidates:
        if p and os.path.exists(os.path.join(p, "steam.exe")):
            return p
    return None

def is_steam_running():
    """Check if steam.exe is currently active."""
    if psutil:
        for proc in psutil.process_iter(['name']):
            try:
                if proc.info['name'] and proc.info['name'].lower() == 'steam.exe':
                    return True
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
        return False
    else:
        try:
            out = subprocess.check_output('tasklist /FI "IMAGENAME eq steam.exe" /NH', shell=True).decode()
            return 'steam.exe' in out.lower()
        except Exception:
            return False

def stop_steam(steam_dir=None):
    """Gracefully request Steam shutdown, force kill after timeout."""
    if not is_steam_running():
        return True

    if steam_dir:
        steam_exe = os.path.join(steam_dir, "steam.exe")
        if os.path.exists(steam_exe):
            try:
                subprocess.run([steam_exe, "-shutdown"], timeout=4, capture_output=True)
            except Exception:
                pass

    for _ in range(8):
        if not is_steam_running():
            return True
        time.sleep(0.5)

    if psutil:
        for proc in psutil.process_iter(['name']):
            try:
                if proc.info['name'] and proc.info['name'].lower() == 'steam.exe':
                    proc.kill()
            except Exception:
                pass
    else:
        try:
            subprocess.run("taskkill /F /IM steam.exe", shell=True, capture_output=True)
        except Exception:
            pass

    time.sleep(1)
    return not is_steam_running()

def load_games():
    """Load games list from games.json."""
    if not GAMES_FILE.exists():
        initial = [
            {
                "appId": 1091500,
                "name": "Cyberpunk 2077",
                "image": "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1091500/header.jpg",
                "enabled": True,
                "type": "game",
                "dlcCount": 3
            },
            {
                "appId": 1245620,
                "name": "ELDEN RING",
                "image": "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1245620/header.jpg",
                "enabled": True,
                "type": "game",
                "dlcCount": 1
            },
            {
                "appId": 2358720,
                "name": "Black Myth: Wukong",
                "image": "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2358720/header.jpg",
                "enabled": True,
                "type": "game",
                "dlcCount": 0
            }
        ]
        save_games(initial)
        return initial

    try:
        with open(GAMES_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return []

def save_games(games_list):
    """Save games list to games.json and sync AppList.ini."""
    with open(GAMES_FILE, "w", encoding="utf-8") as f:
        json.dump(games_list, f, indent=2, ensure_ascii=False)
    sync_app_list_ini(games_list)

def sync_app_list_ini(games_list=None):
    """Generate and write AppList.ini based on enabled games."""
    if games_list is None:
        games_list = load_games()

    enabled_ids = [g["appId"] for g in games_list if g.get("enabled", True)]

    lines = [
        "[AppList]",
        "# Format:",
        "# Old AppID = New AppID to unlock",
        "# Remove the # before the old AppID",
        ""
    ]

    used_count = 0
    total_slots = len(DEFAULT_SLOTS)

    # Fill default slots
    for i, slot in enumerate(DEFAULT_SLOTS):
        if i < len(enabled_ids):
            lines.append(f"{slot} = {enabled_ids[i]}")
            used_count += 1
        else:
            lines.append(f"#{slot} = ")

    # Overflow games beyond default slots
    if len(enabled_ids) > total_slots:
        for extra_id in enabled_ids[total_slots:]:
            lines.append(f"{extra_id} = {extra_id}")

    ini_content = "\r\n".join(lines) + "\r\n"

    # Write to local AppList folder
    APP_LIST_DIR.mkdir(parents=True, exist_ok=True)
    with open(APP_LIST_DIR / "AppList.ini", "w", encoding="utf-8") as f:
        f.write(ini_content)

    # Write to StealthMode/AppList folder
    (STEALTH_DIR / "AppList").mkdir(parents=True, exist_ok=True)
    with open(STEALTH_DIR / "AppList" / "AppList.ini", "w", encoding="utf-8") as f:
        f.write(ini_content)

    # If Steam folder is currently injected, update it in place as well
    steam_path = get_steam_path()
    if steam_path and os.path.exists(os.path.join(steam_path, "AppList")):
        steam_app_list = os.path.join(steam_path, "AppList", "AppList.ini")
        try:
            with open(steam_app_list, "w", encoding="utf-8") as f:
                f.write(ini_content)
        except Exception:
            pass

def fetch_steam_game_details(app_id):
    """Fetch official title, DLC list, and capsule image from Steam API."""
    url = f"https://store.steampowered.com/api/appdetails?appids={app_id}&l=arabic"
    header_img = f"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{app_id}/header.jpg"
    try:
        req = urllib.request.Request(
            url,
            headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
        )
        with urllib.request.urlopen(req, timeout=4) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            if str(app_id) in data and data[str(app_id)].get("success"):
                game_data = data[str(app_id)]["data"]
                dlc_ids = game_data.get("dlc", [])
                return {
                    "appId": int(app_id),
                    "name": game_data.get("name", f"App {app_id}"),
                    "image": game_data.get("header_image", header_img),
                    "type": game_data.get("type", "game"),
                    "dlcs": dlc_ids,
                    "dlcCount": len(dlc_ids)
                }
    except Exception:
        pass

    # Fallback to English lookup
    url_en = f"https://store.steampowered.com/api/appdetails?appids={app_id}&l=english"
    try:
        req = urllib.request.Request(
            url_en,
            headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
        )
        with urllib.request.urlopen(req, timeout=4) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            if str(app_id) in data and data[str(app_id)].get("success"):
                game_data = data[str(app_id)]["data"]
                dlc_ids = game_data.get("dlc", [])
                return {
                    "appId": int(app_id),
                    "name": game_data.get("name", f"App {app_id}"),
                    "image": game_data.get("header_image", header_img),
                    "type": game_data.get("type", "game"),
                    "dlcs": dlc_ids,
                    "dlcCount": len(dlc_ids)
                }
    except Exception:
        pass

    return {
        "appId": int(app_id),
        "name": f"App ID {app_id}",
        "image": header_img,
        "type": "game",
        "dlcs": [],
        "dlcCount": 0
    }

def search_steam_store(term):
    """Search Steam Store by game title for autocomplete."""
    encoded_term = urllib.parse.quote(term)
    url = f"https://store.steampowered.com/api/storesearch/?term={encoded_term}&l=english&cc=US"
    results = []
    try:
        req = urllib.request.Request(
            url,
            headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
        )
        with urllib.request.urlopen(req, timeout=4) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            for item in data.get("items", [])[:10]:
                app_id = item.get("id")
                results.append({
                    "appId": app_id,
                    "name": item.get("name"),
                    "image": f"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{app_id}/header.jpg",
                    "price": item.get("price", {}).get("final", 0) / 100.0 if item.get("price") else None
                })
    except Exception:
        pass
    return results

def remove_stealth_files(steam_dir, kill_if_locked=True):
    """Remove user32.dll and AppList directory from Steam folder."""
    user32_file = os.path.join(steam_dir, "user32.dll")
    app_list_folder = os.path.join(steam_dir, "AppList")

    if kill_if_locked and is_steam_running():
        stop_steam(steam_dir)

    for _ in range(5):
        try:
            if os.path.exists(user32_file):
                os.remove(user32_file)
            if os.path.exists(app_list_folder):
                shutil.rmtree(app_list_folder, ignore_errors=True)
            SESSION_STATE["injected"] = False
            return True
        except Exception:
            time.sleep(1)

    return False

def session_monitor_worker(steam_dir, clean_timing="on_exit"):
    """Background thread watching Steam execution and cleaning up based on selected timing."""
    # Wait for Steam to spin up
    for _ in range(25):
        if is_steam_running():
            break
        time.sleep(0.5)

    if clean_timing == "after_login":
        # Wait 15 seconds for Steam to finish loading/login hook
        time.sleep(15)
        # Attempt to delete while Steam continues running (as GreenLuma stealth recommends)
        for _ in range(10):
            try:
                user32_file = os.path.join(steam_dir, "user32.dll")
                app_list_folder = os.path.join(steam_dir, "AppList")
                if os.path.exists(user32_file):
                    os.remove(user32_file)
                if os.path.exists(app_list_folder):
                    shutil.rmtree(app_list_folder, ignore_errors=True)
                SESSION_STATE["cleaned_after_login"] = True
                SESSION_STATE["injected"] = False
                break
            except Exception:
                time.sleep(2)

    # Continue watching until Steam exits
    while True:
        if SESSION_STATE["stop_requested"]:
            break
        if not is_steam_running():
            # Steam closed naturally
            time.sleep(1.5)
            remove_stealth_files(steam_dir, kill_if_locked=False)
            break
        time.sleep(1.0)

class RequestHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(STATIC_DIR), **kwargs)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path
        query = urllib.parse.parse_qs(parsed.query)

        if path == "/api/status":
            steam_dir = get_steam_path()
            steam_running = is_steam_running()
            user32_exists = bool(steam_dir and os.path.exists(os.path.join(steam_dir, "user32.dll")))
            app_list_exists = bool(steam_dir and os.path.exists(os.path.join(steam_dir, "AppList")))
            is_injected = user32_exists or app_list_exists or SESSION_STATE["injected"]
            games = load_games()
            enabled_count = sum(1 for g in games if g.get("enabled", True))
            cfg = load_settings()

            self.send_json({
                "steamDir": steam_dir,
                "steamRunning": steam_running,
                "injected": is_injected,
                "activeMode": SESSION_STATE["active_mode"],
                "totalGames": len(games),
                "enabledGames": enabled_count,
                "cleanTiming": cfg.get("cleanTiming", "on_exit"),
                "theme": cfg.get("theme", "emerald"),
                "viewMode": cfg.get("viewMode", "grid"),
                "lang": cfg.get("lang", "ar")
            })
            return

        elif path == "/api/settings":
            self.send_json(load_settings())
            return

        elif path == "/api/games":
            games = load_games()
            self.send_json(games)
            return

        elif path == "/api/applist/raw":
            sync_app_list_ini()
            ini_file = APP_LIST_DIR / "AppList.ini"
            content = ini_file.read_text(encoding="utf-8") if ini_file.exists() else ""
            self.send_json({"content": content})
            return

        elif path == "/api/search":
            term = query.get("q", [""])[0]
            if len(term.strip()) >= 2:
                results = search_steam_store(term.strip())
                self.send_json(results)
            else:
                self.send_json([])
            return

        elif path == "/api/games/dlcs":
            app_id = query.get("appId", [""])[0]
            if app_id:
                details = fetch_steam_game_details(app_id)
                self.send_json({
                    "appId": int(app_id),
                    "name": details.get("name"),
                    "dlcs": details.get("dlcs", []),
                    "count": len(details.get("dlcs", []))
                })
            else:
                self.send_json({"error": "Missing appId"}, status=400)
            return

        return super().do_GET()

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        path = parsed.path
        content_len = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_len).decode("utf-8") if content_len > 0 else "{}"
        try:
            payload = json.loads(body)
        except Exception:
            payload = {}

        if path == "/api/settings":
            current = load_settings()
            current.update(payload)
            save_settings(current)
            self.send_json({"success": True, "settings": current})
            return

        elif path == "/api/games/add":
            app_id_raw = payload.get("appId")
            custom_name = payload.get("customName", "").strip()

            try:
                app_id = int(str(app_id_raw).strip())
                if app_id <= 0:
                    raise ValueError()
            except Exception:
                self.send_json({"error": "Invalid AppID. Must be a positive integer."}, status=400)
                return

            games = load_games()
            if any(g["appId"] == app_id for g in games):
                self.send_json({"error": "Game already in library."}, status=400)
                return

            details = fetch_steam_game_details(app_id)
            if custom_name:
                details["name"] = custom_name
            details["enabled"] = True
            details["addedAt"] = int(time.time())

            games.insert(0, details)
            save_games(games)

            self.send_json({"success": True, "game": details})
            return

        elif path == "/api/games/bulk":
            raw_text = payload.get("raw", "")
            # Extract all integers
            import re
            found_ids = [int(x) for x in re.findall(r"\b\d{2,8}\b", raw_text)]
            unique_ids = list(dict.fromkeys(found_ids))

            games = load_games()
            existing_ids = {g["appId"] for g in games}
            added = []

            for aid in unique_ids:
                if aid not in existing_ids:
                    details = fetch_steam_game_details(aid)
                    details["enabled"] = True
                    details["addedAt"] = int(time.time())
                    games.insert(0, details)
                    existing_ids.add(aid)
                    added.append(details)

            if added:
                save_games(games)

            self.send_json({"success": True, "addedCount": len(added), "totalAdded": added})
            return

        elif path == "/api/games/add-dlcs":
            parent_id = payload.get("appId")
            if not parent_id:
                self.send_json({"error": "Missing parent appId"}, status=400)
                return

            details = fetch_steam_game_details(parent_id)
            dlc_ids = details.get("dlcs", [])
            games = load_games()
            existing_ids = {g["appId"] for g in games}
            added = []

            for dlc_id in dlc_ids:
                if dlc_id not in existing_ids:
                    dlc_details = fetch_steam_game_details(dlc_id)
                    dlc_details["enabled"] = True
                    dlc_details["isDLC"] = True
                    dlc_details["parentAppId"] = int(parent_id)
                    dlc_details["addedAt"] = int(time.time())
                    games.insert(0, dlc_details)
                    existing_ids.add(dlc_id)
                    added.append(dlc_details)

            if added:
                save_games(games)

            self.send_json({"success": True, "addedCount": len(added)})
            return

        elif path == "/api/games/toggle":
            app_id = int(payload.get("appId", 0))
            enabled = bool(payload.get("enabled", True))
            games = load_games()
            updated = False
            for g in games:
                if g["appId"] == app_id:
                    g["enabled"] = enabled
                    updated = True
                    break
            if updated:
                save_games(games)
                self.send_json({"success": True})
            else:
                self.send_json({"error": "Game not found"}, status=404)
            return

        elif path == "/api/games/delete":
            app_id = int(payload.get("appId", 0))
            games = load_games()
            games = [g for g in games if g["appId"] != app_id]
            save_games(games)
            self.send_json({"success": True})
            return

        elif path == "/api/games/clear":
            save_games([])
            self.send_json({"success": True})
            return

        elif path == "/api/launch":
            steam_dir = get_steam_path()
            if not steam_dir:
                self.send_json({"error": "Steam installation path not found."}, status=500)
                return

            cfg = load_settings()
            mode = payload.get("mode", cfg.get("mode", "standard"))
            clear_cache = payload.get("clearAppCache", cfg.get("clearAppCache", False))
            clean_timing = payload.get("cleanTiming", cfg.get("cleanTiming", "on_exit"))
            steam_args = payload.get("steamArgs", cfg.get("steamArgs", "-inhibitbootstrap")).strip()

            SESSION_STATE["active_mode"] = mode
            SESSION_STATE["stop_requested"] = False
            SESSION_STATE["cleaned_after_login"] = False

            # Stop Steam if running
            stop_steam(steam_dir)

            # Clear cache if requested
            if clear_cache:
                cache_exe = (BASE_DIR / "DeleteSteamAppCache.exe") if (BASE_DIR / "DeleteSteamAppCache.exe").exists() else (STEALTH_DIR / "DeleteSteamAppCache.exe")
                if cache_exe.exists():
                    try:
                        subprocess.run(str(cache_exe), timeout=5, capture_output=True)
                    except Exception:
                        pass

            # Sync and ensure AppList.ini is generated
            sync_app_list_ini()

            # Choose source DLL (prioritizing root dropped update over StealthMode subfolder)
            dll_name = "user32SF.dll" if mode == "families" else "user32.dll"
            dll_src = (BASE_DIR / dll_name) if (BASE_DIR / dll_name).exists() else (STEALTH_DIR / dll_name)

            if not dll_src.exists():
                self.send_json({"error": f"Required DLL missing: {dll_name}"}, status=500)
                return

            # Copy DLL to Steam
            dest_dll = os.path.join(steam_dir, "user32.dll")
            try:
                shutil.copy2(str(dll_src), dest_dll)
            except Exception as e:
                self.send_json({"error": f"Failed copying user32.dll: {str(e)}"}, status=500)
                return

            # Copy AppList directory to Steam
            dest_app_list = os.path.join(steam_dir, "AppList")
            try:
                if os.path.exists(dest_app_list):
                    shutil.rmtree(dest_app_list, ignore_errors=True)
                shutil.copytree(str(APP_LIST_DIR), dest_app_list)
            except Exception as e:
                self.send_json({"error": f"Failed copying AppList: {str(e)}"}, status=500)
                return

            # Launch Steam with custom arguments
            steam_exe = os.path.join(steam_dir, "steam.exe")
            cmd = [steam_exe]
            if steam_args:
                cmd.extend(steam_args.split())

            try:
                subprocess.Popen(cmd)
            except Exception as e:
                self.send_json({"error": f"Failed launching Steam: {str(e)}"}, status=500)
                return

            SESSION_STATE["injected"] = True

            # Launch monitoring thread
            monitor = threading.Thread(
                target=session_monitor_worker, 
                args=(steam_dir, clean_timing), 
                daemon=True
            )
            SESSION_STATE["monitor_thread"] = monitor
            monitor.start()

            self.send_json({"success": True, "message": "Steam launched with GreenLuma."})
            return

        elif path == "/api/stop":
            steam_dir = get_steam_path()
            if not steam_dir:
                self.send_json({"error": "Steam installation path not found."}, status=500)
                return

            SESSION_STATE["stop_requested"] = True
            stop_steam(steam_dir)
            remove_stealth_files(steam_dir, kill_if_locked=True)
            self.send_json({"success": True, "message": "Steam closed and all stealth files removed."})
            return

        elif path == "/api/clean-all":
            steam_dir = get_steam_path()
            if not steam_dir:
                self.send_json({"error": "Steam directory not found."}, status=500)
                return

            stop_steam(steam_dir)

            artifacts = [
                "user32.dll",
                "AppList",
                "DLLInjector.exe",
                "DLLInjector.ini",
                "GreenLumaSettings_2026.exe",
                "GreenLuma_2026_x64.dll",
                "GreenLuma_2026_x86.dll",
                "GreenLuma_2026.log",
                "GreenLuma2026_Files",
                "NoQuestion.bin"
            ]

            purged = []
            for item in artifacts:
                target = os.path.join(steam_dir, item)
                if os.path.exists(target):
                    try:
                        if os.path.isdir(target):
                            shutil.rmtree(target, ignore_errors=True)
                        else:
                            os.remove(target)
                        purged.append(item)
                    except Exception:
                        pass

            SESSION_STATE["injected"] = False
            self.send_json({"success": True, "purged": purged})
            return

        self.send_error(404, "Not Found")

    def send_json(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

def open_ui_window():
    """Launch Microsoft Edge in dedicated app mode or default browser and track lifetime."""
    time.sleep(0.8)
    url = f"http://127.0.0.1:{PORT}"
    edge_paths = [
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    ]
    app_proc = None
    for p in edge_paths:
        if os.path.exists(p):
            try:
                profile_dir = BASE_DIR / ".app_profile"
                app_proc = subprocess.Popen([
                    p,
                    f"--app={url}",
                    f"--user-data-dir={profile_dir}",
                    "--window-size=1200,850",
                    "--no-first-run",
                    "--no-default-browser-check"
                ])
                break
            except Exception:
                pass

    if not app_proc:
        import webbrowser
        webbrowser.open(url)
        return

    # Wait for the native desktop application window to close
    app_proc.wait()

    # When the desktop window closes, cleanly exit server and cleanup
    steam_path = get_steam_path()
    if steam_path and SESSION_STATE.get("injected"):
        remove_stealth_files(steam_path)
    os._exit(0)

if __name__ == "__main__":
    STATIC_DIR.mkdir(parents=True, exist_ok=True)
    sync_app_list_ini()

    server = ThreadingHTTPServer(("127.0.0.1", PORT), RequestHandler)
    print(f"[*] GreenLuma UI Server running at http://127.0.0.1:{PORT}")
    
    # Auto-open browser/app window
    threading.Thread(target=open_ui_window, daemon=True).start()

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[*] Shutting down server...")
        steam_path = get_steam_path()
        if steam_path and SESSION_STATE["injected"]:
            print("[*] Running safety cleanup of stealth files...")
            remove_stealth_files(steam_path)
        server.server_close()
