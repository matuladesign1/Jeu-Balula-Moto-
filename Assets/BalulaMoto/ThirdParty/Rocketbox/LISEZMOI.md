# Personnages Microsoft Rocketbox (licence MIT)

Source : https://github.com/microsoft/Microsoft-Rocketbox — © 2020 Microsoft, licence MIT (voir `LICENSE.md`).

## Contenu repris dans BALULA MOTO

- **16 avatars** (dossier `Avatars/`) : Business_Male_05, Male_Adult_04, Male_Adult_12, Male_Adult_17, Male_Adult_18,
  Sports_Male_03, Delivery_Male_01, Wood_Male_01, Construction_Male_03, Construction_Male_08, Business_Female_01,
  Female_Party_02, Medical_Female_01, Female_Child_02, Male_Child_02, Police_Male_01.
  Choix : personnes à la peau foncée en priorité (population de Kinshasa), tenues de ville et de travail. Les uniformes
  militaires étrangers du pack (camouflage américain) ne sont pas utilisés.
- **22 animations** (dossier `Animations/`) : marche, marche lente, attente, téléphone, discussion, signe de la main,
  position assise, accroupie, parapluie (versions homme et femme).

## Modifications

- Textures réduites de 2048 à 1024 pixels et converties de TGA en JPG (couleur, relief) ou PNG (cheveux, transparence) ;
  cartes de brillance (specular) non reprises, remplacées par une valeur fixe.
- Fichiers d'animation renommés sans « .max ».
- Les animations du jeu sont dérivées automatiquement (rotations seules) par `Editor/HumanBuilder.cs`.

## Licence (résumé)

Utilisation, modification et distribution autorisées, y compris commerciale, à condition de conserver la mention de
copyright et le texte de la licence MIT (fichier `LICENSE.md`, crédits du jeu).
