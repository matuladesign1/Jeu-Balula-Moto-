# Feuille de route — BALULA MOTO

## 1. Première compilation (priorité immédiate)
- Ouvrir le projet dans Unity 6 (modèle Universal 3D), lancer « Préparer le projet », corriger les éventuelles erreurs de compilation signalées.
- Construire un premier APK, tester sur 2 ou 3 téléphones Android courants à Kinshasa (gamme d'entrée et moyenne) ; régler la qualité et la densité de circulation.

## 2. Carte réelle de Kinshasa
- Importer un premier quartier (Victoire / Matonge / Kalamu ou Gombe) avec l'outil OSM ; confirmer les références REF-KIN à valider (voir `BIBLIOTHEQUE_REFERENCES.md`).
- Placer le garage, l'atelier, la concession et les stations à leurs vrais emplacements (choix du créateur).
- Graphe de voies OSM pour la circulation (intersections, priorités, feux réels), arrêts réels, sauts-de-mouton.
- Découpage en zones et « origine flottante » pour couvrir toute la ville sans perte de précision.

## 3. Modèles 3D réalistes (Blender)
- Échelle réelle (1 unité = 1 m), axe avant = +Z, pivot au sol entre les roues.
- Budgets indicatifs pour mobile : moto 8 000 à 15 000 triangles, tricycle 12 000 à 20 000, voitures PNJ 5 000 à 10 000, personnages 5 000 à 8 000 ; 2 ou 3 niveaux de détail (LOD).
- Exporter en FBX, créer un prefab avec `VehicleRig` (carrosserie, pivot de direction, roues, sièges, ancrages caméra 1re personne / tableau de bord, feux), puis renseigner `prefabRessource` dans `vehicles.json`.
- Pont Maréchal : modèle détaillé à partir de REF-KCE-001 et de plans publics (hauteurs et largeur à confirmer).

## 4. Provinces
- Kongo Central : carte de Matadi (import OSM) autour de la zone du Pont Maréchal ; routes en forte pente.
- Ouverture progressive des 26 provinces avec leurs véhicules et décors typiques, à partir des photos du créateur.

## 5. Gameplay
- Courses partagées en tricycle (plusieurs clients, plusieurs arrêts), livraisons de marchandises, contrats réguliers.
- Gestion d'une petite flotte (chauffeurs employés, « versement » quotidien), tontine (likelemba) entre chauffeurs.
- Événements : embouteillages, routes inondées en saison des pluies, travaux.

## 6. Son et musique
- Enregistrements réels de moteurs et de klaxons (ou banques sous licence), ambiance de rue de Kinshasa.
- Musique originale commandée à des artistes congolais (contrat de cession de droits) ; aucune chanson commerciale sans licence.

## 7. Publication
- Clé de signature, AAB, fiche Google Play (captures, politique de confidentialité, classification).
- Achats éventuels uniquement via Google Play Billing / App Store (Unity IAP) ; le jeu ne demande jamais de carte bancaire.
- Revue juridique des marques de véhicules et des enseignes (licences ou noms fictifs).
