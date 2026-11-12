using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests
{
    /// <summary>
    /// 전투 밸런스: '가만히 서서 때리기만 하는' 최악의 플레이어가 적 한 마리를 상대로 쓰러뜨리는 시간(TTK)과 잃는 체력을 잰다.
    /// 피하거나 물러나는 실제 플레이는 이보다 쉽다. 결과는 Logs/combat_report.txt 에 남는다.
    /// </summary>
    public class CombatBalanceTests
    {
        const float Dt = 1f / 60f;

        static Sim Arena(int harpoonLv, int armorLv)
        {
            var s = new Sim(1234) { started = true };
            for (int dy = -8; dy <= 8; dy++) for (int dx = -14; dx <= 14; dx++) s.world.SetTile(World.CX + dx, World.CY - 30 + dy, Tile.Floor);   // 넓은 공터
            s.P.x = (World.CX) * 16 + 8; s.P.y = (World.CY - 30) * 16 + 8; s.P.lvl[(int)Track.Harpoon] = harpoonLv; s.P.lvl[(int)Track.Armor] = armorLv;
            s.enemies.Clear(); s.P.hp = 999; s.P.o2 = 999;
            return s;
        }

        /// <summary>적 하나와 최대 maxSec 초 싸운다. 반환: (처치 시간 또는 maxSec, 잃은 체력, 처치 여부)</summary>
        static void Duel(EnemyType type, int harpoonLv, int armorLv, float maxSec, out float ttk, out float lost, out bool killed)
        {
            var s = Arena(harpoonLv, armorLv);
            var e = s.SpawnEnemy(type, s.P.x + 18, s.P.y);
            float t = 0; killed = false;
            while (t < maxSec)
            {
                if (!s.enemies.Contains(e)) { killed = true; break; }
                var aim = new Vector2(e.x, e.y);
                float dx = e.x - s.P.x, dy = e.y - s.P.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                var mv = d > 24f ? new Vector2(dx, dy) / d : Vector2.zero;       // 밀려난 적에게는 다가가 붙는다(피하지는 않는다)
                s.Step(Dt, new SimInput { attack = true, aim = aim, move = mv });
                t += Dt;
            }
            ttk = t; lost = 999f - s.P.hp;
        }

        [Test]
        public void Report_EnemyDuels_AndAssertSaneBounds()
        {
            var sb = new StringBuilder("가만히 서서 때리기만 할 때: [작살Lv x 방호복Lv]  TTK(초) / 잃은 체력\n");
            var types = new[] { EnemyType.Jelly, EnemyType.Urchin, EnemyType.Angler, EnemyType.Crab, EnemyType.Eel, EnemyType.Wraith, EnemyType.CrabKing, EnemyType.EelKing };
            var bad = new System.Collections.Generic.List<string>();
            foreach (var type in types)
            {
                sb.Append(type + ": ");
                foreach (var gear in new[] { new[] { 0, 0 }, new[] { 1, 1 }, new[] { 2, 1 }, new[] { 3, 2 } })
                {
                    float ttk, lost; bool k; Duel(type, gear[0], gear[1], 40f, out ttk, out lost, out k);
                    sb.Append("[" + (gear[0] + 1) + "x" + (gear[1] + 1) + "] " + (k ? ttk.ToString("F1") : ">40") + "s/" + lost.ToString("F0") + "hp  ");
                }
                sb.Append("\n");
            }
            // 기본 장비(작살 Lv1·방호복 Lv1, 체력 10)로 일반 몹 한 마리를 맞서도 체력의 절반 이상을 잃으면 초반이 너무 가혹하다.
            foreach (var type in new[] { EnemyType.Jelly, EnemyType.Urchin })
            {
                float ttk, lost; bool k; Duel(type, 0, 0, 40f, out ttk, out lost, out k);
                if (!k || lost > 4f) bad.Add(type + " 초반 장비로 가혹: ttk " + ttk.ToString("F1") + " 손실 " + lost);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Path.Combine("Logs", "combat_report.txt"), sb.ToString());
            Assert.IsEmpty(bad, string.Join("; ", bad));
        }

        [Test]
        public void Report_BossFight_ByGear()
        {
            var sb = new StringBuilder("심연의 눈, 가만히 붙어서 때릴 때(최악): [작살Lv x 방호복Lv]  TTK / 잃은 체력\n");
            foreach (var gear in new[] { new[] { 1, 1 }, new[] { 2, 1 }, new[] { 2, 2 }, new[] { 3, 2 }, new[] { 3, 3 } })
            {
                var s = Arena(gear[0], gear[1]);
                s.P.x = World.AX; s.P.y = World.AY + 60; s.P.hp = 999;
                for (int dy = -12; dy <= 12; dy++) for (int dx = -14; dx <= 14; dx++) s.world.SetTile(World.ArenaX + dx, World.ArenaY + dy, Tile.Floor);
                s.boss.active = true; s.boss.x = World.AX; s.boss.y = World.AY; s.boss.hp = Data.BossHp; s.boss.fireT = 2.4f; s.boss.chargeT = 6f;
                float t = 0;
                while (t < 120f && !s.boss.dead)
                {
                    float dx = s.boss.x - s.P.x, dy = s.boss.y - s.P.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                    var mv = d > 22f ? new Vector2(dx, dy) / d : Vector2.zero;                     // 사거리 안으로 붙는다
                    s.Step(Dt, new SimInput { attack = true, aim = new Vector2(s.boss.x, s.boss.y), move = mv });
                    t += Dt;
                }
                sb.Append("[" + (gear[0] + 1) + "x" + (gear[1] + 1) + "] " + (s.boss.dead ? t.ToString("F0") : ">120") + "s/" + (999f - s.P.hp).ToString("F0") + "hp (체력 " + s.MaxHp + ")\n");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Path.Combine("Logs", "combat_boss_report.txt"), sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
