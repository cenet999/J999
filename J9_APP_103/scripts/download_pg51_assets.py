from __future__ import annotations

import base64
import json
import pathlib
import ssl
import urllib.parse
import urllib.request


ROOT = pathlib.Path(__file__).resolve().parents[1]
PLAYWRIGHT_DATA = ROOT / "output" / "playwright" / "target-home-data.json"
ASSET_ROOT = ROOT / "assets" / "pg51"
BRAND_DIR = ASSET_ROOT / "brand"
QUICK_DIR = ASSET_ROOT / "quick"
GAME_DIR = ASSET_ROOT / "games"


def ensure_dirs() -> None:
    for directory in (BRAND_DIR, QUICK_DIR, GAME_DIR):
        directory.mkdir(parents=True, exist_ok=True)


def load_images() -> list[dict]:
    data = json.loads(PLAYWRIGHT_DATA.read_text(encoding="utf-8"))
    return data["imgs"]


def write_remote(url: str, target: pathlib.Path) -> None:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "Mozilla/5.0",
            "Referer": "https://3myzq7zymz2.pg511230.cc/",
        },
    )
    ssl_context = ssl.create_default_context()
    ssl_context.check_hostname = False
    ssl_context.verify_mode = ssl.CERT_NONE
    with urllib.request.urlopen(request, context=ssl_context) as response:
        target.write_bytes(response.read())


def write_data_url(data_url: str, target: pathlib.Path) -> None:
    _, payload = data_url.split(",", 1)
    target.write_bytes(base64.b64decode(payload))


def suffix_from_url(url: str) -> str:
    path = urllib.parse.urlparse(url).path
    suffix = pathlib.Path(path).suffix.lower()
    return suffix or ".png"


def download_brand_and_quick(images: list[dict]) -> list[dict]:
    manifest = []
    brand_assets = [
        ("brand-logo", images[0]["src"], BRAND_DIR / "brand-logo.png"),
        ("quick-daily-rebate", images[2]["src"], QUICK_DIR / "quick-daily-rebate.png"),
        ("quick-lucky-box", images[3]["src"], QUICK_DIR / "quick-lucky-box.png"),
        ("quick-cooperate-earn", images[4]["src"], QUICK_DIR / "quick-cooperate-earn.png"),
        ("quick-profit-rain", images[5]["src"], QUICK_DIR / "quick-profit-rain.png"),
    ]

    for key, source, target in brand_assets:
        write_remote(source, target)
        manifest.append({"key": key, "file": str(target.relative_to(ROOT)), "source": source})
    return manifest


def download_games(images: list[dict]) -> list[dict]:
    manifest = []

    remote_sources: list[str] = []
    for item in images[6:]:
        source = item["src"]
        if source.startswith("data:"):
            continue
        if "empty-icon" in source or "icon-type-cq9" in source:
            continue
        remote_sources.append(source)
        if len(remote_sources) == 21:
            break

    data_sources = [
        item["src"]
        for item in images
        if item["src"].startswith("data:") and item["width"] == 368 and item["height"] == 279
    ][:16]

    for index, source in enumerate(remote_sources, start=1):
        suffix = suffix_from_url(source)
        target = GAME_DIR / f"game-{index:02d}{suffix}"
        write_remote(source, target)
        manifest.append({"index": index, "file": str(target.relative_to(ROOT)), "source": source})

    for offset, source in enumerate(data_sources, start=22):
        target = GAME_DIR / f"game-{offset:02d}.png"
        write_data_url(source, target)
        manifest.append({"index": offset, "file": str(target.relative_to(ROOT)), "source": "data-url"})

    return manifest


def main() -> None:
    if not PLAYWRIGHT_DATA.exists():
        raise SystemExit(f"Missing source data: {PLAYWRIGHT_DATA}")

    ensure_dirs()
    images = load_images()

    manifest = {
        "brand": download_brand_and_quick(images),
        "games": download_games(images),
    }

    manifest_path = ASSET_ROOT / "manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    print(f"Saved assets to {ASSET_ROOT}")


if __name__ == "__main__":
    main()
