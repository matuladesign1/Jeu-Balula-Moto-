#!/usr/bin/env python3
"""Lecteur minimal de FBX binaire (nœuds, propriétés, tableaux compressés)."""
import struct, zlib, sys

def read(path):
    d = open(path, 'rb').read()
    assert d[:20] == b'Kaydara FBX Binary  ', 'pas un FBX binaire'
    ver = struct.unpack_from('<I', d, 23)[0]
    wide = ver >= 7500
    pos = 27

    def prop(p):
        t = chr(d[p]); p += 1
        if t == 'Y': return struct.unpack_from('<h', d, p)[0], p + 2
        if t == 'C': return d[p] != 0, p + 1
        if t == 'I': return struct.unpack_from('<i', d, p)[0], p + 4
        if t == 'F': return struct.unpack_from('<f', d, p)[0], p + 4
        if t == 'D': return struct.unpack_from('<d', d, p)[0], p + 8
        if t == 'L': return struct.unpack_from('<q', d, p)[0], p + 8
        if t in 'fdlib':
            n, enc, clen = struct.unpack_from('<III', d, p); p += 12
            raw = d[p:p + clen]; p += clen
            if enc == 1: raw = zlib.decompress(raw)
            fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B'}[t]
            return list(struct.unpack('<%d%s' % (n, fmt), raw)), p
        if t in 'SR':
            n = struct.unpack_from('<I', d, p)[0]; p += 4
            v = d[p:p + n]; p += n
            return (v.decode('utf-8', 'replace') if t == 'S' else v), p
        raise ValueError('type ' + t)

    def node(p):
        if wide:
            end, nprops, plen = struct.unpack_from('<QQQ', d, p); p += 24
        else:
            end, nprops, plen = struct.unpack_from('<III', d, p); p += 12
        nl = d[p]; p += 1
        name = d[p:p + nl].decode(); p += nl
        if end == 0: return None, p
        props = []
        for _ in range(nprops):
            v, p = prop(p); props.append(v)
        kids = []
        null = 25 if wide else 13
        while p < end - null or (p < end and d[p:p + null] != b'\x00' * null):
            if p >= end: break
            k, p2 = node(p)
            if k is None: p = p2; break
            kids.append(k); p = p2
        return {'name': name, 'props': props, 'kids': kids}, end

    nodes = []
    while pos < len(d):
        n, pos2 = node(pos)
        if n is None: break
        nodes.append(n); pos = pos2
    return nodes

def find(n, name):
    for k in n['kids']:
        if k['name'] == name: return k
    return None

def findall(n, name):
    return [k for k in n['kids'] if k['name'] == name]

if __name__ == '__main__':
    for n in read(sys.argv[1]): print(n['name'], [str(x)[:40] for x in n['props']][:3], len(n['kids']))
