"""Parse first-party GDScript with Godot 4.7.

Agent gate after batch .gd edits. Engine parser only — not a linter.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

DEFAULT_GODOT = Path(
    r"D:\Godot\Editor\Godot_v4.7.1-stable_mono_win64"
    r"\Godot_v4.7.1-stable_mono_win64"
    r"\Godot_v4.7.1-stable_mono_win64.exe"
)
CHECK_SCRIPT = "res://addons/gdscript_check/check.gd"
CLASS_CACHE = Path(".godot") / "global_script_class_cache.cfg"
JSON_PREFIX = "GDSCRIPT_CHECK_JSON:"

_RE_SCRIPT_ERROR = re.compile(r"^SCRIPT ERROR:\s*(.*)$")
_RE_AT = re.compile(
    r"^\s+at:\s+.*\((.+\.gd):(\d+)\)\s*$"
)
_RE_FAILED_LOAD = re.compile(
    r'Failed to load script ["\']?(res://[^"\']+\.gd)'
)


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _exe_in_dir(folder: Path) -> Path | None:
    consoles = sorted(folder.glob("*_console.exe"))
    if consoles:
        return consoles[0]
    exes = sorted(p for p in folder.glob("*.exe") if p.is_file())
    if exes:
        return exes[0]
    return None


def _resolve_exe(path: Path) -> Path | None:
    if path.is_file():
        sibling = path.parent / f"{path.stem}_console{path.suffix}"
        if sibling.is_file():
            return sibling
        return path
    if path.is_dir():
        return _exe_in_dir(path)
    return None


def resolve_godot(cli_path: str | None) -> Path:
    if cli_path:
        candidates = [Path(cli_path)]
    elif os.environ.get("GODOT_BIN"):
        candidates = [Path(os.environ["GODOT_BIN"])]
    else:
        candidates = [DEFAULT_GODOT]
        which = shutil.which("godot")
        if which:
            candidates.append(Path(which))

    for raw in candidates:
        resolved = _resolve_exe(raw)
        if resolved is not None:
            return resolved

    sys.stderr.write("Godot not found. Set GODOT_BIN or pass --godot.\n")
    sys.exit(2)


def _rel_from_res(project: Path, res_path: str) -> str:
    raw = res_path.strip()
    if raw.startswith("res://"):
        return raw[len("res://") :]
    posix = raw.replace("\\", "/")
    project_posix = project.as_posix()
    if posix.lower().startswith(project_posix.lower()):
        return posix[len(project_posix) :].lstrip("/")
    return raw


def _to_res(project: Path, raw: str) -> str:
    text = raw.strip().replace("\\", "/")
    if text.startswith("res://"):
        return text
    project_posix = project.as_posix()
    if text.lower().startswith(project_posix.lower()):
        text = text[len(project_posix) :].lstrip("/")
    return "res://" + text.lstrip("/")


def _run_godot(
    godot: Path,
    project: Path,
    args: list[str],
    timeout: float,
) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(
            [str(godot), "--headless", "--path", str(project), *args],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
            cwd=str(project),
        )
    except subprocess.TimeoutExpired:
        sys.stderr.write(
            f"Godot timed out after {timeout:.0f}s: {' '.join(args)}\n"
        )
        sys.exit(2)


def _should_import(project: Path, force: bool, skip: bool) -> bool:
    if skip:
        return False
    if force:
        return True
    return not (project / CLASS_CACHE).is_file()


def _parse_errors(project: Path, text: str) -> list[dict]:
    errors: list[dict] = []
    current_file = ""
    pending_message = ""
    for line in text.splitlines():
        if line.startswith("GDSCRIPT_CHECK_BEGIN "):
            current_file = _rel_from_res(project, line.split(" ", 1)[1])
            pending_message = ""
            continue
        script_err = _RE_SCRIPT_ERROR.match(line)
        if script_err:
            pending_message = script_err.group(1).strip()
            continue
        at = _RE_AT.match(line)
        if at and pending_message:
            errors.append(
                {
                    "file": _rel_from_res(project, at.group(1)),
                    "line": int(at.group(2)),
                    "message": pending_message,
                }
            )
            pending_message = ""
            continue
        failed = _RE_FAILED_LOAD.search(line)
        if failed:
            errors.append(
                {
                    "file": _rel_from_res(project, failed.group(1)),
                    "line": 0,
                    "message": "Failed to load script",
                }
            )
        if pending_message and line.startswith("GDSCRIPT_CHECK_"):
            errors.append(
                {
                    "file": current_file,
                    "line": 0,
                    "message": pending_message,
                }
            )
            pending_message = ""
    if pending_message:
        errors.append(
            {
                "file": current_file,
                "line": 0,
                "message": pending_message,
            }
        )
    return _dedupe(errors)


def _parse_json_trailer(text: str) -> dict | None:
    for line in text.splitlines():
        if line.startswith(JSON_PREFIX):
            return json.loads(line[len(JSON_PREFIX) :])
    return None


def _dedupe(errors: list[dict]) -> list[dict]:
    seen: set[tuple] = set()
    out: list[dict] = []
    for item in errors:
        key = (item["file"], item["line"], item["message"])
        if key in seen:
            continue
        seen.add(key)
        out.append(item)
    detailed = {
        item["file"]
        for item in out
        if item["line"]
        or item["message"]
        not in ("Failed to load script", "Parse or compile failed")
    }
    return [
        item
        for item in out
        if not (
            item["file"] in detailed
            and item["message"]
            in ("Failed to load script", "Parse or compile failed")
        )
    ]


def _merge_failed(project: Path, errors: list[dict], trailer: dict | None) -> list[dict]:
    if not trailer:
        return errors
    have = {item["file"] for item in errors}
    for res_path in trailer.get("failed", []):
        rel = _rel_from_res(project, res_path)
        if rel not in have:
            errors.append(
                {
                    "file": rel,
                    "line": 0,
                    "message": "Parse or compile failed",
                }
            )
    return errors


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Parse-check GDScript files with Godot."
    )
    parser.add_argument("--godot", help="Godot 4.7 executable")
    parser.add_argument(
        "--force-import",
        action="store_true",
        help="Force Godot --import before checking (rebuilds class_name cache)",
    )
    parser.add_argument(
        "--skip-import",
        action="store_true",
        help="Never run --import, even if the class cache is missing",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Print machine-readable JSON instead of text",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Also parse vendor addons under addons/",
    )
    parser.add_argument(
        "--vendor",
        action="store_true",
        help="Include addon folders listed in check.gd _VENDOR",
    )
    parser.add_argument(
        "--root",
        action="append",
        default=[],
        help="Extra res:// or repo-relative dir/file (repeatable)",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=180.0,
        help="Seconds for the check process (default 180)",
    )
    parser.add_argument(
        "--import-timeout",
        type=float,
        default=300.0,
        help="Seconds for --import (default 300)",
    )
    parser.add_argument(
        "paths",
        nargs="*",
        help="Optional dirs/files to check instead of first-party addons",
    )
    args = parser.parse_args()

    project = _repo_root()
    if not (project / "project.godot").is_file():
        sys.stderr.write(f"project.godot missing under {project}\n")
        sys.exit(2)

    godot = resolve_godot(args.godot)

    if _should_import(project, args.force_import, args.skip_import):
        imported = _run_godot(godot, project, ["--import"], args.import_timeout)
        if imported.returncode != 0:
            sys.stderr.write("Godot --import failed.\n")
            sys.stderr.write(imported.stdout)
            sys.stderr.write(imported.stderr)
            sys.exit(2)

    script_args = ["--script", CHECK_SCRIPT, "--"]
    if args.all:
        script_args.append("--all")
    if args.vendor:
        script_args.append("--vendor")
    for raw in list(args.root) + list(args.paths):
        script_args.extend(["--root", _to_res(project, raw)])

    result = _run_godot(godot, project, script_args, args.timeout)
    text = f"{result.stdout}\n{result.stderr}"
    trailer = _parse_json_trailer(text)
    if trailer is None and result.returncode != 0:
        sys.stderr.write("Godot check script failed to run.\n")
        sys.stderr.write(text)
        sys.exit(2)

    errors = _merge_failed(project, _parse_errors(project, text), trailer)
    checked = int(trailer["checked"]) if trailer else 0
    payload = {
        "ok": len(errors) == 0,
        "checked": checked,
        "errors": errors,
    }

    if args.json:
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        for item in errors:
            loc = item["file"]
            if item["line"]:
                loc = f"{loc}:{item['line']}"
            print(f"{loc}: {item['message']}")
        print(
            f"gdscript-check: {checked} files, {len(errors)} error"
            f"{'' if len(errors) == 1 else 's'}"
        )

    sys.exit(0 if payload["ok"] else 1)


if __name__ == "__main__":
    main()
