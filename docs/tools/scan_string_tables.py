#!/usr/bin/env python3
"""Find the header pointers that bound tables of string-pointers.

Most of an offset profile is pointer pairs bounding a table of int32 offsets, each
pointing at a null-terminated CP932 string -- what DataExtractionService.ReadStringRange
consumes. This finds those tables and prints the first strings of each, so the pointer can
be identified by what it names: armour slots read as ヘルム/メイル/アーム/ベルト, weapons
as アイアンソード, items as 調合書.

Run it on a *decrypted, decompressed* file:

    ./ReFrontier unpack mhfdat.bin --flat
    python3 scan_string_tables.py mhfdat.bin.decd.bin

Validated against the `zz` profile, whose armour (0x64->0x60 and the four after it),
weapon (0x84, 0x88) and item (0x100->0xFC) pointers it recovers exactly.
"""
import struct
import sys


def read_u32(data, position):
    """Read a little-endian uint32, or None past the end."""
    if position + 4 > len(data):
        return None
    return struct.unpack_from("<I", data, position)[0]


def string_table_at(data, start, probe=24, min_ok=0.9):
    """Score `start` as a table of string-pointers, and sample what it names.

    A table is a run of ascending in-range offsets whose targets decode as CP932 and
    hold no control characters.
    """
    size = len(data)
    if not 0 < start < size - 4 * probe:
        return 0.0, []

    pointers = [read_u32(data, start + 4 * k) for k in range(probe)]
    if any(p is None or not 0 < p < size for p in pointers):
        return 0.0, []
    if any(b <= a for a, b in zip(pointers, pointers[1:])):
        return 0.0, []

    ok, sample = 0, []
    for pointer in pointers:
        end = data.find(b"\x00", pointer)
        if end < 0 or end - pointer > 200:
            continue
        try:
            text = data[pointer:end].decode("cp932")
        except UnicodeDecodeError:
            continue
        if text and all(ord(c) >= 0x20 for c in text):
            ok += 1
            if len(sample) < 3:
                sample.append(text)
    return ok / probe, sample


def scan(path, limit=0x1400, min_ok=0.9):
    """Report every header pointer that begins a string table."""
    data = open(path, "rb").read()
    positions = [(p, read_u32(data, p)) for p in range(0, min(limit, len(data) - 4), 4)]
    positions = [(p, v) for p, v in positions if v and 0 < v < len(data)]
    values = sorted({v for _, v in positions})

    print(f"{path}  size 0x{len(data):X}  version byte 0x{data[4]:02X}")
    for position, value in positions:
        ratio, sample = string_table_at(data, value, min_ok=min_ok)
        if ratio < min_ok:
            continue
        following = next((v for v in values if v > value), None)
        # Several pointers can hold the same value; the one that bounds this table is the
        # neighbour in the chain, so prefer the position nearest this one.
        candidates = [p for p, v in positions if v == following]
        end_position = min(candidates, key=lambda p: abs(p - position)) if candidates else None
        count = (following - value) // 4 if following else 0
        end = f"0x{end_position:03X}" if end_position is not None else "?"
        print(f"  start 0x{position:03X}  end {end:>5}  {count:6} strings  {sample}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    for argument in sys.argv[1:]:
        scan(argument)
        print()
