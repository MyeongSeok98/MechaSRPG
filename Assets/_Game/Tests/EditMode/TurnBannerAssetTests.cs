using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MechaXCOM.Tests
{
    public class TurnBannerAssetTests
    {
        [TestCase("EnemyTurn")] [TestCase("YourTurn")]
        public void BannerImportPreservesSourceDimensionsAndTransparentEdges(string name)
        {
            string path = "Assets/_Game/Art/UI/Resources/TurnBanners/" + name + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); Assert.That(texture, Is.Not.Null);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Assert.That(texture.width, Is.EqualTo(width), "UI art must not stretch to a power-of-two width.");
            Assert.That(texture.height, Is.EqualTo(height));
            Assert.That(importer.alphaIsTransparency, Is.True); Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }
    }
}
