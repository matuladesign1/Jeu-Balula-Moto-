#!/usr/bin/env python3
# BALULA MOTO — extrait compact de la carte OpenStreetMap utilisée par le jeu (licence ODbL), pour analyser les
# boulevards, ronds-points, espaces verts, terrains de sport et lieux nommés sans relancer Unity.
# Usage : python3 scripts/extrait_osm.py osm/kinshasa.osm dossier_sortie
import json, os, sys
import xml.etree.ElementTree as ET

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
nodes = {}
named_nodes = []
ways = []
rels = []
for ev, el in ET.iterparse(src, events=("end",)):
    tag = el.tag
    if tag == "node":
        nid = int(el.get("id"))
        lat, lon = float(el.get("lat")), float(el.get("lon"))
        nodes[nid] = (lat, lon)
        tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
        if tags and ("name" in tags or "amenity" in tags or "leisure" in tags or "place" in tags or "tourism" in tags or "barrier" in tags):
            named_nodes.append({"id": nid, "lat": lat, "lon": lon, "t": tags})
        el.clear()
    elif tag == "way":
        tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
        refs = [int(n.get("ref")) for n in el.findall("nd")]
        if "building" in tags and not ("name" in tags or "amenity" in tags or "leisure" in tags):
            el.clear()
            continue
        ways.append((int(el.get("id")), tags, refs))
        el.clear()
    elif tag == "relation":
        tags = {t.get("k"): t.get("v") for t in el.findall("tag")}
        mem = [(m.get("type"), int(m.get("ref")), m.get("role")) for m in el.findall("member")]
        rels.append({"id": int(el.get("id")), "t": tags, "m": mem})
        el.clear()

def pts(refs):
    return [[round(nodes[r][0], 7), round(nodes[r][1], 7)] for r in refs if r in nodes]

roads, areas, named_ways = [], [], []
for wid, tags, refs in ways:
    p = pts(refs)
    if len(p) < 2:
        continue
    if "highway" in tags:
        roads.append({"id": wid, "t": tags, "p": p})
    elif any(k in tags for k in ("leisure", "landuse", "natural", "amenity", "place", "barrier", "building")):
        areas.append({"id": wid, "t": tags, "p": p})
json.dump(roads, open(os.path.join(out, "routes.json"), "w"), ensure_ascii=False, separators=(",", ":"))
json.dump(areas, open(os.path.join(out, "zones.json"), "w"), ensure_ascii=False, separators=(",", ":"))
json.dump(named_nodes, open(os.path.join(out, "lieux.json"), "w"), ensure_ascii=False, separators=(",", ":"))
json.dump(rels, open(os.path.join(out, "relations.json"), "w"), ensure_ascii=False, separators=(",", ":"))
print("noeuds", len(nodes), "routes", len(roads), "zones", len(areas), "lieux", len(named_nodes), "relations", len(rels))
