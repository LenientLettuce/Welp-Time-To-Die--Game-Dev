using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Gameplay.Tests
{
    public sealed class TerrainFeatureTests
    {
        [Test]
        public void PreviewRevealsAreIsolatedAndUseEuclideanEntranceDistance()
        {
            var cells = new[] { new Vector3Int(10, 6), new Vector3Int(11, 6) };
            var tunnel = new TerrainFeature(TerrainFeatureKind.Tunnel, cells, new Vector2(10, 10), new Vector2(30, 10));
            var live = new TerrainFeatureState(new[] { tunnel });
            var preview = live.Copy();
            Assert.That(preview.Evaluate(new Bounds(new Vector3(14, 14), Vector3.one), 0, 5), Is.False);
            Assert.That(preview.Evaluate(new Bounds(new Vector3(13, 14), Vector3.one), 0, 5), Is.True);
            Assert.That(preview.IsRemoved(cells[0]), Is.True);
            Assert.That(live.IsOpen(0), Is.False);
            Assert.That(live.IsRemoved(cells[0]), Is.False);
            Assert.That(live.Copy().IsOpen(0), Is.False, "Replanning must start from committed state.");
            Assert.That(live.Evaluate(new Bounds(new Vector3(30, 11), Vector3.one), 0, 5), Is.True);
            live.Evaluate(new Bounds(new Vector3(100, 100), Vector3.one), 0, 5);
            Assert.That(live.IsOpen(0), Is.True);
            Assert.That(live.Copy().IsRemoved(cells[0]), Is.True);
        }

        [Test]
        public void CollapsingFloorRequiresFeetContactAndPreviewDoesNotCommit()
        {
            var cell = new Vector3Int(10, 7);
            var pit = new TerrainFeature(TerrainFeatureKind.CollapsingFloor, new[] { cell }, default, default, new Rect(10, 10, 4, 0));
            var live = new TerrainFeatureState(new[] { pit });
            Assert.That(live.Evaluate(new Bounds(new Vector3(11, 14), new Vector3(1, 2)), -1, 5), Is.False, "Passing overhead is safe.");
            Assert.That(live.Evaluate(new Bounds(new Vector3(11, 9), new Vector3(1, 2)), 0, 5), Is.False, "Side contact is safe.");
            Assert.That(live.Evaluate(new Bounds(new Vector3(11, 11), new Vector3(1, 2)), 2, 5), Is.False, "Moving upward is safe.");
            var preview = live.Copy();
            Assert.That(preview.Evaluate(new Bounds(new Vector3(11, 11), new Vector3(1, 2)), 0, 5), Is.True);
            Assert.That(live.Revision, Is.Zero);
            Assert.That(live.IsRemoved(cell), Is.False);
            Assert.That(live.Evaluate(new Bounds(new Vector3(11, 11), new Vector3(1, 2)), 0, 5), Is.True);
            Assert.That(live.IsRemoved(cell), Is.True);
            Assert.That(live.Evaluate(new Bounds(new Vector3(11, 11), new Vector3(1, 2)), 0, 5), Is.False);
            Assert.That(live.Revision, Is.EqualTo(1));
        }

        [Test]
        public void FeatureSeedsRepeatAndAllGeometryIsInsideSolidTerrainWithoutOverlap()
        {
            int tunnels = 0, pits = 0;
            var settings = new TerrainFeatureSettings();
            for (int seed = 0; seed < 40; seed++)
            {
                var map = new TerrainHeightGenerator().Generate(new TerrainSettings(), "feature-test-" + seed);
                map = TerrainFoundationBuilder.Expand(map, settings, out _);
                var generator = new TerrainFeatureGenerator();
                var features = generator.Generate(map, settings);
                var repeated = generator.Generate(map, settings);
                Assert.That(repeated.Count, Is.EqualTo(features.Count));
                var occupied = new HashSet<Vector3Int>();
                var tunnelSpans = new List<Vector2Int>();
                for (int i = 0; i < features.Count; i++)
                {
                    var feature = features[i];
                    CollectionAssert.AreEqual(feature.Cells, repeated[i].Cells);
                    if (feature.Kind == TerrainFeatureKind.Tunnel)
                    {
                        tunnels++;
                        var remaining = new HashSet<Vector3Int>(feature.Cells);
                        var pending = new Queue<Vector3Int>();
                        pending.Enqueue(feature.Cells[0]);
                        remaining.Remove(feature.Cells[0]);
                        while (pending.Count > 0)
                        {
                            Vector3Int cell = pending.Dequeue();
                            foreach (Vector3Int step in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                                if (remaining.Remove(cell + step)) pending.Enqueue(cell + step);
                        }
                        Assert.That(remaining, Is.Empty, "Tunnel must be connected.");
                        int left = Mathf.RoundToInt(feature.Entrance.x), right = Mathf.RoundToInt(feature.Exit.x) - 1;
                        Assert.That(right - left, Is.InRange(settings.tunnelMinLength, settings.tunnelMaxLength));
                        foreach (var span in tunnelSpans)
                        {
                            int gap = left > span.y ? left - TunnelAccessBuilder.AccessLength - span.y - 1 :
                                span.x - right - TunnelAccessBuilder.AccessLength - 1;
                            Assert.That(gap, Is.GreaterThanOrEqualTo(settings.tunnelMinimumGap), "Separate tunnels need the configured ground gap.");
                        }
                        tunnelSpans.Add(new Vector2Int(left - TunnelAccessBuilder.AccessLength, right + TunnelAccessBuilder.AccessLength));
                        var tunnelCells = new HashSet<Vector3Int>(feature.Cells);
                        foreach (var mouth in new[] { feature.Entrance, feature.Exit })
                        {
                            bool entering = mouth == feature.Entrance;
                            int inside = entering ? left : right;
                            int outside = entering ? left - 1 : right + 1;
                            int floor = Mathf.RoundToInt(mouth.y - feature.PassageHeight * 0.5f);
                            for (int y = floor; y < floor + feature.PassageHeight; y++)
                            {
                                Assert.That(tunnelCells.Contains(new Vector3Int(inside, y)), Is.True, "Side portal must have full vertical clearance.");
                                Assert.That(!map.IsSolid(outside, y) || tunnelCells.Contains(new Vector3Int(outside, y)), Is.True,
                                    "Side portal must connect to its concealed access route.");
                            }
                            Assert.That(map.IsSolid(inside, floor + feature.PassageHeight), Is.True, "Keep a roof over the entrance.");
                            Assert.That(tunnelCells.Contains(new Vector3Int(inside, map.GetColumnHeight(inside) - 1)), Is.False,
                                "Sideways mouths must not cut a vertical hole through the surface.");
                        }
                        foreach (var discovery in new[] { feature.EntranceRevealPoint, feature.ExitRevealPoint })
                        {
                            int x = Mathf.FloorToInt(discovery.x);
                            Assert.That(TunnelAccessBuilder.IsSubtleDiscoveryPoint(map, x), Is.True);
                            Assert.That(tunnelCells.Contains(new Vector3Int(x, map.GetColumnHeight(x) - 1)), Is.True,
                                "The concealed access cover must connect to the surface when revealed.");
                            Assert.That(map.IsSolid(x, map.GetColumnHeight(x) - 1), Is.True, "Access stays sealed before reveal.");
                        }
                        int previousBottom = -1, previousTop = -1;
                        for (int x = left; x <= right; x++)
                        {
                            int bottom = int.MaxValue, top = int.MinValue;
                            foreach (Vector3Int cell in feature.Cells)
                                if (cell.x == x) { bottom = Mathf.Min(bottom, cell.y); top = Mathf.Max(top, cell.y); }
                            Assert.That(top - bottom + 1, Is.GreaterThanOrEqualTo(4), "Every tunnel column needs four-block clearance.");
                            if (previousBottom >= 0)
                            {
                                Assert.That(Mathf.Abs(bottom - previousBottom), Is.LessThanOrEqualTo(1), "Curves must use gentle single-row steps.");
                                Assert.That(Mathf.Min(top, previousTop) - Mathf.Max(bottom, previousBottom) + 1,
                                    Is.GreaterThanOrEqualTo(4), "Adjacent slices must share four open rows.");
                            }
                            previousBottom = bottom; previousTop = top;
                        }
                    }
                    else
                    {
                        pits++;
                        int depth = feature.Cells.Count / Mathf.RoundToInt(feature.Surface.width);
                        Assert.That(depth, Is.InRange(9, 15), "Tripled pit depths must not be clipped by the map bottom.");
                    }
                    foreach (Vector3Int cell in feature.Cells)
                    {
                        Assert.That(map.IsSolid(cell.x, cell.y), Is.True);
                        Assert.That(cell.y, Is.GreaterThanOrEqualTo(1), "Keep a solid bottom row.");
                        Assert.That(occupied.Add(cell), Is.True, "Features must not overlap.");
                    }
                }
            }
            Assert.That(tunnels, Is.GreaterThan(20));
            Assert.That(pits, Is.GreaterThan(40));
        }

        [Test]
        public void ConcealedSurfaceDiscoveryRevealsDeepPortalWithoutCommittingPreview()
        {
            var feature = new TerrainFeature(TerrainFeatureKind.Tunnel, new[] { new Vector3Int(10, 2) },
                new Vector2(10, 3), new Vector2(30, 3), passageHeight: 4,
                entranceRevealPoint: new Vector2(6, 15), exitRevealPoint: new Vector2(34, 15));
            var live = new TerrainFeatureState(new[] { feature });
            var predicted = live.Copy();
            var player = new Bounds(new Vector3(6, 16), new Vector3(1, 2));
            Assert.That(predicted.Evaluate(player, 0, 5), Is.True, "Discovery must work from the unchanged surface.");
            Assert.That(live.IsOpen(0), Is.False);
            Assert.That(live.Evaluate(player, 0, 5), Is.True);
        }

        [Test]
        public void ExtraFoundationPreservesWorldSurfaceHeightAndLeavesRoomForDeepPits()
        {
            var original = new TerrainHeightGenerator().Generate(new TerrainSettings(), "foundation-test");
            var expanded = TerrainFoundationBuilder.Expand(original, new TerrainFeatureSettings(), out int addedRows);
            Assert.That(addedRows, Is.GreaterThan(0));
            for (int x = 0; x < original.Width; x++)
            {
                Assert.That(expanded.GetColumnHeight(x) - addedRows, Is.EqualTo(original.GetColumnHeight(x)));
                Assert.That(expanded.GetColumnHeight(x), Is.GreaterThanOrEqualTo(16));
            }
        }
    }
}
