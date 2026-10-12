using UnityEditor;
using UnityEngine;
public static class ImportRedDragonPresentationExtras
{
    public static string Run()
    {
        const string fire = "Assets/Resources/Project333/Presentation/RedDragon/FireAtlas.png";
        var texture = (TextureImporter)AssetImporter.GetAtPath(fire);
        if (texture == null) throw new System.InvalidOperationException("Missing FireAtlas");
        texture.textureType = TextureImporterType.Default;
        texture.alphaSource = TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency = true;
        texture.filterMode = FilterMode.Point;
        texture.mipmapEnabled = false;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.maxTextureSize = 4096;
        texture.SaveAndReimport();
        foreach (var path in new [] {
            "Assets/Resources/Project333/Audio/RedDragon/FireBreath.wav",
            "Assets/Resources/Project333/Audio/Common/UnitHit.wav" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if(importer == null) throw new System.InvalidOperationException("Missing sound " + path);
            // Files are already mono; avoid an extra mono conversion/normalization pass.
            importer.forceToMono = false;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        return "FireAtlas and two audio clips imported.";
    }
}