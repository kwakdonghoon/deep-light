using System.Collections.Generic;
using NUnit.Framework;

namespace Abyss.Tests
{
    /// <summary>웹 프로토타입(시드 1234)에서 측정한 값과 같은 월드가 나오는지 확인한다. 포팅이 맞았다는 가장 강한 증거.</summary>
    public class WorldParityTests
    {
        static uint Fnv(byte[] a)
        {
            uint h = 2166136261u;
            unchecked { foreach (byte b in a) { h ^= b; h *= 16777619u; } }
            return h;
        }

        static string Row(World w, int y, int from, int to)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = from; x < to; x++) sb.Append(w.tiles[World.Idx(x, y)]);
            return sb.ToString();
        }

        static World Make(int seed) { var w = new World(); w.Generate(seed); return w; }

        [Test]
        public void Seed1234_MatchesWebTileCounts()
        {
            var w = Make(1234);
            var counts = new Dictionary<int, int>();
            foreach (byte t in w.tiles) { counts.TryGetValue(t, out int c); counts[t] = c + 1; }
            var expected = new Dictionary<int, int> { { 0, 15361 }, { 1, 836 }, { 2, 2347 }, { 3, 5206 }, { 4, 203 }, { 5, 252 }, { 6, 87 }, { 7, 1264 }, { 10, 44 } };
            CollectionAssert.AreEquivalent(expected, counts);
        }

        [Test]
        public void Seed1234_MatchesWebDecoCounts()
        {
            var w = Make(1234);
            int d1 = 0, d2 = 0, d3 = 0;
            foreach (byte d in w.deco) { if (d == 1) d1++; else if (d == 2) d2++; else if (d == 3) d3++; }
            Assert.AreEqual(201, d1); Assert.AreEqual(288, d2); Assert.AreEqual(121, d3);
        }

        [Test]
        public void Seed1234_MatchesWebChecksums()
        {
            var w = Make(1234);
            Assert.AreEqual(1979040639u, Fnv(w.tiles), "tiles FNV");
            Assert.AreEqual(3682895901u, Fnv(w.deco), "deco FNV");
        }

        [Test]
        public void Seed1234_MatchesWebSampleRows()
        {
            var w = Make(1234);
            Assert.AreEqual("1111110000000000000000000000001111100000", Row(w, 80, 60, 100));
            Assert.AreEqual("3333300000000000000000000000000030000333", Row(w, 20, 60, 100));
            Assert.AreEqual("2220000022000000000000000000000000022222", Row(w, 110, 60, 100));
        }

        [Test]
        public void SameSeed_IsDeterministic_DifferentSeed_Differs()
        {
            Assert.AreEqual(Fnv(Make(7).tiles), Fnv(Make(7).tiles));
            Assert.AreNotEqual(Fnv(Make(7).tiles), Fnv(Make(8).tiles));
        }

        [Test]
        public void BaseAndArena_AreOpen_BorderIsBedrock()
        {
            var w = Make(99);
            Assert.AreEqual(Tile.Floor, w.Get(World.CX, World.CY));
            Assert.AreEqual(Tile.Floor, w.Get(World.ArenaX, World.ArenaY));
            for (int i = 0; i < World.W; i++) { Assert.AreEqual(Tile.Bedrock, w.Get(i, 0)); Assert.AreEqual(Tile.Bedrock, w.Get(0, i)); }
        }

        [Test]
        public void Zones_FollowDistanceFromBeacon()
        {
            Assert.AreEqual(0, World.ZoneOfTile(World.CX, World.CY));
            Assert.AreEqual(0, World.ZoneOfTile(World.CX + 29, World.CY));
            Assert.AreEqual(1, World.ZoneOfTile(World.CX + 30, World.CY));
            Assert.AreEqual(1, World.ZoneOfTile(World.CX + 54, World.CY));
            Assert.AreEqual(2, World.ZoneOfTile(World.CX + 55, World.CY));
        }

        [Test]
        public void Collision_SolidBlocksMovement_FloorDoesNot()
        {
            var w = Make(1234);
            // (90,80) 은 암반, (89,80) 은 바닥
            Assert.IsTrue(w.Collides(90 * 16 + 8, 80 * 16 + 8, 5));
            Assert.IsFalse(w.Collides(89 * 16 + 8, 80 * 16 + 8, 5));
            float x = 89 * 16 + 8, y = 80 * 16 + 8;
            bool hit = w.MoveBox(ref x, ref y, 40, 0, 5);
            Assert.IsTrue(hit);
            Assert.Less(x, 90 * 16);
        }
    }
}
