# Architecture technique — BALULA MOTO

## Principes

- **Règles en C# pur** (`Scripts/Core`, espace de noms `BalulaMoto.Core`) : aucune dépendance à Unity. Économie, taxis, négociation, satisfaction, missions, police, garage, atelier, carburant, profil et sauvegarde sont testables seuls.
- **Monde et rendu** (`Scripts/Runtime`, `BalulaMoto.Runtime`) : physique, construction des scènes, circulation, caméras, sons, interface.
- **Données éditables** : tout le contenu est en JSON (`Resources/BalulaData`). Des paquets complémentaires (`vehicules_*.json`, `missions_*.json`, `dialogues_*.json`, `lieux_*.json`) peuvent être ajoutés dans le dossier `contenu` des données de l'application sans recompiler.
- **Compatibilité** : Unity 6 et 2022.3 LTS (`#if UNITY_6000_0_OR_NEWER`), ancien et nouveau système d'entrée (`ENABLE_INPUT_SYSTEM` / `ENABLE_LEGACY_INPUT_MANAGER`), rendu URP (recommandé) ou intégré.

## Démarrage

`BalulaGame` (scène `BalulaMoto.unity`) charge les données, crée la caméra, l'environnement (soleil, météo), l'ambiance sonore, le monde — **carte OSM importée** (`ImportedWorld`) sinon **piste d'essai** — puis la **zone de Matadi** (`MatadiBuilder`) placée à l'écart, la circulation et l'interface (`UIManager`). Une partie crée une `GameSession` (règles) et un `GameplayDirector` (déroulement).

## Zones et voyages

Chaque lieu (`PlaceRef`) porte une **zone** (`kinshasa`, `matadi`). Les clients ne demandent que des destinations de leur zone. Le voyage Kinshasa ⇄ Matadi se fait par **Pause > Provinces et voyages** (province Kongo Central débloquée) et fait avancer l'horloge du jeu. La zone courante est enregistrée dans la sauvegarde.

## Véhicule

`RaycastVehicle` : roues par rayons (ressort, amortisseur, cercle d'adhérence), force motrice limitée par l'adhérence et la puissance, freins avant/arrière, résistance au roulement selon la surface (`SurfaceZone`), traînée aérodynamique, inclinaison visuelle de la moto, anti-roulis et retournement du tricycle. Les statistiques viennent de `VehicleStatsCalculator` (fiche + usure + améliorations).

Modèle 3D final : un prefab dans `Resources/` dont le chemin est indiqué dans `vehicles.json` (`prefabRessource`), avec un composant `VehicleRig` qui désigne la carrosserie, le pivot de direction, les roues, les sièges, les ancrages de caméra et les feux. Sans prefab, une maquette est générée.

## Interface

`UIManager` crée un canevas adapté aux encoches (zone sûre), le HUD (`HudView`) et les panneaux (`UIPanel`) : menu, nouvelle partie, pause, paramètres, crédits, missions, finances, provinces et voyages, magasin (aperçu 3D par caméra dédiée), garage (améliorations, personnalisation), négociation, fin de course, carburant, atelier, panne sèche, police, confirmation. Les panneaux modaux mettent le jeu en pause ; le bouton retour Android ferme le panneau du dessus quand c'est permis.

## Carte OpenStreetMap

`OsmImporterWindow` (éditeur) : lecture XML, projection équirectangulaire locale autour de l'origine choisie, routes par blocs (largeur selon le type, revêtement selon `surface` / `smoothness` et une règle optionnelle « rue résidentielle sans étiquette = terre »), bâtiments extrudés (`height`, `building:levels`), eau et verdure (anneaux extérieurs des multipolygones), arrêts, stations, lieux nommés rattachés au quartier le plus proche, boucles de circulation sur les grandes avenues, et lieux de référence du créateur (prioritaires). Les maillages et matériaux sont enregistrés dans `Assets/BalulaMoto/Generated/`.

## Sauvegarde

JSON atomique (fichier temporaire + copie de secours) dans les données persistantes de l'application, emplacements « carriere » et « libre », sauvegarde automatique chaque minute, à chaque course, au changement de jour et à la mise en arrière-plan.

## Vérifications effectuées

Sans compilateur Unity disponible, le code a été contrôlé par : analyse lexicale de tous les fichiers C# (équilibre des délimiteurs, membres appelés connus du projet ou de l'API Unity/.NET, nombre d'arguments des méthodes du projet), contrôle des JSON contre les classes C#, et simulations des formules (tarifs, négociation, accélérations). La première compilation dans Unity reste à faire.
