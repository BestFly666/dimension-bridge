"""
EAW DAT File Reader/Writer - Complete library for Alamo engine DAT files.
Matches the official eaw-texteditor tool's output format.

DAT Format (mastertextfile_english.dat):
  [Header]    4 bytes: entry count (uint32 LE)
  [Index]     N x 12 bytes per entry:
                4 bytes: CRC32 of key (uint32 LE)
                4 bytes: value length in characters (uint32 LE)  
                4 bytes: key length in bytes (uint32 LE)
  [ValueData] All value strings concatenated, UTF-16-LE encoded, NO null terminators
  [KeyData]   All key strings concatenated, ASCII encoded, NO null terminators

Usage:
  import datlib
  entries = datlib.read_dat('mastertextfile_english.dat')
  datlib.write_dat('output.dat', entries)
  datlib.dat_to_xml(entries, 'output.xml')
  entries = datlib.xml_to_dat('input.xml')
"""

import struct
import os
import sys
import xml.etree.ElementTree as ET
import binascii


def crc32(data: bytes) -> int:
    """Calculate CRC32 (matches binascii.crc32 used by Alamo engine)."""
    return binascii.crc32(data) & 0xFFFFFFFF


def read_dat(filepath: str) -> list:
    """
    Read a mastertextfile_english.dat file.
    Returns list of (key, value, crc) tuples.
    """
    with open(filepath, 'rb') as f:
        data = f.read()

    if len(data) < 4:
        raise ValueError("DAT file too small")

    entry_count = struct.unpack_from('<I', data, 0)[0]

    # Parse index table
    index_table = []
    index_offset = 4
    for i in range(entry_count):
        off = index_offset + i * 12
        crc = struct.unpack_from('<I', data, off)[0]
        val_chars = struct.unpack_from('<I', data, off + 4)[0]
        key_bytes = struct.unpack_from('<I', data, off + 8)[0]
        index_table.append((crc, val_chars, key_bytes))

    # Calculate positions: NO null terminators, strings are concatenated directly
    total_val_chars = sum(vc for _, vc, _ in index_table)
    val_data_offset = 4 + entry_count * 12
    key_data_offset = val_data_offset + total_val_chars * 2

    # Extract values and keys
    entries = []
    val_pos = val_data_offset
    key_pos = key_data_offset
    for crc, val_chars, key_bytes in index_table:
        # Value (UTF-16-LE, exact byte count = val_chars * 2)
        val_byte_count = val_chars * 2
        value = data[val_pos:val_pos + val_byte_count].decode('utf-16-le', errors='replace')
        val_pos += val_byte_count

        # Key (ASCII, exact byte count = key_bytes)
        key = data[key_pos:key_pos + key_bytes].decode('ascii', errors='replace')
        key_pos += key_bytes

        entries.append((key, value, crc))

    return entries


def write_dat(filepath: str, entries: list) -> None:
    """
    Write entries to a mastertextfile_english.dat file.
    entries: list of (key, value) tuples. CRC is auto-calculated.
    """
    count = len(entries)

    # Encode
    encoded = []
    for key, value in entries:
        key_enc = key.encode('ascii', errors='replace')
        val_enc = value.encode('utf-16-le')
        crc = crc32(key_enc)
        val_chars = len(value)  # Python char count = UTF-16 code units (BMP only)
        key_bytes = len(key_enc)
        encoded.append((key_enc, val_enc, crc, val_chars, key_bytes))

    with open(filepath, 'wb') as f:
        # Header
        f.write(struct.pack('<I', count))

        # Index table
        for _, _, crc, val_chars, key_bytes in encoded:
            f.write(struct.pack('<III', crc, val_chars, key_bytes))

        # Value data (UTF-16-LE, no null terminators)
        for _, val_enc, _, _, _ in encoded:
            f.write(val_enc)

        # Key data (ASCII, no null terminators)
        for key_enc, _, _, _, _ in encoded:
            f.write(key_enc)

    size = os.path.getsize(filepath)
    print(f"Wrote {count} entries to {filepath} ({size:,} bytes)")


