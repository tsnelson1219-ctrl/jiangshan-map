from __future__ import annotations

import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist"
PACKAGE = DIST / "JiangshanAtlas_Portable"

EXCLUDED_DIRS = {
    ".git", ".github", "docs", "scripts", "skills", "data", "tools", "dist", "__pycache__",
}
EXCLUDED_FILES = {
    ".gitignore", ".dockerignore", "Dockerfile", "docker-compose.yml", "package-lock.json",
}
EXCLUDED_RELATIVE_FILES = {Path("geojson") / "demo.html"}

def should_exclude(path: Path) -> bool:
    relative = path.relative_to(ROOT)
    if any(part in EXCLUDED_DIRS for part in relative.parts):
        return True
    if path.name in EXCLUDED_FILES or path.suffix.lower() == ".md":
        return True
    if relative in EXCLUDED_RELATIVE_FILES:
        return True
    if path.name.lower() == "jiangshanlauncher.cs":
        return True
    return False

def main() -> None:
    if PACKAGE.exists():
        shutil.rmtree(PACKAGE)
    PACKAGE.mkdir(parents=True)

    for path in ROOT.rglob("*"):
        if not path.is_file() or should_exclude(path):
            continue
        relative = path.relative_to(ROOT)
        destination = PACKAGE / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)

    launcher = DIST / "JiangshanAtlas.exe"
    if not launcher.exists():
        raise FileNotFoundError("Missing compiled launcher: " + str(launcher))
    shutil.copy2(launcher, PACKAGE / launcher.name)

    if not (PACKAGE / "index.html").exists():
        raise FileNotFoundError("Packaged index.html is missing")
    print("Packaged application at:", PACKAGE)

if __name__ == "__main__":
    main()
