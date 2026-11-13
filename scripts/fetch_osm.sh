#!/usr/bin/env bash
# BALULA MOTO — téléchargement de la carte réelle de Kinshasa (OpenStreetMap, licence ODbL) pour la compilation.
# Usage : scripts/fetch_osm.sh <sortie.osm> [lat] [lon] [rayon_routes_m] [rayon_batiments_m]
# Données © les contributeurs d'OpenStreetMap — https://www.openstreetmap.org/copyright
#
# 1) Méthode principale : extrait « RDC » de Geofabrik (fichier .osm.pbf mis à jour chaque jour), découpé
#    avec osmium : rues, eau, verdure et lieux dans la grande zone, bâtiments dans la zone centrale.
#    Fiable pour une grande zone (les serveurs Overpass publics refusent les requêtes trop lourdes).
# 2) Secours : serveurs Overpass, sur une zone plus petite.
# La carte n'est téléchargée qu'une fois par mois (cache de la compilation).
set -u
OUT="${1:-osm/kinshasa.osm}"
LAT="${2:--4.3376}"
LON="${3:-15.3317}"
R_ROADS="${4:-4900}"
R_BUILD="${5:-1900}"
mkdir -p "$(dirname "$OUT")"
WORK="$(mktemp -d)"

bbox() {
  python3 -c "
import math
lat,lon,r=$1,$2,$3
dl=r/110574.0; dn=r/(111320.0*math.cos(math.radians(lat)))
print(f'{lat-dl:.5f} {lon-dn:.5f} {lat+dl:.5f} {lon+dn:.5f}')"
}
read -r S W N E <<<"$(bbox "$LAT" "$LON" "$R_ROADS")"
read -r S2 W2 N2 E2 <<<"$(bbox "$LAT" "$LON" "$R_BUILD")"

valid() { [ -s "$1" ] && grep -q "<osm" "$1" && grep -q "<way" "$1"; }

# ---------------------------------------------------------------- 1) Geofabrik + osmium
geofabrik() {
  if ! command -v osmium >/dev/null 2>&1; then
    echo "Installation d'osmium-tool…"
    (sudo apt-get update -qq && sudo apt-get install -y -qq osmium-tool) >/dev/null 2>&1 || return 1
  fi
  command -v osmium >/dev/null 2>&1 || return 1
  local PBF="$WORK/rdc.osm.pbf"
  echo "Téléchargement de l'extrait OpenStreetMap de la RDC (Geofabrik)…"
  curl -sSfL --retry 3 --retry-delay 10 --max-time 1500 -A "BalulaMoto-CI (github.com/matuladesign1)" \
       -o "$PBF" "https://download.geofabrik.de/africa/congo-democratic-republic-latest.osm.pbf" || return 1
  echo "Extrait téléchargé ($(du -h "$PBF" | cut -f1)). Découpage : rues $W,$S,$E,$N ; bâtiments $W2,$S2,$E2,$N2"
  osmium extract -b "$W,$S,$E,$N" "$PBF" -o "$WORK/zone.osm.pbf" --overwrite || return 1
  osmium extract -b "$W2,$S2,$E2,$N2" "$PBF" -o "$WORK/centre.osm.pbf" --overwrite || return 1
  rm -f "$PBF"
  osmium tags-filter "$WORK/zone.osm.pbf" \
      w/highway w/natural=water w/waterway=riverbank \
      w/leisure=park,pitch,stadium,garden,playground \
      w/landuse=grass,forest,recreation_ground,cemetery,meadow \
      r/natural=water \
      n/amenity n/shop n/highway=bus_stop n/public_transport n/place n/tourism n/historic n/leisure \
      -o "$WORK/rues.osm.pbf" --overwrite || return 1
  osmium tags-filter "$WORK/centre.osm.pbf" w/building -o "$WORK/batiments.osm.pbf" --overwrite || return 1
  osmium merge "$WORK/rues.osm.pbf" "$WORK/batiments.osm.pbf" -o "$WORK/kinshasa.osm" --overwrite || return 1
  valid "$WORK/kinshasa.osm" || return 1
  mv "$WORK/kinshasa.osm" "$OUT"
  return 0
}

if geofabrik; then
  echo "Carte OSM prête (Geofabrik) : $OUT ($(du -h "$OUT" | cut -f1))"
  rm -rf "$WORK"
  exit 0
fi
echo "Extrait Geofabrik indisponible : essai des serveurs Overpass (zone réduite)."

# ---------------------------------------------------------------- 2) Overpass (zone réduite)
read -r S W N E <<<"$(bbox "$LAT" "$LON" 3000)"
read -r S2 W2 N2 E2 <<<"$(bbox "$LAT" "$LON" 1500)"
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
  if curl -sS -f --retry 1 --max-time 360 -A "BalulaMoto-CI (github.com/matuladesign1)" \
       --data-urlencode "data=$QUERY" -o "$WORK/overpass.osm" "$SERVER"; then
    if valid "$WORK/overpass.osm"; then
      mv "$WORK/overpass.osm" "$OUT"
      echo "Carte OSM prête (Overpass, zone réduite) : $OUT ($(du -h "$OUT" | cut -f1))"
      rm -rf "$WORK"
      exit 0
    fi
  fi
  echo "Échec avec $SERVER, essai suivant…"
  sleep 5
done
rm -rf "$WORK"
echo "Impossible de télécharger la carte OSM : la piste d'essai sera utilisée."
exit 0
