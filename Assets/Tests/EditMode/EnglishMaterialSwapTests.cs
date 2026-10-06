using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>英語化（段階6）：英語版で看板のマテリアルを差し替える計算。</summary>
    public class EnglishMaterialSwapTests
    {
        private Material _ja;
        private Material _en;
        private Material _other;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _ja = new Material(shader) { name = "Ja" };
            _en = new Material(shader) { name = "En" };
            _other = new Material(shader) { name = "Other" };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ja);
            Object.DestroyImmediate(_en);
            Object.DestroyImmediate(_other);
        }

        [Test]
        public void 日本語のマテリアルだけを英語に替える()
        {
            var result = EnglishMaterialSwap.Swap(new[] { _other, _ja, _other }, _ja, _en);
            Assert.AreEqual(new[] { _other, _en, _other }, result);
        }

        [Test]
        public void 元の並びは変えない()
        {
            var original = new[] { _ja };
            EnglishMaterialSwap.Swap(original, _ja, _en);
            Assert.AreSame(_ja, original[0]);
        }

        [Test]
        public void 使っていなければ替えない()
        {
            Assert.IsNull(EnglishMaterialSwap.Swap(new[] { _other }, _ja, _en));
        }

        [Test]
        public void 組が欠けていれば替えない()
        {
            Assert.IsNull(EnglishMaterialSwap.Swap(new[] { _ja }, _ja, null));
            Assert.IsNull(EnglishMaterialSwap.Swap(new[] { _ja }, null, _en));
            Assert.IsNull(EnglishMaterialSwap.Swap(null, _ja, _en));
        }
    }
}
