#!/usr/bin/env bash
# BALULA MOTO — téléchargement de la carte réelle de Kinshasa (OpenStreetMap, licence ODbL) pour la compilation.
# Usage : scripts/fetch_osm.sh <sortie.osm> [lat] [lon] [rayon_routes_m] [rayon_batiments_m]
# Données © les contributeurs d'OpenStreetMap — https://www.openstreetmap.org/copyright
set -u
OUT="${1:-osm/kinshasa.osm}"
LAT="${2:--4.32194}"
LON="${3:-15.31194}"
R_ROADS="${4:-2600}"
R_BUILD="${5:-1700}"
mkdir -p "$(dirname "$OUT")"

read -r S W N E <<<"$(python3 -c "
import math
lat,lon,r=$LAT,$LON,$R_ROADS
dl=r/110574.0; dn=r/(111320.0*math.cos(math.radians(lat)))
print(f'{lat-dl:.5f} {lon-dn:.5f} {lat+dl:.5f} {lon+dn:.5f}')")"
read -r S2 W2 N2 E2 <<<"$(python3 -c "
import math
lat,lon,r=$LAT,$LON,$R_BUILD
dl=r/110574.0; dn=r/(111320.0*math.cos(math.radians(lat)))
print(f'{lat-dl:.5f} {lon-dn:.5f} {lat+dl:.5f} {lon+dn:.5f}')")"

BB="$S,$W,$N,$E"
BB2="$S2,$W2,$N2,$E2"
QUERY="[out:xml][timeout:300];
(
  way[\"highway\"]($BB);
  way[\"building\"]($BB2);
  way[\"natural\"=\"water\"]($BB);
  way[\"waterway\"=\"riverbank\"]($BB);
  way[\"leisure\"~\"park|pitch|stadium|garden|playground\"]($BB);
  way[\"landuse\"~\"grass|forest|recreation_ground|cemetery|meadow\"]($BB);
  relation[\"natural\"=\"water\"]($BB);
  node[\"amenity\"]($BB);
  node[\"shop\"]($BB);
  node[\"highway\"=\"bus_stop\"]($BB);
  node[\"public_transport\"]($BB);
  node[\"place\"]($BB);
  node[\"tourism\"]($BB);
  node[\"historic\"]($BB);
  node[\"leisure\"]($BB);
);
(._;>;);
out body;"

for SERVER in https://overpass-api.de/api/interpreter https://overpass.kumi.systems/api/interpreter https://overpass.private.coffee/api/interpreter; do
  echo "Téléchargement OSM depuis $SERVER (routes $BB, bâtiments $BB2)…"
  if curl -sS -f --retry 2 --max-time 600 -A "BalulaMoto-CI (github.com/matuladesign1)" \
       --data-urlencode "data=$QUERY" -o "$OUT.tmp" "$SERVER"; then
    if [ -s "$OUT.tmp" ] && grep -q "<osm" "$OUT.tmp" && grep -q "<way" "$OUT.tmp"; then
      mv "$OUT.tmp" "$OUT"
      echo "Carte OSM prête : $OUT ($(du -h "$OUT" | cut -f1))"
      exit 0
    fi
  fi
  echo "Échec avec $SERVER, essai suivant…"
  sleep 5
done
rm -f "$OUT.tmp"
echo "Impossible de télécharger la carte OSM : la piste d'essai sera utilisée."
exit 0
