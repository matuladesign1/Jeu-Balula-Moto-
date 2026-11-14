#!/usr/bin/env python3
"""Lecture / écriture fidèle d'un FBX binaire : les propriétés non modifiées sont réécrites octet pour octet,
les tableaux modifiés sont recompressés (zlib) et toutes les positions de fin de nœud recalculées.
Un aller-retour sans modification redonne exactement le fichier d'origine (vérifié par verifier())."""
import struct, zlib

FMT = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B'}


class Prop:
    __slots__ = ('t', 'raw', 'val', 'dirty')

    def __init__(self, t, raw, val):
        self.t, self.raw, self.val, self.dirty = t, raw, val, False

    def set(self, val):
        self.val = list(val)
        self.dirty = True

    def encode(self):
        if not self.dirty:
            return self.raw
        t = self.t
        assert t in FMT, 'seuls les tableaux peuvent être modifiés'
        data = struct.pack('<%d%s' % (len(self.val), FMT[t]), *self.val)
        comp = zlib.compress(data, 9)
        return t.encode() + struct.pack('<III', len(self.val), 1, len(comp)) + comp


class Node:
    __slots__ = ('name', 'props', 'kids', 'null')

    def __init__(self, name, props, kids, null):
        self.name, self.props, self.kids, self.null = name, props, kids, null

    def find(self, name):
        for k in self.kids:
            if k.name == name:
                return k
        return None

    def findall(self, name):
        return [k for k in self.kids if k.name == name]


class FBX:
    def __init__(self, path):
        d = open(path, 'rb').read()
        assert d[:20] == b'Kaydara FBX Binary  '
        self.head = d[:27]
        self.version = struct.unpack_from('<I', d, 23)[0]
        self.wide = self.version >= 7500
        self.nl = 25 if self.wide else 13
        pos = 27
        self.nodes = []
        while True:
            n, pos = self._node(d, pos)
            if n is None:
                break
            self.nodes.append(n)
        self.footer_id = d[pos:pos + 16]          # identifiant de pied (16 octets)
        rest = d[pos + 16:]
        k = 0
        while k < len(rest) and rest[k] == 0:
            k += 1
        # zéros (bourrage d'alignement puis 4 zéros) jusqu'à la version
        self.pad_orig = k
        self.tail = rest[k:]
        self.size = len(d)

    def _prop(self, d, p):
        t = chr(d[p])
        s = p
        p += 1
        if t == 'Y': v = struct.unpack_from('<h', d, p)[0]; p += 2
        elif t == 'C': v = d[p] != 0; p += 1
        elif t == 'I': v = struct.unpack_from('<i', d, p)[0]; p += 4
        elif t == 'F': v = struct.unpack_from('<f', d, p)[0]; p += 4
        elif t == 'D': v = struct.unpack_from('<d', d, p)[0]; p += 8
        elif t == 'L': v = struct.unpack_from('<q', d, p)[0]; p += 8
        elif t in FMT:
            n, enc, clen = struct.unpack_from('<III', d, p); p += 12
            raw = d[p:p + clen]; p += clen
            if enc == 1: raw = zlib.decompress(raw)
            v = list(struct.unpack('<%d%s' % (n, FMT[t]), raw))
        elif t in 'SR':
            n = struct.unpack_from('<I', d, p)[0]; p += 4
            v = d[p:p + n]; p += n
        else:
            raise ValueError('type %r' % t)
        return Prop(t, d[s:p], v), p

    def _node(self, d, p):
        if self.wide:
            end, np_, pl = struct.unpack_from('<QQQ', d, p); p += 24
        else:
            end, np_, pl = struct.unpack_from('<III', d, p); p += 12
        ln = d[p]; p += 1
        name = d[p:p + ln].decode(); p += ln
        if end == 0:
            return None, p
        props = []
        for _ in range(np_):
            pr, p = self._prop(d, p)
            props.append(pr)
        kids = []
        null = False
        while p < end:
            if end - p == self.nl and d[p:end] == b'\0' * self.nl:
                null = True
                p = end
                break
            k, p = self._node(d, p)
            if k is None:
                null = True
                break
            kids.append(k)
        return Node(name, props, kids, null), end

    def _write_node(self, out, n):
        start = len(out)
        hdr = 24 if self.wide else 12
        out += b'\0' * hdr
        nb = n.name.encode()
        out += bytes([len(nb)]) + nb
        ps = len(out)
        for pr in n.props:
            out += pr.encode()
        pl = len(out) - ps
        for k in n.kids:
            self._write_node(out, k)
        if n.null:
            out += b'\0' * self.nl
        end = len(out)
        if self.wide:
            struct.pack_into('<QQQ', out, start, end, len(n.props), pl)
        else:
            struct.pack_into('<III', out, start, end, len(n.props), pl)

    def save(self, path):
        out = bytearray(self.head)
        for n in self.nodes:
            self._write_node(out, n)
        out += b'\0' * self.nl
        out += self.footer_id
        ofs = len(out)
        pad = ((ofs + 15) & ~15) - ofs     # règle du SDK FBX : alignement sur 16 juste après l'identifiant,
        if pad == 0:                       # puis 4 zéros avant la version
            pad = 16
        out += b'\0' * (pad + 4)
        out += self.tail
        open(path, 'wb').write(bytes(out))
        return len(out)

    def top(self, name):
        for n in self.nodes:
            if n.name == name:
                return n
        return None


def verifier(path, tmp='/tmp/_fbx_rt.fbx'):
    f = FBX(path)
    f.save(tmp)
    a, b = open(path, 'rb').read(), open(tmp, 'rb').read()
    return a == b, len(a), len(b), f.pad_orig