def dat_to_xml(entries: list, filepath: str) -> None:
    """Export entries to XML (matches official eaw-texteditor format)."""
    root = ET.Element('TranslationData')
    for key, value, crc in entries:
        loc = ET.SubElement(root, 'Localisation')
        loc.set('Key', key)
        trans = ET.SubElement(loc, 'Translation')
        trans.set('Language', 'ENGLISH')
        trans.text = value
    tree = ET.ElementTree(root)
    ET.indent(tree, '  ')
    tree.write(filepath, encoding='utf-8', xml_declaration=True)
    print(f"Exported {len(entries)} entries to {filepath}")


def xml_to_dat(filepath: str) -> list:
    """Import entries from XML file."""
    tree = ET.parse(filepath)
    entries = []
    for loc in tree.findall('.//Localisation'):
        key = loc.get('Key', '')
        for trans in loc.findall('.//Translation'):
            if trans.get('Language', 'ENGLISH') == 'ENGLISH':
                entries.append((key, trans.text or ''))
                break
    return entries


def compare_dats(dat1: str, dat2: str) -> dict:
    """Compare two DAT files entry by entry."""
    try:
        d1 = read_dat(dat1)
        d2 = read_dat(dat2)
    except Exception as e:
        return {'error': str(e)}

    m1 = {k: (v, c) for k, v, c in d1}
    m2 = {k: (v, c) for k, v, c in d2}

    result = {
        'count_1': len(d1),
        'count_2': len(d2),
        'only_in_1': [],
        'only_in_2': [],
        'crc_diff': [],
        'value_diff': [],
    }

    for k in m1:
        if k not in m2:
            result['only_in_1'].append(k)
    for k in m2:
        if k not in m1:
            result['only_in_2'].append(k)
    for k in m1:
        if k in m2:
            v1, c1 = m1[k]
            v2, c2 = m2[k]
            if c1 != c2:
                result['crc_diff'].append((k, c1, c2))
            if v1 != v2:
                result['value_diff'].append((k, len(v1), len(v2)))

    return result


def roundtrip_test(filepath: str) -> bool:
    """Read DAT -> write temp -> read temp -> compare. Returns True if identical."""
    import tempfile
    original = read_dat(filepath)
    tmp = tempfile.mktemp(suffix='.dat')
    write_dat(tmp, [(k, v) for k, v, _ in original])
    reloaded = read_dat(tmp)
    os.unlink(tmp)

    if len(original) != len(reloaded):
        print(f"FAIL: Count mismatch {len(original)} vs {len(reloaded)}")
        return False
    for i, ((ok, ov, oc), (rk, rv, rc)) in enumerate(zip(original, reloaded)):
        if ok != rk or ov != rv or oc != rc:
            print(f"FAIL at [{i}]: key={ok!=rk}, val={ov!=rv}, crc={oc!=rc}")
            return False
    print(f"PASS: {len(original)} entries roundtrip OK")
    return True


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Commands: read | to_xml | to_dat | compare | roundtrip")
        print("  read <file.dat>")
        print("  to_xml <file.dat> <out.xml>")
        print("  to_dat <file.xml> <out.dat>")
        print("  compare <a.dat> <b.dat>")
        print("  roundtrip <file.dat>")
        sys.exit(0)

    cmd = sys.argv[1]
    if cmd == 'read':
        entries = read_dat(sys.argv[2])
        print(f"Entries: {len(entries)}")
        for i, (k, v, c) in enumerate(entries[:5]):
            pv = v[:80] + '...' if len(v) > 80 else v
            print(f"  [{i}] CRC=0x{c:08X} | {k} | {pv}")
        if len(entries) > 5:
            print(f"  ... +{len(entries)-5} more")

    elif cmd == 'to_xml':
        dat_to_xml(read_dat(sys.argv[2]), sys.argv[3])
    elif cmd == 'to_dat':
        write_dat(sys.argv[3], xml_to_dat(sys.argv[2]))
    elif cmd == 'compare':
        r = compare_dats(sys.argv[2], sys.argv[3])
        for k, v in r.items():
            if isinstance(v, list):
                print(f"{k}: {len(v)}")
                for item in v[:10]:
                    print(f"  {item}")
                if len(v) > 10:
                    print(f"  ... +{len(v)-10} more")
            else:
                print(f"{k}: {v}")
    elif cmd == 'roundtrip':
        roundtrip_test(sys.argv[2])
    else:
        print(f"Unknown: {cmd}")
