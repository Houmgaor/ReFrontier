#!/usr/bin/env python3
"""Find the quest sections in mhfinf.bin.

A quest entry ends with four pointers to its title and three description lines. A section
is a run of entries at a fixed stride, so a section shows up as a run of positions whose
four pointers are ascending, clustered and land on CP932 text.

Run it on a *decrypted, decompressed* file:

    ./ReFrontier unpack mhfinf.bin --flat
    python3 scan_quest_sections.py mhfinf.bin.decd.bin

Validated against the `zz` profile: with the default stride it recovers 0x6BD40 x95,
0x740E0 x62, 0x797C0 x99 and the rest exactly, and finds the further sections that
profile does not yet list (see issue #20).

The stride is version-specific -- 0x160 for G10-ZZ, 0xC0 for GG and G2, 0xA8 for
Forward.4 -- and `--stride` sets it. A different stride means a different entry layout,
which the reader models in code, so a matching stride is a precondition for reading a
version's quests, not a guarantee.
"""
import argparse
import struct
from collections import Counter


def read_u32(data, position):
    return struct.unpack_from("<I", data, position)[0]


def is_entry(data, base, pointer_offset):
    """Does an entry start here: four ascending, clustered pointers to CP932 text?"""
    size = len(data)
    if base < 0 or base + pointer_offset + 16 > size:
        return False

    pointers = [read_u32(data, base + pointer_offset + 4 * k) for k in range(4)]
    if not all(0 < p < size for p in pointers):
        return False
    if any(b < a for a, b in zip(pointers, pointers[1:])):
        return False
    if pointers[-1] - pointers[0] > 600:
        return False

    for pointer in pointers:
        end = data.find(b"\x00", pointer)
        if end < 0 or end - pointer > 200:
            return False
        try:
            text = data[pointer:end].decode("cp932")
        except UnicodeDecodeError:
            return False
        if not text or any(ord(c) < 0x20 and c != "\n" for c in text):
            return False
    return True


def guess_stride(data, pointer_offset):
    """The most common gap between entry-like positions is the entry size."""
    hits = [b for b in range(0, len(data) - pointer_offset - 16, 4)
            if is_entry(data, b, pointer_offset)]
    gaps = Counter(b - a for a, b in zip(hits, hits[1:]))
    return [(gap, count) for gap, count in gaps.most_common(5) if gap > 0x40]


def sections(data, pointer_offset, stride, min_run):
    """Runs of at least `min_run` entries at `stride`."""
    size = len(data)
    good = bytearray(size // 4 + 1)
    for base in range(0, size - pointer_offset - 16, 4):
        if is_entry(data, base, pointer_offset):
            good[base // 4] = 1

    found, seen = [], set()
    for base in range(0, size - pointer_offset - 16, 4):
        if not good[base // 4] or base in seen:
            continue
        count, position = 0, base
        while position + pointer_offset + 16 <= size and good[position // 4]:
            seen.add(position)
            count += 1
            position += stride
        if count >= min_run:
            found.append((base, count))

    # An entry is detectable at base+4 too, since its pointers still read as four
    # ascending pointers one slot along. Keep the longest run of each cluster.
    merged = []
    for offset, count in sorted(found):
        if merged and offset - merged[-1][0] <= 0x10:
            if count > merged[-1][1]:
                merged[-1] = (offset, count)
            continue
        merged.append((offset, count))

    # A run inside a longer one is the same quests found again, not a section of its own.
    result, end = [], -1
    for offset, count in merged:
        if offset < end:
            continue
        result.append((offset, count))
        end = offset + count * stride
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("file", help="decrypted, decompressed mhfinf.bin")
    parser.add_argument("--stride", type=lambda s: int(s, 0), default=0x160,
                        help="quest entry size (default 0x160, G10-ZZ)")
    parser.add_argument("--pointer-offset", type=lambda s: int(s, 0), default=0x140,
                        help="offset of the four string pointers inside an entry")
    parser.add_argument("--min-run", type=int, default=8,
                        help="shortest run to report as a section")
    parser.add_argument("--guess-stride", action="store_true",
                        help="report the most common entry spacing and stop")
    args = parser.parse_args()

    data = open(args.file, "rb").read()
    print(f"{args.file}  size 0x{len(data):X}  version byte 0x{data[4]:02X}")

    if args.guess_stride:
        for gap, count in guess_stride(data, args.pointer_offset):
            print(f"  stride 0x{gap:X} seen {count} times")
        return

    found = sections(data, args.pointer_offset, args.stride, args.min_run)
    print(f"  {len(found)} sections, {sum(c for _, c in found)} quests, stride 0x{args.stride:X}")
    for offset, count in found:
        print(f'      {{ "offset": "0x{offset:X}", "count": {count} }},')


if __name__ == "__main__":
    main()
