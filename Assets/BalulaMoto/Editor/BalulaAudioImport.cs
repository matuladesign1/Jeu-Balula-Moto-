// BALULA MOTO — réglages d'import des musiques : lecture en continu depuis l'APK (peu de mémoire), Vorbis.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalulaMoto.EditorTools
{
    public class BalulaAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/BalulaMoto/Musique/")) return;
            AudioImporter ai = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.42f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = true;
        }
    }
}
#endif
