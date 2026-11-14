# BALULA MOTO — projet Unity (C#)

Simulateur de taxi-moto et de tricycle en RDC — **MATULA DESIGN**.
Ce dossier contient le code source complet du jeu (Unity 3D + C#), ses données (véhicules, lieux, missions, dialogues français / lingala, économie en francs congolais), le logo de l'application et les outils de l'éditeur (préparation du projet, construction de l'APK, import de la carte réelle OpenStreetMap).

> **L'APK sans Unity sur votre ordinateur.** Ce dépôt se compile tout seul dans le cloud (GitHub Actions) : il suffit d'ajouter une fois deux secrets (e-mail et mot de passe de votre compte Unity gratuit), puis l'APK apparaît dans l'onglet **Releases** du dépôt, téléchargeable directement sur le téléphone. Guide : [`Docs/COMPILER_APK.md`](Docs/COMPILER_APK.md).

## Ce qui est jouable

- **Menu principal (garage)** : Rouler (continuer), Nouvelle partie, Acheter, Améliorer, Personnaliser, Chauffeur, Missions, Garage, Mode libre, Paramètres, Crédits, Quitter.
- **Deux véhicules de départ** : moto TVS HLX 150 bleue (REF-KIN-001) ou tricycle jaune et noir 5 places : 2 à côté du chauffeur + 3 derrière (REF-KIN-002).
- **Conduite** : physique à roues (suspension, adhérence selon goudron / terre / boue / poussière / nids-de-poule / pluie), freins avant et arrière séparés sur moto, marche arrière (tricycle) et recul aux pieds (moto), chutes et tricycle renversé, usure progressive.
- **Clients** : ils font signe aux arrêts ; on négocie le prix (« Combien jusqu'à… ? »), en français, lingala ou mixte ; contre-offres, clients qui partent ; note de 1 à 5 étoiles selon le prix, le temps, la sécurité, la conduite, le confort et le respect ; pourboires.
- **Économie en FC** : carburant (station, point de vente, Kadafi avec risque de mauvaise qualité), panne sèche (Kadafi livré ou pousser), atelier (moteur, pneus, freins, vidange, suspension, lavage, carrosserie), documents du véhicule (fictifs).
- **Magasin** : 10 catégories, 15 modèles, fiche technique, **aperçu 3D tournant**, comparaison avec votre véhicule, favoris. Acheter n'oblige **jamais** à vendre.
- **Garage** : tous vos véhicules, mise en service, vente (facultative), **améliorations mécaniques** qui changent vraiment les performances, **personnalisation** (couleurs, jantes, selle, phares, autocollants, accessoires).
- **Circulation** : taxis jaunes, minibus, bus de ligne, motos, camions, feux tricolores, robot roulage, piétons, pannes, klaxons.
- **Police** : feu rouge, excès de vitesse, sans phare la nuit, contrôle des documents, amendes fictives avec quittance, contestation polie, poursuite par le bus Kabasele en cas de fuite.
- **Carrière** : niveaux et XP, réputation, 26 missions, 26 provinces à débloquer, finances et historique.
- **Jour / nuit et météo** (saison des pluies), éclairage public, pluie et orage.
- **Caméras** : arrière fixe, rapprochée, 1re personne, tableau de bord (tricycle), libre (glisser le doigt).
- **Carte** : mini-carte et carte plein écran avec légende.
- **Sauvegardes automatiques** (carrière et libre séparées).

### Nouveautés de la version 4.3.1

- **Tricycle Bajaj RE de MATULA DESIGN** : le tricycle « Bajaj RE » du joueur et tous les tricycles de la circulation utilisent désormais le modèle fourni (caisse bleue, bâche crème, vitres, rétroviseurs, feux, plaque jaune). Hauteur ramenée à celle du vrai Bajaj RE (1,71 m), roues au diamètre réel, intérieur aménagé pour le chauffeur et 5 passagers assis ; l'essuie-glace balaie le pare-brise quand il pleut.

### Nouveautés de la version 4.3

- **Policiers dans les rues** : agents à pied aux carrefours (dessin fourni par MATULA DESIGN). Si vous bousculez un piéton ou accrochez une voiture devant un agent, il siffle, vient au véhicule, vous payez l'amende et vous repartez.
- **Bus TRANSCO** (modèle 3D fourni par MATULA DESIGN) dans toute la circulation, avec arrêts et passagers. Design respecté, sans partenariat officiel.
- **Rivières en tranchée** : eau turquoise plus bas que le sol, berges en pente d'herbe et de terre, parois de béton pour les canaux et caniveaux. Plus aucune maison ni clôture sur l'eau ou sur les berges ; **un pont (trottoirs, parapets) partout où une rue croise l'eau** ; on ne peut plus rouler dans le lit.
- **Murs de parcelles bruts** : parpaings, ciment gris sale, chaux tachée, inscriptions peintes « Cette parcelle n'est pas à vendre », barbelé en spirale sur certains murs.
- **Embouteillages** sur quelques rues goudronnées, à toute heure : longues files serrées de minibus et taxis jaunes, motos-taxis et voitures.
- **Esplanades dégagées** : plus de maisons au bord de l'espace de l'Échangeur ni autour du Stade des Martyrs.

### Nouveautés de la version 4.2

- **Tous les personnages ont un teint africain** : 21 avatars (5 nouveaux, dont 3 femmes) ; la peau claire des avatars du
  pack Rocketbox a été assombrie vers cinq teints bruns à très foncés (`scripts/africaniser_avatars.py`), cheveux foncés.
  La forme des visages reste celle du pack.
- **Tricycle corrigé** : il se couchait sur un côté au démarrage (la barre anti-roulis arrière poussait dans le mauvais
  sens). Il reste maintenant à plat comme un vrai trois-roues (roulis mesuré : 0° au démarrage, 1° en virage).
- **Noms des rues** : sur la carte du jeu (ils apparaissent en zoomant) et sur des **plaques bleues à lettres blanches**
  à chaque carrefour entre deux rues nommées dans OpenStreetMap. Une rue sans nom dans OpenStreetMap reste sans plaque.
- **Poteaux électriques, fils et lampadaires** le long de toutes les rues ; **la nuit**, lampadaires et flaques de
  lumière sur la chaussée, fenêtres et ampoules allumées dans les parcelles et les bâtiments, enseignes des boutiques.
- **Phares** : chaque véhicule de la circulation allume ses phares et ses feux arrière quand il fait noir (faisceau sur
  la route) ; les phares du joueur s'allument aussi tout seuls à la tombée de la nuit.
- **Rivières, ruisseaux et canaux** d'OpenStreetMap (tracés réels, eau sombre et trouble, berges de boue) et **ponts** : les
  tronçons de route marqués « pont » dans OpenStreetMap ont des parapets et des garde-corps.
- **Circulation dense** : environ 80 véhicules autour du joueur (100 aux heures de pointe), surtout sur les avenues et
  le goudron ; chaque véhicule est allégé (pièces fusionnées) pour tenir sur téléphone.
- **Taxis, minibus, bus, motos-taxis et tricycles** s'arrêtent aux arrêts (feux de détresse, rangés sur le côté) :
  des clients montent, d'autres descendent.
- **Foules aux arrêts** : jusqu'à 25 personnes de chaque côté de la rue (18 en qualité moyenne, 10 en qualité basse).
- **Monuments de MATULA DESIGN** : Stade des Martyrs et Tour de l'Échangeur (modèles fournis, convertis en OBJ par
  `scripts/convertir_monuments.py`), posés aux repères envoyés par le créateur, sur un terrain clôturé de barreaux de
  fer espacés (on voit les arbres et le monument à travers), avec pelouse, arbres et un portail côté rue.

### Nouveautés de la version 4.1

- **Personnages vraiment visibles** (chauffeur, piétons, passagers, chauffeur du garage). Cause trouvée grâce à des photos prises automatiquement dans le jeu : nos avatars n'ont qu'un niveau de détail, qui n'était dessiné que lorsqu'il occupait la moitié de l'écran (à moins de 3 m environ) ; au-delà, seul le casque ou le foulard restait affiché. Le seuil est corrigé (personnages visibles jusqu'à environ 140 m).
- **Véhicules entiers** : vitres, montants et toit des taxis, minibus, bus, berlines et camions manquaient (un calcul donnait une valeur invalide et Unity rejetait toute la partie haute). Corrigé.
- **Téléphones Android** : affichage en OpenGL ES 3 seulement (les pilotes Vulkan de nombreux téléphones à petit prix dessinent mal certains objets), sans instanciation GPU, personnages animés par le processeur.
- **Musique de fond originale** : quatre morceaux instrumentaux (rumba, sebene, ndombolo) composés pour le jeu et générés par programme, enchaînés en fondu ; réglage **Paramètres > Musique** (0 % = coupée). L'ancien bruit de ville en boucle de 3 s (qui « accrochait ») est remplacé par une ambiance plus longue, seulement pendant la partie.
- **Stations-service** comme sur vos photos : auvent blanc à bande rouge, îlots de pompes, distributeurs, boutique vitrée, totem avec le prix du litre, éclairage la nuit. Marque **fictive « BALULA ÉNERGIE »** (aucun logo réel). Chaque station est posée au bord de la rue la plus proche et face à elle.
- **Nouveaux véhicules** dans la circulation : **camion-citerne** (générique, sans marque), pick-up double cabine (souvent chargé de sacs), 4x4, tricycle-taxi.
- **Courses** : surtout des petites courses de 1 à 2 km (65 %), des moyennes (25 %) et quelques longues (10 %, jamais plus de 9 km de route environ) ; prix au kilomètre un peu réduit.
- **Routes en travaux** (une quarantaine, réparties dans la ville) : chaussée défoncée (sable, gravier, bosses), barrières rouge et blanc, cônes, tas de sable qui mordent sur la route, pelleteuse, panneaux « TRAVAUX — RALENTIR », ouvriers ; la circulation y roule au pas.
- **Caniveaux** en béton des deux côtés des rues goudronnées et des grandes avenues, **accotements** de terre tassée, **bordures peintes rouge et blanc** sur les boulevards. Rouler dans un caniveau secoue et freine la moto.
- **Terre marron foncé, sans reflets** : le sol est découpé en dalles (sur téléphone, un sol d'une seule pièce apparaissait bleu-gris clair, noyé dans le brouillard) ; le sol, les routes et les caniveaux de la carte sont mats (ils brillaient comme du plastique et reflétaient le ciel, ce qui éclaircissait la terre).
- **Textes 3D** (plaques, enseignes, inscriptions des camions, panneaux) : ils ne se voient plus à travers les véhicules et les murs, ni à l'envers depuis l'autre côté.
- **« Tunnels »** : les bâtiments OpenStreetMap posés en travers d'une chaussée (on roulait à travers leurs murs) ne sont plus construits.
- **Paramètres** : le modèle du téléphone et sa carte graphique sont affichés en bas (utile pour me signaler un problème d'affichage).

### Nouveautés de la version 4.0

- **Menu « garage »** (comme la première photo d'exemple) : garage 3D (briques, rideau métallique, établi, pneus, fûts, néons, affiches), votre moto exposée et votre **chauffeur debout à côté** ; tuiles **ACHETER**, **AMÉLIORER**, **PERSONNALISER**, **CHAUFFEUR**, **MISSIONS**, bouton **GARAGE**, argent, niveau, jauge de carburant et grand bouton **ROULER**.
- **Choix du chauffeur** : 11 personnages (hommes et femmes) ; celui choisi conduit votre moto ou votre tricycle.
- **Personnages enfin visibles** : conducteurs dans les voitures, motards, passagers et piétons (correction des niveaux de détail qui les cachaient ; seul le casque restait visible). La marche ne « glisse » plus.
- **Ville pleine** : parcelles **clôturées** le long de toutes les rues (murs, portails métalliques, maisons à toit de tôle ou en béton, manguiers), boutiques avec rideaux et enseignes et petits immeubles au bord des avenues, sans vide entre les quartiers. Tout est **solide** : la moto ne traverse plus les maisons ni les murs, et la caméra ne passe plus à travers.
- **Panneaux publicitaires** au bord des avenues (affiches de marques **fictives**, dont BALULA MOTO et MATULA DESIGN ; aucune marque réelle).
- **Circulation dense et embouteillages** aux heures de pointe : files derrière un bus, un camion ou un minibus qui avance au pas, klaxons.
- **Caméra fixe** qui suit la moto (plus de rotation au doigt pendant la conduite ; caméra libre toujours disponible), 1re personne avec guidon et rétroviseurs.
- **Bouton ARRIÈRE** : marche arrière du tricycle et recul « aux pieds » de la moto.
- **Carte** : mini-carte ronde (cap en haut) et **carte plein écran** avec légende : clients qui attendent, arrêts, stations-service, garages, ateliers, magasin, lieux de référence, contrôles de police, destination du client.
- **Carte de Kinshasa agrandie** (rayon d'environ 4,8 km centré sur Kalamu / Kasa-Vubu : de la Gombe à Limete et Lemba) et **lieux de vos liens Google Maps** (coordonnées seulement) : Place de la Gare, station-service de l'avenue de l'Université, deux lieux dont le nom reste à préciser, Nsele (hors de la zone actuelle).
- **Signature stable** de l'APK : les prochaines versions s'installeront par-dessus celle-ci sans perdre les sauvegardes (clé de test du projet, à remplacer par une clé privée avant le Play Store).

### Nouveautés de la version 0.3 (Unity)

- **Personnages 3D réalistes** : 16 avatars humains scannés et animés (Microsoft Rocketbox, licence MIT) — hommes, femmes, enfants, ouvriers, infirmière, policier — choisis en priorité parmi les personnes à la peau foncée du pack. Animations de marche, attente, téléphone, discussion, signe de la main pour héler un taxi, position assise, accroupie, parapluie. Les clients, passagers, conducteurs, policiers et piétons utilisent ces personnages (les volumes simples restent en secours).
- **Conducteurs et passagers posés sur les véhicules** : mains sur le guidon et pieds sur les repose-pieds par cinématique inverse, casque pour certains motards, passagère assise à califourchon.
- **Foule dense** autour du joueur : piétons sur les trottoirs (y compris ceux de la vraie carte OpenStreetMap), groupes qui discutent, vendeuses avec bassine sur la tête ou accroupies devant leur marchandise, foulards colorés, parapluies sous la pluie ; plus de monde aux heures de pointe, moins la nuit ; les piétons s'écartent quand une moto arrive.
- **Circulation plus belle et plus dense** : carrosseries lisses générées (taxis jaunes, berlines, minibus jaunes « 207 », minibus blancs, bus, pick-up de police, camions) avec passages de roue, pare-brise incliné, montants, vitres teintées transparentes, rétroviseurs, galeries de toit chargées ; plus de motos-taxis, avec de vrais motards.
- **Vraie carte de Kinshasa** importée automatiquement pendant la compilation cloud (OpenStreetMap, ODbL) : rues, bâtiments, arrêts, stations, lieux nommés. Les lieux de vos photos dont le nom est identique dans OpenStreetMap sont signalés sur la carte avec la mention « à vérifier par le créateur » ; les noms seulement proches restent dans le rapport d’import. Aucune position n’est inventée.
- **Compilation de l'APK dans le cloud** (GitHub Actions + Unity 6, licence Personal).

### Nouveautés de la version 0.2

- **Logo** de l'application (REF-APP-001) : icône Android / iOS, écran de démarrage et menu principal.
- **Zone de Matadi — Pont Maréchal** (province du Kongo Central) : maquette jouable du pont suspendu sur le fleuve Congo, avec longueur 722 m et travée centrale de 520 m (sources publiques), pylônes, câbles, suspentes, poutre en treillis, têtes de pont, arrêts, Kadafi, circulation et deux missions (« Traverser le Pont Maréchal », « Taxi sur le Pont Maréchal »). Accès : **Pause > Provinces et voyages**, après déblocage du Kongo Central (immédiat en mode libre). Hauteurs, largeur et relief sont **estimés, à vérifier**.
- **Interface tactile** disposée comme dans les jeux d'exemple fournis (REF-UX-001 à 003) : jauge de carburant en haut à gauche, pause en haut à droite, flèches de direction rondes en bas à gauche, pédales frein / accélérateur en bas à droite, klaxon rond à droite.

## Obtenir l'APK

**Sans Unity (recommandé)** — compilation dans le cloud :
1. Sur GitHub, dans ce dépôt : **Settings > Secrets and variables > Actions > New repository secret**, créer `UNITY_USERNAME` (e-mail du compte Unity) et `UNITY_PASSWORD` (mot de passe).
2. Onglet **Actions > Construire l'APK Android > Run workflow** (ou simplement pousser une modification).
3. 40 à 70 minutes plus tard, l'APK est dans **Releases** (dernière version) : l'ouvrir sur le téléphone et l'installer.

**Avec Unity sur un ordinateur** : ouvrir ce dossier avec Unity 6 (Unity Hub > Add > dossier du dépôt), menu **BALULA MOTO > 1. Préparer le projet**, puis **BALULA MOTO > 2. Construire l'APK Android**.

Détails, conseils et solutions aux erreurs : [`Docs/COMPILER_APK.md`](Docs/COMPILER_APK.md).

### Commandes au clavier (test sur ordinateur)

| Action | Touches |
|---|---|
| Accélérer / freiner (avant) | ↑ ou W / ↓ ou S |
| Frein arrière (moto) | Espace |
| Tourner | ← → ou A D |
| Klaxon / phares / caméra | H / L / C |
| Action (client, station, atelier…) | F ou Entrée |
| Clignotants | Q / E |
| Marche arrière / recul | R |
| Missions / pause | M / Échap (bouton retour sur Android) |

## Carte réelle de Kinshasa

La compilation cloud télécharge et importe automatiquement Kinshasa sur un rayon d'environ 4,8 km autour de Kalamu / Kasa-Vubu (bâtiments OpenStreetMap sur environ 2,6 km, puis parcelles génériques le long de toutes les rues). La carte vient de l'extrait OpenStreetMap de la RDC publié par Geofabrik (téléchargé une fois par mois), découpé avec osmium ; si elle est introuvable, la compilation cloud s'arrête au lieu de publier un APK sans Kinshasa. Sans carte importée (Unity sur ordinateur), le jeu démarre sur une **piste d'essai** clairement signalée comme n'étant **pas** Kinshasa. Pour importer une autre zone à la main :
**BALULA MOTO > 4. Importer une carte OpenStreetMap…** — choisir un fichier `.osm` exporté depuis openstreetmap.org (quartier de 1,5 à 3 km de rayon conseillé pour un téléphone). L'outil reprend la géométrie OSM telle quelle (aucune avenue déplacée), les revêtements, les bâtiments, l'eau, les arrêts, les stations, les lieux nommés et crée des boucles de circulation sur les grandes avenues. Les lieux de référence du créateur gardent la priorité ; les correspondances OSM ne sont que des **suggestions à confirmer** (rapport dans le composant `ImportedWorld`).
Données © les contributeurs d'OpenStreetMap, licence ODbL (attribution affichée dans les crédits).

## Limites actuelles (honnêtes)

- **Relief (Mont-Ngafula, Mont-Ngaliema)** : pas encore fait. La carte est plate ; les collines demandent des données d'altitude et l'adaptation des rues, des parcelles et de la circulation à la pente. De plus, ces deux quartiers sont en dehors de la zone actuelle (ouest et sud-ouest). Prévu pour une prochaine version.
- **Parcelles et boutiques** : ce sont des constructions **génériques** placées entre les rues réelles (jamais des bâtiments identifiés) ; les vrais bâtiments OSM restent au centre de la carte.

- **Compilation** : le code est vérifié par analyse statique ; la compilation Unity se fait dans le cloud. Les éventuelles erreurs apparaissent dans le journal GitHub Actions et sont corrigées au fur et à mesure.
- **Personnages** : réalistes (Rocketbox), mais le pack libre contient peu de femmes africaines (3 modèles adultes) : les variantes viennent des foulards, bassines, animations et tailles. Des modèles supplémentaires (scans ou achats sous licence) pourront être ajoutés dans `ThirdParty`.
- **Véhicules et bâtiments** : carrosseries lisses générées par le code (pas de modèles de marque) ; bâtiments OSM en volumes simples. Chaque véhicule du joueur a un emplacement prévu (`prefabRessource`) pour brancher un modèle Blender final (voir `Docs/FEUILLE_DE_ROUTE.md`).
- **Sons** de synthèse (moteurs, klaxons, ambiance) ; **musique** originale générée par programme pour le jeu (aucune chanson commerciale). Des morceaux enregistrés par des musiciens pourront remplacer les fichiers de `Resources/BalulaMoto/Musique` (licence à obtenir).
- **Marques** : les noms de modèles réels (TVS, Bajaj, Honda, Yamaha…) devront être licenciés ou remplacés par des noms fictifs avant une sortie commerciale. Je ne suis pas juriste : faites valider ce point.
- **Prix et taux** (carburant, change, tarifs) : valeurs datées dans `economy.json`, à mettre à jour.

## Organisation

```
Assets/BalulaMoto/
  Scripts/Core/      règles du jeu en C# pur (économie, taxis, négociation, missions, police, garage, sauvegarde)
  Scripts/Runtime/   monde 3D, physique, circulation, caméras, sons, interface (UI/), zone de Matadi
  Editor/            préparation du projet, construction de l'APK, import OpenStreetMap
  Resources/BalulaData/  données JSON (véhicules, lieux, provinces, missions, dialogues, économie, références)
  Resources/BalulaMoto/  logo (menu) ; matériaux créés par « Préparer le projet »
  Art/               icône 1024 px et écran de démarrage
  ThirdParty/Rocketbox/  avatars humains et animations (Microsoft, licence MIT), textures réduites pour téléphone
scripts/fetch_osm.sh   téléchargement de la carte OSM de Kinshasa pendant la compilation
Docs/                guide APK, bibliothèque de références, architecture, feuille de route
.github/workflows/   compilation automatique de l'APK sur GitHub (Unity 6 dans le cloud)
```

Contenu ajouté sans modifier le jeu : déposez des fichiers `vehicules_*.json`, `missions_*.json`, `dialogues_*.json` ou `lieux_*.json` dans le dossier `contenu` des données de l'application.
