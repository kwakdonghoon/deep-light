using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests
{
    /// <summary>
    /// 밸런스 점검: 월드에 실제로 깔린 자원(광맥·적 드롭·상자·조개)이 모든 업그레이드에 필요한 양에 비해 얼마나 되는지를 센다.
    /// 숫자는 Logs/balance_report.txt 에 남고, 너무 모자라거나 너무 남는 자원이 있으면 실패한다.
    /// </summary>
    public class BalanceTests
    {
        static double Avg(DropRange d) { return (d.min + d.max) / 2.0; }

        /// <summary>한 시드의 자원 공급량(기댓값)과 수요(전체 업그레이드 비용)를 계산한다.</summary>
        static void Measure(int seed, out double[] supply, out double[] need, out Dictionary<string, int> counts)
        {
            var s = new Sim(seed); s.Populate();
            supply = new double[8]; need = new double[8]; counts = new Dictionary<string, int>();
            int scrapT = 0, brassT = 0, crystalT = 0, clamsShallow = 0, clamsDeep = 0;
            for (int y = 0; y < World.H; y++)
                for (int x = 0; x < World.W; x++)
                {
                    int i = World.Idx(x, y); var t = s.world.Get(x, y);
                    if (t == Tile.Scrap) scrapT++; else if (t == Tile.Brass) brassT++; else if (t == Tile.Crystal) crystalT++;
                    else if (t == Tile.Chest) { foreach (var d in Data.ChestLoot[World.ZoneOfTile(x, y)]) supply[(int)d.item] += Avg(d); Inc(counts, "chest"); }
                    if (s.world.deco[i] == 3) { if (World.ZoneOfTile(x, y) == 2) clamsDeep++; else clamsShallow++; }
                }
            supply[(int)Item.Scrap] += scrapT * 1.5; supply[(int)Item.Brass] += brassT * 1.5; supply[(int)Item.Crystal] += crystalT * 1.5;
            supply[(int)Item.Pearl] += clamsShallow * 1 + clamsDeep * 2;
            foreach (var e in s.enemies)
            {
                if (Sim.IsMini(e.type)) continue;
                Inc(counts, e.type.ToString());
                double mult = e.elite ? 2 : 1;
                foreach (var d in e.Def.drop) supply[(int)d.item] += Avg(d) * mult;
            }
            counts["scrapTiles"] = scrapT; counts["brassTiles"] = brassT; counts["crystalTiles"] = crystalT; counts["clamsShallow"] = clamsShallow; counts["clamsDeep"] = clamsDeep;
            for (int t = 0; t < Data.Up.Length; t++)
                for (int l = 1; l < Data.Up[t].Length; l++) foreach (var c in Data.Up[t][l].cost) need[(int)c.item] += c.amount;
        }

        static void Inc(Dictionary<string, int> d, string k) { int v; d.TryGetValue(k, out v); d[k] = v + 1; }

        [Test]
        public void ResourceSupply_VersusFullUpgradeCost_IsInHealthyRange()
        {
            var sb = new StringBuilder("seed | item: supply / need = ratio\n"); var bad = new List<string>();
            foreach (int seed in new[] { 1234, 7, 424242, 99 })
            {
                double[] sup, need; Dictionary<string, int> cnt; Measure(seed, out sup, out need, out cnt);
                sb.Append("seed " + seed + ": ");
                foreach (var it in new[] { Item.Scrap, Item.Brass, Item.Crystal, Item.Pearl, Item.Gel })
                {
                    double r = sup[(int)it] / need[(int)it];
                    sb.Append(Data.ItemName[(int)it] + " " + sup[(int)it].ToString("F0") + "/" + need[(int)it].ToString("F0") + "=" + r.ToString("F2") + "  ");
                }
                sb.Append(" | tiles s/b/c " + cnt["scrapTiles"] + "/" + cnt["brassTiles"] + "/" + cnt["crystalTiles"] + " clams " + cnt["clamsShallow"] + "+" + cnt["clamsDeep"] + " chests " + (cnt.ContainsKey("chest") ? cnt["chest"] : 0) + "\n");
                foreach (var it in new[] { Item.Scrap, Item.Brass, Item.Crystal, Item.Pearl, Item.Gel })
                {
                    double r = sup[(int)it] / need[(int)it];
                    // 초기 배치만 센 값이다(적은 계속 다시 나온다). 허용 범위는 자원마다 다르다:
                    // 고철은 램프·환기구·포션·비콘으로도 쓰여 넉넉해야 하고, 진주·수정은 희소해야 선택이 의미 있다.
                    double lo, hi;
                    switch (it) { case Item.Scrap: lo = 2.5; hi = 5.5; break; case Item.Brass: lo = 1.6; hi = 3.6; break; case Item.Crystal: lo = 1.8; hi = 4.2; break; case Item.Pearl: lo = 2.2; hi = 5.0; break; default: lo = 2.5; hi = 6.0; break; }
                    if (r < lo) bad.Add("seed " + seed + " " + Data.ItemName[(int)it] + " 공급이 모자라다(" + r.ToString("F2") + " < " + lo + ")");
                    else if (r > hi) bad.Add("seed " + seed + " " + Data.ItemName[(int)it] + " 공급이 너무 남는다(" + r.ToString("F2") + " > " + hi + ")");
                }
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Path.Combine("Logs", "balance_report.txt"), sb.ToString());
            Assert.IsEmpty(bad, string.Join("; ", bad));
        }
    }
}
