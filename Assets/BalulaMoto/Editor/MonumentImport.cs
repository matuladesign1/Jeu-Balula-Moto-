// BALULA MOTO — réglages d'import des modèles fournis par MATULA DESIGN, convertis en OBJ (mètres, Y vers le haut) :
// monuments (dossier Modeles), véhicules (Resources/BalulaMoto/Vehicules), accessoires (Resources/BalulaMoto/Accessoires)
// et décor de la ville (Resources/BalulaMoto/Decor : maisons, immeubles, barrières, mobilier urbain), chargés pendant la partie.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    public class MonumentImportRules : AssetPostprocessor
    {
        private const string Folder = "Assets/BalulaMoto/Modeles/";
        private const string Vehicles = "Assets/BalulaMoto/Resources/BalulaMoto/Vehicules/";
        private const string Accessories = "Assets/BalulaMoto/Resources/BalulaMoto/Accessoires/";
        private const string Decor = "Assets/BalulaMoto/Resources/BalulaMoto/Decor/";

        private void OnPreprocessModel()
        {
            bool vehicle = assetPath.StartsWith(Vehicles);
            bool decor = assetPath.StartsWith(Decor);
            if (!assetPath.StartsWith(Folder) && !vehicle && !decor && !assetPath.StartsWith(Accessories)) return;
            ModelImporter mi = assetImporter as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileScale = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            // Véhicules et décor : maillages lisibles (la circulation fusionne carrosserie, roues et feux ; la ville fusionne
            // les maisons, immeubles et véhicules garés de chaque case par matériau)
            mi.isReadable = vehicle || decor;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.weldVertices = true;
            // Fichiers sans normales : faces nettes (angle de lissage faible) ; fichiers qui fournissent leurs normales
            // (tricycle, fusil : bords arrondis) : normales du fichier
            bool hasNormals = false;
            try { hasNormals = assetPath.EndsWith(".obj") && System.IO.File.ReadAllText(assetPath).Contains("\nvn "); } catch (System.Exception) { }
            mi.importNormals = hasNormals ? ModelImporterNormals.Import : ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = vehicle ? 40f : 30f;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            // Noms des matériaux = noms du fichier .mtl (le jeu y retrouve couleurs, textures et rôles)
            mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            mi.materialSearch = ModelImporterMaterialSearch.Local;
            mi.generateSecondaryUV = false;
        }
    }
}
#endif
