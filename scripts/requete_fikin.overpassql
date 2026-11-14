[out:json][timeout:120];
(
  nwr["name"~"[Ff][Ii][Kk][Ii][Nn]"](-4.43,15.29,-4.32,15.43);
  nwr["name"~"[Mm][Oo][Tt][Ee][Ll]"](-4.43,15.29,-4.32,15.43);
  way["landuse"="residential"]["name"](-4.41,15.33,-4.35,15.40);
  relation["landuse"="residential"]["name"](-4.41,15.33,-4.35,15.40);
  nwr["place"](-4.41,15.33,-4.35,15.40);
  nwr["amenity"="school"](-4.41,15.33,-4.35,15.40);
  nwr["leisure"~"pitch|park|sports_centre|stadium|playground"](around:900,-4.330275,15.31035);
  nwr["leisure"~"pitch|park|stadium"](around:700,-4.374389,15.345417);
  way["junction"="roundabout"](-4.43,15.27,-4.29,15.40);
);
out geom tags;
