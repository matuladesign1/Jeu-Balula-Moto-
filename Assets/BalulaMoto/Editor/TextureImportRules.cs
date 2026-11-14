// BALULA MOTO — réglages d'import des textures dessinées par les scripts (dossier Resources/BalulaMoto/Textures) :
// murs de clôture (répétés en longueur), barbelé découpé (couverture conservée au loin), berges et rides de l'eau.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    public class TextureImportRules : AssetPostprocessor
    {
        private const string Folder = "Assets/BalulaMoto/Resources/BalulaMoto/Textures/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            TextureImporter ti = assetImporter as TextureImporter;
            if (ti == null) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            ti.mipmapEnabled = true;
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 2;
            ti.maxTextureSize = 2048;
            if (name.StartsWith("eau_ondes"))
            {
                // Pentes des rides lues telles quelles par le shader BalulaMoto/Eau : texture linéaire, pas une normal map
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
            }
            else if (name.StartsWith("barbele"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;
                ti.alphaTestReferenceValue = 0.45f;
                ti.wrapModeV = TextureWrapMode.Clamp;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                // Murs et berges : bandes empilées ou pente entière, répétées seulement en longueur
                if (name.StartsWith("murs") || name.StartsWith("berge") || name.StartsWith("beton")) ti.wrapModeV = TextureWrapMode.Clamp;
            }
        }
    }
}
#endif
