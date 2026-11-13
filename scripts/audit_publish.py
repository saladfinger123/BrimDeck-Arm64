#!/usr/bin/env python3
"""Audit a publish directory's PE machine types and native import names.

Managed IL-only assemblies may use the historical I386 machine value. A native
image, or a ReadyToRun image, must use the expected machine. This does not load
the binaries and does not prove that every P/Invoke target exists on a device.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from datetime import datetime, timezone
from pathlib import Path

MACHINES = {0x14C: "I386", 0x8664: "AMD64", 0xAA64: "ARM64", 0x1C4: "ARMNT"}
ILONLY = 0x1
BIT32_REQUIRED = 0x2


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rva_to_offset(sections: list[tuple[int, int, int]], rva: int) -> int | None:
    for virtual, raw, raw_size in sections:
        if virtual <= rva < virtual + max(raw_size, 1):
            return raw + (rva - virtual)
    return None


def imports(data: bytes, sections, import_rva: int) -> list[str]:
    if not import_rva:
        return []
    offset = rva_to_offset(sections, import_rva)
    if offset is None:
        return []
    names = []
    while offset + 20 <= len(data):
        name_rva = struct.unpack_from("<I", data, offset + 12)[0]
        if name_rva == 0:
            break
        name_at = rva_to_offset(sections, name_rva)
        if name_at is None:
            break
        end = data.find(b"\0", name_at)
        if end < 0:
            break
        names.append(data[name_at:end].decode("ascii", "replace"))
        offset += 20
    return names


def read_pe(path: Path) -> dict | None:
    data = path.read_bytes()
    if len(data) < 0x40 or data[:2] != b"MZ":
        return None
    e_lfanew = struct.unpack_from("<I", data, 0x3C)[0]
    if e_lfanew + 24 > len(data) or data[e_lfanew : e_lfanew + 4] != b"PE\0\0":
        return None
    machine = struct.unpack_from("<H", data, e_lfanew + 4)[0]
    sections_count = struct.unpack_from("<H", data, e_lfanew + 6)[0]
    optional_size = struct.unpack_from("<H", data, e_lfanew + 20)[0]
    optional = e_lfanew + 24
    if optional + 2 > len(data):
        return None
    magic = struct.unpack_from("<H", data, optional)[0]
    directory = optional + (112 if magic == 0x20B else 96 if magic == 0x10B else -1)
    import_rva = cli_rva = cli_size = 0
    if directory > 0 and directory + 15 * 8 <= optional + optional_size and directory + 15 * 8 <= len(data):
        import_rva = struct.unpack_from("<I", data, directory + 8)[0]
        cli_rva, cli_size = struct.unpack_from("<II", data, directory + 14 * 8)
    section_table = optional + optional_size
    sections = []
    for index in range(sections_count):
        at = section_table + index * 40
        if at + 40 > len(data):
            break
        virtual_size, virtual, raw_size, raw = struct.unpack_from("<IIII", data, at + 8)
        sections.append((virtual, raw, max(virtual_size, raw_size)))
    managed = cli_rva != 0 and cli_size >= 16
    flags = 0
    if managed:
        cli_at = rva_to_offset(sections, cli_rva)
        if cli_at is not None and cli_at + 20 <= len(data):
            flags = struct.unpack_from("<I", data, cli_at + 16)[0]
        else:
            managed = False
    return {
        "machine": machine,
        "machine_name": MACHINES.get(machine, hex(machine)),
        "managed": managed,
        "il_only": bool(flags & ILONLY),
        "bit32_required": bool(flags & BIT32_REQUIRED),
        "imports": imports(data, sections, import_rva),
    }


def acceptable(info: dict, expected: int) -> bool:
    if info["machine"] == expected:
        return True
    # AnyCPU IL: historical I386 machine, no 32-bit requirement. It is not a native x86 image.
    return info["managed"] and info["il_only"] and not info["bit32_required"] and info["machine"] == 0x14C


def source_declarations(root: Path) -> list[str]:
    found = set()
    for path in root.rglob("*.cs"):
        if any(part in {".git", "bin", "obj"} for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        for token in ("DllImport(\"", "TryLoad(\""):
            start = 0
            while True:
                at = text.find(token, start)
                if at < 0:
                    break
                name_at = at + len(token)
                end = text.find("\"", name_at)
                if end > name_at:
                    found.add(text[name_at:end])
                start = name_at
    return sorted(found)


def build_minimal_pe(machine: int, managed: bool, il_only: bool) -> bytes:
    # Layout: DOS header, PE, one section ".text" holding an optional CLI header.
    section_virtual = 0x1000
    section_raw = 0x200
    cli = b""
    if managed:
        flags = ILONLY if il_only else 0
        cli = struct.pack("<IHHIIII", 72, 2, 0, 0, 0, flags, 0)
    optional_size = 240  # PE32+ with 16 data directories
    headers = bytearray(section_raw)
    headers[0:2] = b"MZ"
    e_lfanew = 0x80
    struct.pack_into("<I", headers, 0x3C, e_lfanew)
    struct.pack_into("<I", headers, e_lfanew, 0x4550)
    struct.pack_into("<H", headers, e_lfanew + 4, machine)
    struct.pack_into("<H", headers, e_lfanew + 6, 1)
    struct.pack_into("<H", headers, e_lfanew + 20, optional_size)
    optional = e_lfanew + 24
    struct.pack_into("<H", headers, optional, 0x20B)
    directory = optional + 112
    if managed:
        struct.pack_into("<II", headers, directory + 14 * 8, section_virtual, len(cli))
    section = optional + optional_size
    headers[section : section + 8] = b".text\0\0\0"
    struct.pack_into("<IIII", headers, section + 8, len(cli) or 1, section_virtual, len(cli) or 1, section_raw)
    blob = bytes(headers) + cli
    if len(blob) < section_raw + len(cli):
        blob = blob + bytes(section_raw + len(cli) - len(blob))
    return blob


def self_test() -> None:
    import tempfile

    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        (root / "native-arm64.dll").write_bytes(build_minimal_pe(0xAA64, False, False))
        (root / "native-x64.dll").write_bytes(build_minimal_pe(0x8664, False, False))
        (root / "anycpu.dll").write_bytes(build_minimal_pe(0x14C, True, True))
        good = audit(root, 0xAA64, root)
        bad = [item["name"] for item in good["unexpected"]]
        if bad != ["native-x64.dll"]:
            raise SystemExit(f"self-test unexpected set was {bad}")
        anycpu = next(item for item in good["files"] if item["name"] == "anycpu.dll")
        if not anycpu["acceptable"]:
            raise SystemExit("self-test rejected AnyCPU IL")
    print("audit self-test passed")


def audit(directory: Path, expected: int, source: Path) -> dict:
    files = []
    unexpected = []
    for path in sorted(item for item in directory.rglob("*") if item.is_file()):
        info = read_pe(path)
        if info is None:
            continue
        record = {"name": str(path.relative_to(directory)), "sha256": sha256(path), "size": path.stat().st_size, **info}
        record["acceptable"] = acceptable(info, expected)
        files.append(record)
        if not record["acceptable"]:
            unexpected.append(record)
    return {
        "audited_at": datetime.now(timezone.utc).isoformat(),
        "expected_machine": MACHINES.get(expected, hex(expected)),
        "expected_machine_value": expected,
        "file_count": len(files),
        "unexpected": unexpected,
        "files": files,
        "source_native_declarations": source_declarations(source),
        "not_proven": [
            "P/Invoke and NativeLibrary names above are source declarations, not a list of modules loaded on a device.",
            "Transitive imports of Windows API sets beyond the PE import table are not walked.",
            "nvapi64.dll, nvidia-smi.exe and nvml.dll are not in the publish folder. No Windows ARM64 build of them is verified.",
            "The Inno Setup stub is a separate file. Its machine type is recorded by the installer audit and is not the payload.",
            "No Authenticode signature is checked or claimed.",
        ],
    }


def main() -> int:
    if "--self-test" in sys.argv:
        self_test()
        return 0
    parser = argparse.ArgumentParser()
    parser.add_argument("--dir", required=True, type=Path)
    parser.add_argument("--expect", required=True, choices=["arm64", "x64"])
    parser.add_argument("--source", type=Path, default=Path("."))
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--role", default="payload")
    args = parser.parse_args()
    expected = 0xAA64 if args.expect == "arm64" else 0x8664
    report = audit(args.dir, expected, args.source)
    report["role"] = args.role
    args.out.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"{args.role}: {report['file_count']} PE files, {len(report['unexpected'])} unexpected, expected {report['expected_machine']}")
    if report["unexpected"] and args.role == "payload":
        for item in report["unexpected"]:
            print(f"UNEXPECTED {item['name']} machine {item['machine_name']} managed {item['managed']}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
