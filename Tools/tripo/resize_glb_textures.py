"""Resize images embedded in a GLB to a max edge (default 1024).

Tripo's convert:texture_size bills extra credits. Do this locally instead.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.stderr.write("Need Pillow: pip install -r Tools/tripo/requirements.txt\n")
    sys.exit(2)

GLB_MAGIC = b"glTF"
JSON_CHUNK = b"JSON"
BIN_CHUNK = b"BIN\x00"


def _pad4(data: bytes, pad: bytes) -> bytes:
    n = (4 - (len(data) % 4)) % 4
    return data + pad * n


def _read_glb(path: Path) -> tuple[dict, bytes]:
    raw = path.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", raw, 0)
    if magic != GLB_MAGIC or version != 2:
        raise ValueError(f"{path} is not a GLB v2")
    offset = 12
    gltf: dict | None = None
    bin_chunk = b""
    while offset < length:
        chunk_len, chunk_type = struct.unpack_from("<I4s", raw, offset)
        offset += 8
        payload = raw[offset : offset + chunk_len]
        offset += chunk_len
        if chunk_type == JSON_CHUNK:
            gltf = json.loads(payload)
        elif chunk_type == BIN_CHUNK:
            bin_chunk = payload
    if gltf is None:
        raise ValueError(f"{path} has no JSON chunk")
    return gltf, bin_chunk


def _write_glb(path: Path, gltf: dict, bin_chunk: bytes) -> None:
    json_bytes = _pad4(
        json.dumps(gltf, separators=(",", ":"), ensure_ascii=False).encode("utf-8"),
        b" ",
    )
    bin_bytes = _pad4(bin_chunk, b"\x00")
    length = 12 + 8 + len(json_bytes) + 8 + len(bin_bytes)
    header = struct.pack("<4sII", GLB_MAGIC, 2, length)
    json_header = struct.pack("<I4s", len(json_bytes), JSON_CHUNK)
    bin_header = struct.pack("<I4s", len(bin_bytes), BIN_CHUNK)
    path.write_bytes(header + json_header + json_bytes + bin_header + bin_bytes)


def _resize_image_bytes(blob: bytes, mime: str, max_edge: int) -> tuple[bytes, tuple[int, int], tuple[int, int]]:
    im = Image.open(BytesIO(blob))
    im.load()
    src = (im.width, im.height)
    if max(src) <= max_edge:
        return blob, src, src
    scale = max_edge / float(max(src))
    dst = (max(1, round(im.width * scale)), max(1, round(im.height * scale)))
    # Normal/ORM stay RGB; don't convert JPEG to PNG.
    if im.mode not in ("RGB", "L"):
        im = im.convert("RGB")
    resized = im.resize(dst, Image.Resampling.LANCZOS)
    out = BytesIO()
    fmt = "JPEG" if mime == "image/jpeg" else "PNG"
    if fmt == "JPEG":
        if resized.mode != "RGB":
            resized = resized.convert("RGB")
        resized.save(out, format="JPEG", quality=90, optimize=True)
    else:
        resized.save(out, format="PNG", optimize=True)
    return out.getvalue(), src, dst


def resize_glb(path: Path, max_edge: int, dry_run: bool) -> list[str]:
    gltf, bin_chunk = _read_glb(path)
    images = gltf.get("images") or []
    views = gltf.get("bufferViews") or []
    if not images:
        return [f"{path}: no images"]

    # Copy every bufferView, replace image payloads, then pack tightly.
    blobs: list[bytes] = []
    for view in views:
        if view.get("buffer", 0) != 0:
            raise ValueError(f"{path}: only buffer 0 is supported")
        start = view.get("byteOffset", 0)
        blobs.append(bin_chunk[start : start + view["byteLength"]])

    lines: list[str] = []
    changed = False
    for image in images:
        mime = image.get("mimeType") or "image/jpeg"
        if "bufferView" in image:
            idx = image["bufferView"]
            new_blob, src, dst = _resize_image_bytes(blobs[idx], mime, max_edge)
            name = image.get("name") or f"image{idx}"
            if src == dst:
                lines.append(f"  keep {name} {src[0]}x{src[1]}")
            else:
                lines.append(f"  {name} {src[0]}x{src[1]} -> {dst[0]}x{dst[1]}")
                blobs[idx] = new_blob
                changed = True
            continue
        uri = image.get("uri")
        if uri and not uri.startswith("data:"):
            side = path.parent / uri
            if not side.is_file():
                lines.append(f"  skip missing {uri}")
                continue
            new_blob, src, dst = _resize_image_bytes(side.read_bytes(), mime, max_edge)
            name = image.get("name") or uri
            if src == dst:
                lines.append(f"  keep {name} {src[0]}x{src[1]}")
            elif dry_run:
                lines.append(f"  {name} {src[0]}x{src[1]} -> {dst[0]}x{dst[1]}")
            else:
                side.write_bytes(new_blob)
                lines.append(f"  {name} {src[0]}x{src[1]} -> {dst[0]}x{dst[1]}")
                changed = True

    if dry_run or not changed:
        if not changed:
            lines.append("  already within max edge")
        return lines

    packed = bytearray()
    for i, blob in enumerate(blobs):
        packed.extend(b"\x00" * ((4 - (len(packed) % 4)) % 4))
        views[i]["byteOffset"] = len(packed)
        views[i]["byteLength"] = len(blob)
        packed.extend(blob)

    buffers = gltf.setdefault("buffers", [{}])
    buffers[0]["byteLength"] = len(packed)
    _write_glb(path, gltf, bytes(packed))
    lines.append(f"  wrote {path} ({path.stat().st_size} bytes)")
    return lines


def main() -> int:
    parser = argparse.ArgumentParser(description="Downsample GLB embedded textures locally (no Tripo convert).")
    parser.add_argument("glb", nargs="+", type=Path, help="GLB file(s)")
    parser.add_argument("--max", type=int, default=1024, help="max edge in pixels (default 1024)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    for glb in args.glb:
        if not glb.is_file():
            sys.stderr.write(f"missing {glb}\n")
            return 1
        print(glb)
        for line in resize_glb(glb, args.max, args.dry_run):
            print(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
