using NUnit.Framework;
using UnityEngine;
using WelpTimeToDie.Cameras;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Gameplay.Tests
{
    public sealed class ProceduralMapTests
    {
        [TestCase(128, 32)]
        [TestCase(64, 16)]
        [TestCase(4, 4)]
        [TestCase(257, 48)]
        public void GeneratedMapHasRequestedDimensionsSolidColumnsAndGentleSteps(int width, int height)
        {
            var map = new TerrainHeightGenerator().Generate(new TerrainSettings(width, height), "terrain-test");
            Assert.That(map.Width, Is.EqualTo(width));
            Assert.That(map.Height, Is.EqualTo(height));
            for (int x = 0; x < width; x++)
            {
                int top = map.GetColumnHeight(x);
                Assert.That(top, Is.InRange(1, height - 3));
                for (int y = 0; y < top; y++) Assert.That(map.IsSolid(x, y), Is.True);
                Assert.That(map.IsSolid(x, top), Is.False);
                if (x > 0) Assert.That(Mathf.Abs(top - map.GetColumnHeight(x - 1)), Is.LessThanOrEqualTo(1));
            }
            Assert.That(map.IsSolid(-1, 0), Is.False);
            Assert.That(map.IsSolid(width, 0), Is.False);
        }

        [Test]
        public void SeedsAreRepeatableAndDifferentSeedsProduceDifferentTerrain()
        {
            var generator = new TerrainHeightGenerator();
            var settings = new TerrainSettings();
            var first = generator.Generate(settings, "a saved seed");
            var repeated = generator.Generate(settings, "a saved seed");
            var other = generator.Generate(settings, "another seed");
            bool differs = false;
            for (int x = 0; x < first.Width; x++)
            {
                Assert.That(first.GetColumnHeight(x), Is.EqualTo(repeated.GetColumnHeight(x)));
                differs |= first.GetColumnHeight(x) != other.GetColumnHeight(x);
            }
            Assert.That(differs, Is.True);
        }

        [Test]
        public void CameraClampKeepsViewInsideMapAndCentersViewsLargerThanMap()
        {
            Assert.That(MapCameraController.ClampAxis(-1000f, -64f, 64f, 12f), Is.EqualTo(-52f));
            Assert.That(MapCameraController.ClampAxis(1000f, -64f, 64f, 12f), Is.EqualTo(52f));
            Assert.That(MapCameraController.ClampAxis(20f, -8f, 24f, 24f), Is.EqualTo(8f));
        }

        [Test]
        public void CameraBrowseRecenterAndZoomLimitsWorkIndependentlyOfCombatTime()
        {
            var cameraObject = new GameObject("Test map camera", typeof(Camera));
            try
            {
                var controller = cameraObject.AddComponent<MapCameraController>();
                Assert.That(controller.Zoom, Is.EqualTo(6.75f));
                controller.Pan(new Vector2(3f, 4f));
                Assert.That(controller.IsBrowsing, Is.True);
                Assert.That((Vector2)cameraObject.transform.position, Is.EqualTo(new Vector2(3f, 4f)));
                controller.ZoomBy(1000f, Vector2.zero);
                Assert.That(controller.Zoom, Is.EqualTo(2f));
                controller.ZoomBy(-1000f, Vector2.zero);
                Assert.That(controller.Zoom, Is.EqualTo(24f));
                controller.RecenterOnPlayer();
                Assert.That(controller.IsBrowsing, Is.False);
                Assert.That(controller.Zoom, Is.EqualTo(24f));
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }
    }
}
