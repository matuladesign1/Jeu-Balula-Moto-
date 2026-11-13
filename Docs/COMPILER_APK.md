# Obtenir l'APK Android de BALULA MOTO

## Méthode 1 — dans le cloud, sans Unity sur votre ordinateur (recommandée)

Le dépôt GitHub contient un « workflow » qui installe Unity 6 sur un serveur de GitHub, prépare le projet
(personnages 3D, carte réelle de Kinshasa), compile l'APK et le publie. C'est gratuit pour un dépôt public.

### À faire une seule fois : vos identifiants Unity

1. Avoir un **compte Unity gratuit** (id.unity.com). La licence **Personal** gratuite suffit.
2. Sur GitHub, ouvrir le dépôt **Jeu-Balula-Moto-** → **Settings** → **Secrets and variables** → **Actions**.
3. **New repository secret** :
   - Nom `UNITY_USERNAME` → valeur : l'adresse e-mail du compte Unity.
   - Nom `UNITY_PASSWORD` → valeur : le mot de passe du compte Unity.

Les secrets sont chiffrés par GitHub : personne ne peut les relire (ni moi, ni les visiteurs du dépôt).
Ne les écrivez jamais dans un message, un fichier ou une discussion.

> Si votre compte Unity a la **vérification en deux étapes** activée, la connexion automatique peut échouer.
> Dans ce cas, désactivez-la le temps de la première compilation, ou créez un second compte Unity gratuit réservé à la compilation.

### Lancer la compilation

- Onglet **Actions** → **Construire l'APK Android** → **Run workflow** (case « Importer la vraie carte de Kinshasa » cochée).
- La compilation démarre aussi toute seule à chaque modification du projet poussée sur `main`.
- Durée : 40 à 70 minutes la première fois (installation de Unity), moins ensuite grâce au cache.

### Récupérer l'APK

- Onglet **Releases** du dépôt (ou https://github.com/matuladesign1/Jeu-Balula-Moto-/releases/latest) →
  **BalulaMoto.apk**. Ouvrez ce lien directement sur le téléphone Android.
- Sur le téléphone : autoriser l'installation d'applications de « sources inconnues » pour le navigateur, puis installer.
- **Version 4.0** : si le téléphone refuse l'installation (« application non installée »), désinstallez une fois
  l'ancienne version (signée avec une autre clé), puis installez la 4.0. Les versions suivantes s'installeront
  par-dessus sans perdre les sauvegardes (même clé de test pour toutes les compilations).

### Ce que fait la compilation

1. Installe Unity 6 (dernière version 6000.0) avec le module Android, active la licence Personal.
2. Télécharge le centre de Kinshasa depuis OpenStreetMap (`scripts/fetch_osm.sh`, rayon ≈ 2,6 km pour les rues,
   1,7 km pour les bâtiments). En cas d'échec, la piste d'essai est utilisée.
3. Lance `BalulaMoto.EditorTools.BalulaSetup.BuildAndroidCI` : matériaux, personnages 3D (Rocketbox), scène,
   import de la carte, réglages Android (IL2CPP, ARM64 + ARMv7), puis l'APK.
4. Publie l'APK en artefact et dans **Releases**.

### Si la compilation échoue

Ouvrez l'exécution en rouge dans l'onglet **Actions**, puis l'étape en échec :

| Message | Solution |
|---|---|
| « Secrets Unity manquants » | Ajouter `UNITY_USERNAME` et `UNITY_PASSWORD` (voir plus haut). |
| Échec de l'étape « Activer la licence » | Vérifier e-mail / mot de passe, désactiver la vérification en deux étapes, ou utiliser un autre compte gratuit. |
| `error CS…` dans « Construire l'APK » | Erreur de code : transmettez la ligne, elle sera corrigée. |
| Plus de place sur le disque | Relancer ; l'étape « Libérer de l'espace disque » fait normalement de la place. |

## Méthode 2 — avec Unity sur un ordinateur

1. Installer **Unity Hub**, puis **Unity 6** (6000.0 LTS) avec **Android Build Support** (OpenJDK, Android SDK & NDK).
2. Unity Hub → **Add** → choisir le dossier du dépôt (cloné ou téléchargé en ZIP).
3. Menu **BALULA MOTO > 1. Préparer le projet** (construit aussi les personnages 3D, quelques minutes la première fois).
4. Facultatif : **BALULA MOTO > 4. Importer une carte OpenStreetMap…** avec un fichier `.osm`.
5. Menu **BALULA MOTO > 2. Construire l'APK Android** → `Builds/BalulaMoto.apk`.

## Réglages Android appliqués

- Identifiant `com.matuladesign.balulamoto`, nom « BALULA MOTO », éditeur « MATULA DESIGN ».
- Paysage uniquement, IL2CPP, ARM64 + ARMv7 (téléphones récents et plus anciens).
- Icône et écran de démarrage : logo BALULA MOTO (REF-APP-001).
- APK signé avec la **clé de test du projet** (`Keystore/balula-test.keystore`, mot de passe connu) : la même pour
  toutes les compilations, pour pouvoir mettre à jour le jeu sans le désinstaller. Ce n'est pas une clé secrète :
  pour le Google Play Store, il faudra une clé de publication privée (gardée hors du dépôt, dans les secrets GitHub)
  et un fichier `.aab` (étape ultérieure).
