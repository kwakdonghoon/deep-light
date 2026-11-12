using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>Resources/Art 아래 PNG 를 픽셀아트 규칙으로 임포트: 점 샘플링, 무압축, PPU 1(UI 용), 프레임은 9-슬라이스 경계 4px.</summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() { return 3; }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 1;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            if (assetPath.Contains("/Frames/")) ti.spriteBorder = new Vector4(4, 4, 4, 4);
            if (assetPath.Contains("/Tiles/")) ti.isReadable = true;   // 타일은 픽셀을 읽어 가장자리 하이라이트를 입힌다
        }
    }
}
