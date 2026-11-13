// BALULA MOTO — réglages d'import des modèles de monuments (dossier Modeles/Monuments, fichiers OBJ en mètres).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    public class MonumentImportRules : AssetPostprocessor
    {
        private const string Folder = "Assets/BalulaMoto/Modeles/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            ModelImporter mi = assetImporter as ModelImporter;
            if (mi == null) return;
            mi.globalScale = 1f;
            mi.useFileScale = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.weldVertices = true;
            // Les fichiers n'ont pas de normales : faces nettes (angle de lissage faible)
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalSmoothingAngle = 30f;
            mi.importTangents = ModelImporterTangents.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.generateSecondaryUV = false;
        }
    }
}
#endif
