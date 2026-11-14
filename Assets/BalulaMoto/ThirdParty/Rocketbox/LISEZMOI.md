# Personnages Microsoft Rocketbox (licence MIT)

Source : https://github.com/microsoft/Microsoft-Rocketbox — © 2020 Microsoft, licence MIT (voir `LICENSE.md`).

## Contenu repris dans BALULA MOTO

- **21 avatars** (dossier `Avatars/`) : Business_Male_05, Male_Adult_04, Male_Adult_09, Male_Adult_10, Male_Adult_12,
  Male_Adult_17, Male_Adult_18, Sports_Male_03, Delivery_Male_01, Wood_Male_01, Construction_Male_03, Construction_Male_08,
  Business_Female_01, Female_Party_02, Female_Adult_02, Female_Adult_11, Female_Adult_15, Medical_Female_01,
  Female_Child_02, Male_Child_02, Police_Male_01.
  Choix : tenues de ville et de travail portées à Kinshasa. Les uniformes militaires étrangers du pack (camouflage
  américain) ne sont pas utilisés.
- **22 animations** (dossier `Animations/`) : marche, marche lente, attente, téléphone, discussion, signe de la main,
  position assise, accroupie, parapluie (versions homme et femme).

## Modifications

- **Teint africain pour tous les personnages** (population de Kinshasa) : sur les avatars dont la peau est claire dans
  le pack, la peau des textures de tête et de corps a été assombrie vers cinq teints bruns à très foncés, en gardant les
  détails (ombres, pores) ; les cheveux châtains ou clairs ont été foncés. Le visage (forme du modèle 3D) reste celui
  du pack. Script : `scripts/africaniser_avatars.py` (à lancer sur les textures TGA d'origine du pack).
- Textures réduites de 2048 à 1024 pixels et converties de TGA en JPG (couleur, relief) ou PNG (cheveux, transparence) ;
  cartes de brillance (specular) non reprises, remplacées par une valeur fixe.
- Fichiers d'animation renommés sans « .max ».
- Les animations du jeu sont dérivées automatiquement (rotations seules) par `Editor/HumanBuilder.cs`.
- `Police_Male_01` habillé en agent de la police congolaise (`scripts/police_congolaise.py`, à lancer après
  `africaniser_avatars.py`) : la casquette à visière du modèle (coque séparée, attachée à l'os de la tête) est
  remodelée en béret noir posé sur le crâne déjà modélisé dessous ; écussons étrangers des manches (drapeau et insigne
  de la police afghane du pack) effacés ; tissu passé au bleu roi de la tenue congolaise. Seuls les sommets et les
  normales de la casquette changent dans le FBX, le reste du fichier est réécrit octet pour octet (`scripts/fbxrw.py`).

## Licence (résumé)

Utilisation, modification et distribution autorisées, y compris commerciale, à condition de conserver la mention de
copyright et le texte de la licence MIT (fichier `LICENSE.md`, crédits du jeu).
