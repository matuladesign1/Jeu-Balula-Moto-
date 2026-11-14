#!/usr/bin/env python3
"""Lecteur de fichiers .blend (Blender 2.7x – 2.9x, format « MVert/MLoop/MPoly ») : SDNA, blocs, structures.
Permet d'extraire les maillages (objets, matrices, modificateurs miroir), matériaux, images (internes ou externes)."""
import struct


class Struct:
    def __init__(self, bf, sdna_index, data, offset, addr):
        self._bf, self._si, self._data, self._off, self.addr = bf, sdna_index, data, offset, addr

    @property
    def type(self):
        return self._bf.types[self._bf.structs[self._si][0]]

    def fields(self):
        return [f[0] for f in self._bf.layouts[self._si]]

    def has(self, name):
        return name in self._bf.layout_map[self._si]

    def raw(self, name):
        f = self._bf.layout_map[self._si][name]
        return f

    def get(self, name, default=None):
        m = self._bf.layout_map[self._si]
        if name not in m:
            return default
        (fname, tname, ptr, dims, off, size) = m[name]
        o = self._off + off
        bf = self._bf
        if ptr:
            n = 1
            for d in dims:
                n *= d
            vals = struct.unpack_from(bf.e + (bf.pfmt * n), self._data, o)
            return vals[0] if n == 1 else list(vals)
        fmt = {"char": "b", "uchar": "B", "short": "h", "ushort": "H", "int": "i", "uint": "I", "float": "f",
               "double": "d", "int64_t": "q", "uint64_t": "Q", "int8_t": "b", "uint8_t": "B"}.get(tname)
        if fmt is None:
            # structure imbriquée
            si = bf.struct_index.get(tname)
            if si is None:
                return None
            if dims:
                n = 1
                for d in dims:
                    n *= d
                L = bf.tlen[bf.structs[si][0]]
                return [Struct(bf, si, self._data, o + k * L, None) for k in range(n)]
            return Struct(bf, si, self._data, o, None)
        n = 1
        for d in dims:
            n *= d
        if tname == "char" and dims:
            raw = self._data[o:o + n]
            return raw.split(b"\0", 1)[0].decode("utf-8", "replace")
        vals = struct.unpack_from(bf.e + fmt * n, self._data, o)
        if not dims:
            return vals[0]
        if len(dims) == 2:
            return [list(vals[i * dims[1]:(i + 1) * dims[1]]) for i in range(dims[0])]
        return list(vals)


class BlendFile:
    def __init__(self, path):
        d = open(path, "rb").read()
        self.data = d
        assert d[:7] == b"BLENDER", "pas un fichier .blend"
        self.psize = 8 if d[7:8] == b"-" else 4
        self.e = "<" if d[8:9] == b"v" else ">"
        self.version = int(d[9:12])
        self.pfmt = "Q" if self.psize == 8 else "I"
        pos = 12
        self.blocks = []
        hdr = self.e + "4si" + self.pfmt + "ii"
        hlen = struct.calcsize(hdr)
        while pos + hlen <= len(d):
            code, size, addr, sdna, count = struct.unpack_from(hdr, d, pos)
            pos += hlen
            code = code.rstrip(b"\0").decode("latin1")
            self.blocks.append((code, size, addr, sdna, count, pos))
            if code == "ENDB":
                break
            pos += size
        self.addr_map = {b[2]: b for b in self.blocks}
        dna = [b for b in self.blocks if b[0] == "DNA1"][0]
        self._parse_sdna(dna[5])

    def _parse_sdna(self, o):
        d, e = self.data, self.e
        assert d[o:o + 4] == b"SDNA"
        o += 4

        def align4(x):
            return (x + 3) & ~3
        assert d[o:o + 4] == b"NAME"
        n = struct.unpack_from(e + "i", d, o + 4)[0]
        o += 8
        names = []
        for _ in range(n):
            k = d.index(b"\0", o)
            names.append(d[o:k].decode("latin1"))
            o = k + 1
        o = align4(o)
        assert d[o:o + 4] == b"TYPE"
        n = struct.unpack_from(e + "i", d, o + 4)[0]
        o += 8
        types = []
        for _ in range(n):
            k = d.index(b"\0", o)
            types.append(d[o:k].decode("latin1"))
            o = k + 1
        o = align4(o)
        assert d[o:o + 4] == b"TLEN"
        o += 4
        tlen = list(struct.unpack_from(e + "%dh" % len(types), d, o))
        o = align4(o + 2 * len(types))
        assert d[o:o + 4] == b"STRC"
        n = struct.unpack_from(e + "i", d, o + 4)[0]
        o += 8
        structs = []
        for _ in range(n):
            t, nf = struct.unpack_from(e + "hh", d, o)
            o += 4
            fl = []
            for _ in range(nf):
                ft, fn = struct.unpack_from(e + "hh", d, o)
                o += 4
                fl.append((ft, fn))
            structs.append((t, fl))
        self.names, self.types, self.tlen, self.structs = names, types, tlen, structs
        self.struct_index = {types[s[0]]: i for i, s in enumerate(structs)}
        self.layouts, self.layout_map = [], []
        for si, (t, fl) in enumerate(structs):
            off = 0
            lay = []
            for ft, fn in fl:
                name = names[fn]
                ptr = name.startswith("*") or name.startswith("(*")
                base = name.lstrip("*").replace("(", "").replace(")", "")
                dims = []
                if "[" in base:
                    parts = base.split("[")
                    base = parts[0]
                    dims = [int(p.rstrip("]")) for p in parts[1:]]
                if base.startswith("*"):
                    base = base.lstrip("*")
                n = 1
                for dd in dims:
                    n *= dd
                if ptr:
                    size = self.psize * n
                else:
                    size = tlen[ft] * n
                lay.append((base, types[ft], ptr, dims, off, size))
                off += size
            self.layouts.append(lay)
            self.layout_map.append({l[0]: l for l in lay})

    def block_structs(self, block):
        code, size, addr, sdna, count, pos = block
        si = sdna
        L = self.tlen[self.structs[si][0]]
        return [Struct(self, si, self.data, pos + k * L, addr + k * L) for k in range(count)]

    def find_blocks(self, code):
        return [b for b in self.blocks if b[0] == code]

    def deref(self, addr):
        """Bloc pointé par une adresse (exacte) : liste des structures du bloc."""
        if not addr:
            return None
        b = self.addr_map.get(addr)
        if b is None:
            return None
        return b

    def deref_struct(self, addr):
        b = self.deref(addr)
        if b is None:
            return None
        s = self.block_structs(b)
        return s[0] if s else None

    def block_bytes(self, addr):
        b = self.deref(addr)
        if b is None:
            return None
        return self.data[b[5]:b[5] + b[1]]

    def pointer_array(self, addr):
        b = self.deref(addr)
        if b is None:
            return []
        n = b[1] // self.psize
        return list(struct.unpack_from(self.e + self.pfmt * n, self.data, b[5]))

    def listbase(self, lb):
        """Parcourt une ListBase (first/last) : structures liées par next."""
        out = []
        a = lb.get("first")
        guard = 0
        while a and guard < 100000:
            s = self.deref_struct(a)
            if s is None:
                break
            out.append(s)
            a = s.get("next")
            guard += 1
        return out
