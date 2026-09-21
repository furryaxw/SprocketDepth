"""Compare the IL surface of two managed assemblies using dnfile.

Usage: python compare_surface.py <old.dll> <new.dll>
"""
import sys

import dnfile


def _name(obj):
    return str(obj) if obj is not None else ""


def surface(path):
    pe = dnfile.dnPE(path)
    types = set()
    members = {}
    attrs = set()
    if pe.net is None or pe.net.mdtables is None:
        return types, members, attrs
    for row in pe.net.mdtables.TypeDef:
        if row is None:
            continue
        ns, nm = _name(row.TypeNamespace), _name(row.TypeName)
        full = "{}.{}".format(ns, nm) if ns else nm
        types.add(full)
        bag = members.setdefault(full, set())
        for table in ("MethodList", "FieldList"):
            collection = getattr(row, table, None)
            if collection is None:
                continue
            for entry in collection:
                r = getattr(entry, "row", entry)
                bag.add(_name(getattr(r, "Name", r)))
    for ca in pe.net.mdtables.CustomAttribute:
        parent = getattr(ca, "Parent", None)
        if parent is None or "Assembly" not in type(parent).__name__:
            continue
        try:
            attrs.add("{}:{}".format(_name(ca.Type.row.TypeName), ca.Value))
        except Exception:
            attrs.add(_name(ca.Type.row.TypeName))
    return types, members, attrs


def il_bodies(path):
    """Map "Type.Method" -> md5 of the method body's *true* IL bytes (header-aware)."""
    pe = dnfile.dnPE(path)
    raw = open(path, "rb").read()
    offsets = []
    if pe.net is None or pe.net.mdtables is None:
        return {}
    for trow in pe.net.mdtables.TypeDef:
        if trow is None:
            continue
        ns, nm = _name(trow.TypeNamespace), _name(trow.TypeName)
        full = "{}.{}".format(ns, nm) if ns else nm
        for entry in trow.MethodList or []:
            r = getattr(entry, "row", entry)
            rva = getattr(r, "Rva", 0)
            if not rva:
                continue
            try:
                off = pe.get_offset_from_rva(rva)
            except Exception:
                continue
            offsets.append((off, "{}.{}".format(full, _name(getattr(r, "Name", r)))))
    import hashlib

    out = {}
    for off, name in offsets:
        flags = raw[off]
        if flags & 0x03 == 0x02:  # tiny header: 1 byte, size in high 6 bits
            header, size = 1, flags >> 2
        elif flags & 0x03 == 0x03:  # fat header: 12 bytes, uint32 code size at +4
            header = (flags >> 12) * 4
            size = int.from_bytes(raw[off + 4 : off + 8], "little")
        else:
            continue
        out[name] = hashlib.md5(raw[off + header : off + header + size]).hexdigest()[:12]
    return out


def main():
    old, new = sys.argv[1], sys.argv[2]
    ot, om, oa = surface(old)
    nt, nm, na = surface(new)
    print("type count: old={} new={}".format(len(ot), len(nt)))
    print("types only in OLD:", sorted(ot - nt))
    print("types only in NEW:", sorted(nt - ot))
    diffs = 0
    for t in sorted(ot & nt):
        d_old, d_new = om.get(t, set()), nm.get(t, set())
        if d_old != d_new:
            diffs += 1
            print("member diff in {}: only-old={} only-new={}".format(t, sorted(d_old - d_new), sorted(d_new - d_old)))
    print("types with member diffs:", diffs)
    print("assembly attrs only in OLD:", sorted(oa - na))
    print("assembly attrs only in NEW:", sorted(na - oa))
    ob, nb = il_bodies(old), il_bodies(new)
    changed = sorted(k for k in set(ob) & set(nb) if ob[k] != nb[k])
    print("method IL hashes: old={} new={}".format(len(ob), len(nb)))
    print("methods with changed IL bytes:", changed)


if __name__ == "__main__":
    main()
