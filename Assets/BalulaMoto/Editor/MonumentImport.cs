// BALULA MOTO — réglages d'import des modèles fournis par MATULA DESIGN, convertis en OBJ (mètres, Y vers le haut) :
// monuments (dossier Modeles), véhicules (Resources/BalulaMoto/Vehicules) et accessoires (Resources/BalulaMoto/Accessoires),
// chargés pendant la partie.
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

        private void OnPreprocessModel()
        {
            bool vehicle = assetPath.StartsWith(Vehicles);
            if (!assetPath.StartsWith(Folder) && !vehicle && !assetPath.StartsWith(Accessories)) return;
            ModelImporter mi = assetImporter as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileScale = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            // Véhicules : maillages lisibles, pour que la circulation fusionne carrosserie, roues et feux
            mi.isReadable = vehicle;
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
            mi.generateSecondaryUV = false;
        }
    }
}
#endif
