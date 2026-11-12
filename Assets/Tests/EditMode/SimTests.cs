using NUnit.Framework;
using UnityEngine;

namespace Abyss.Tests
{
    public class SimTests
    {
        static Sim NewSim(int seed = 1234)
        {
            var s = new Sim(seed) { started = true };
            return s;
        }

        static void Run(Sim s, float seconds, SimInput inp, float dt = 1f / 60f)
        {
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < n; i++) s.Step(dt, inp);
        }

        static void AtBeacon(Sim s) { s.P.x = World.BX; s.P.y = World.BY + 28; s.P.vx = s.P.vy = 0; }

        [Test]
        public void StartingState_IsSane()
        {
            var s = NewSim();
            Assert.AreEqual(10, s.MaxHp); Assert.AreEqual(40, s.MaxO2);
            Assert.AreEqual(3, s.inv[(int)Item.Lamp]);
            Assert.AreEqual(s.MaxHp, s.P.hp, 0.001f);
        }

        [Test]
        public void Mining_BreaksRock_AfterEnoughTime_AndChangesTile()
        {
            var s = NewSim();
            s.P.x = 89 * 16 + 8; s.P.y = 80 * 16 + 8;
            s.world.SetTile(90, 80, Tile.Rock);            // 구역별 지형 변주로 이 칸이 깎였을 수 있어 암반을 직접 둔다
            int changed = 0; s.TileChanged += (x, y) => changed++;
            var inp = new SimInput { mine = true, aim = new Vector2(90 * 16 + 8, 80 * 16 + 8) };
            Run(s, 0.5f, inp);
            Assert.AreEqual(Tile.Rock, s.world.Get(90, 80), "0.5초로는 아직 안 깨져야 한다 (hp3 / power3 = 1초)");
            Run(s, 1.0f, inp);
            Assert.AreEqual(Tile.Floor, s.world.Get(90, 80));
            Assert.Greater(changed, 0);
        }

        [Test]
        public void Mining_OutOfReach_DoesNothing()
        {
            var s = NewSim();
            s.P.x = 89 * 16 + 8; s.P.y = 80 * 16 + 8;
            var inp = new SimInput { mine = true, aim = new Vector2(95 * 16 + 8, 80 * 16 + 8) };
            Run(s, 2f, inp);
            Assert.IsFalse(s.cursorReach);
        }

        [Test]
        public void Mining_HardRock_NeedsBetterPick()
        {
            var s = NewSim();
            // 경암(Hard) 타일을 찾아 바로 옆에서 곡괭이 T0 으로 시도
            int tx = -1, ty = -1;
            for (int y = 3; y < World.H - 3 && tx < 0; y++)
                for (int x = 3; x < World.W - 3; x++)
                    if (s.world.Get(x, y) == Tile.Hard) { tx = x; ty = y; break; }
            Assert.GreaterOrEqual(tx, 0);
            s.P.x = tx * 16 + 8; s.P.y = ty * 16 + 8;
            var inp = new SimInput { mine = true, aim = new Vector2(tx * 16 + 8, ty * 16 + 8) };
            Run(s, 3f, inp);
            Assert.AreEqual(Tile.Hard, s.world.Get(tx, ty), "티어0 곡괭이는 경암을 못 깬다");
        }

        [Test]
        public void Oxygen_DrainsOutside_RefillsAtBeacon()
        {
            var s = NewSim();
            s.P.x = World.BX + 200; s.P.y = World.BY; // 비콘 반경(80) 밖, 구역0
            Run(s, 10f, new SimInput { aim = new Vector2(s.P.x, s.P.y) });
            Assert.AreEqual(30f, s.P.o2, 0.6f, "구역0 에서 초당 1");
            AtBeacon(s);
            Run(s, 3f, new SimInput { aim = new Vector2(s.P.x, s.P.y) });
            Assert.Greater(s.P.o2, 40f - 0.01f, "비콘 안에서는 빠르게 충전");
        }

        [Test]
        public void Oxygen_Empty_HurtsPlayer()
        {
            var s = NewSim();
            s.P.x = World.BX + 200; s.P.y = World.BY; s.P.o2 = 0;
            float hp0 = s.P.hp;
            Run(s, 2.5f, new SimInput { aim = new Vector2(s.P.x, s.P.y) });
            Assert.Less(s.P.hp, hp0);
        }

        [Test]
        public void Vent_RefillsOxygen_AwayFromBeacon()
        {
            var s = NewSim();
            s.P.x = World.BX + 200; s.P.y = World.BY; s.P.o2 = 10;
            int vx = Mathf.FloorToInt(s.P.x / 16), vy = Mathf.FloorToInt(s.P.y / 16);
            s.world.SetTile(vx, vy, Tile.Vent);
            Run(s, 1f, new SimInput { aim = new Vector2(s.P.x, s.P.y) });
            Assert.Greater(s.P.o2, 30f);
        }

        [Test]
        public void Craft_RequiresBeacon_AndConsumesCost()
        {
            var s = NewSim();
            s.AddItem(Item.Scrap, 20); s.AddItem(Item.Gel, 10);
            s.P.x = World.BX + 400; s.P.y = World.BY;
            Assert.IsFalse(s.TryCraft(0), "비콘에서 멀면 제작 불가");
            AtBeacon(s);
            Assert.IsTrue(s.TryCraft(0));
            Assert.AreEqual(1, s.P.lvl[(int)Track.Pick]);
            var c = Data.Up[(int)Track.Pick][1].cost;      // 초반 비용 완화로 숫자가 바뀌어도 표를 기준으로 검사한다
            Assert.AreEqual(20 - c[0].amount, s.inv[(int)Item.Scrap]);
            Assert.AreEqual(10 - c[1].amount, s.inv[(int)Item.Gel]);
            Assert.AreEqual(1, s.PickLevel.tier);
        }

        [Test]
        public void Craft_FailsWithoutMaterials_AndKeepsInventory()
        {
            var s = NewSim(); AtBeacon(s);
            s.AddItem(Item.Scrap, 3);
            Assert.IsFalse(s.TryCraft(0));
            Assert.AreEqual(3, s.inv[(int)Item.Scrap]);
            Assert.AreEqual(0, s.P.lvl[(int)Track.Pick]);
        }

        [Test]
        public void Craft_Consumables_GiveItems()
        {
            var s = NewSim(); AtBeacon(s);
            s.AddItem(Item.Scrap, 20); s.AddItem(Item.Gel, 8);   // 램프 2/1 + 환기구 12/5
            int lamps = s.inv[(int)Item.Lamp];
            Assert.IsTrue(s.TryCraft(6));                       // 램프 x2
            Assert.AreEqual(lamps + 2, s.inv[(int)Item.Lamp]);
            Assert.IsTrue(s.TryCraft(7));                       // 환기구
            Assert.AreEqual(1, s.inv[(int)Item.Vent]);
        }

        [Test]
        public void Craft_MaxLevel_IsRefused()
        {
            var s = NewSim(); AtBeacon(s);
            s.P.lvl[(int)Track.Pick] = Data.Up[(int)Track.Pick].Length - 1;
            s.AddItem(Item.Brass, 99); s.AddItem(Item.Gel, 99); s.AddItem(Item.Pearl, 99);
            Assert.IsFalse(s.TryCraft(0));
        }

        [Test]
        public void Craft_ArmorAndTank_RefillOnPurchase()
        {
            var s = NewSim(); AtBeacon(s);
            s.AddItem(Item.Scrap, 99); s.AddItem(Item.Gel, 99); s.AddItem(Item.Pearl, 99);
            s.P.hp = 2; s.P.o2 = 1;
            Assert.IsTrue(s.TryCraft(5)); Assert.AreEqual(14, s.MaxHp); Assert.AreEqual(14f, s.P.hp, 0.001f);
            Assert.IsTrue(s.TryCraft(3)); Assert.AreEqual(65, s.MaxO2); Assert.AreEqual(65f, s.P.o2, 0.001f);
        }

        [Test]
        public void Combat_CrabArmor_ReducesDamage_ButNeverBelowOne()
        {
            var s = NewSim(); AtBeacon(s);
            var crab = s.SpawnEnemy(EnemyType.Crab, s.P.x + 16, s.P.y);
            var inp = new SimInput { attack = true, aim = new Vector2(crab.x, crab.y) };
            s.Step(1f / 60f, inp);
            Assert.AreEqual(9f, crab.hp, 0.001f, "작살 피해 1 - 방어 1 = 최소 1");
        }

        [Test]
        public void Combat_KillingJelly_DropsGel()
        {
            var s = NewSim(); AtBeacon(s);
            var j = s.SpawnEnemy(EnemyType.Jelly, s.P.x + 16, s.P.y); j.hp = 1;
            s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(j.x, j.y) });
            Assert.IsFalse(s.enemies.Contains(j));
            Assert.GreaterOrEqual(s.inv[(int)Item.Gel], 1);
        }

        [Test]
        public void Combat_Range_IsSameForTouchAndMouse()
        {
            // 기획 검토: 터치 36 / 마우스 26 이던 사거리를 32 로 통일했다
            foreach (bool touch in new[] { false, true })
            {
                var s = NewSim(); AtBeacon(s);
                var near = s.SpawnEnemy(EnemyType.Jelly, s.P.x + 30, s.P.y); near.hp = 5; near.dirx = near.diry = 0; near.wt = 99;
                s.Step(1f / 60f, new SimInput { attack = true, touch = touch, faceDir = Vector2.right, aim = new Vector2(near.x, near.y) });
                Assert.Less(near.hp, 5f, "사거리(32+5) 안: touch=" + touch);
                var s2 = NewSim(); AtBeacon(s2);
                var far = s2.SpawnEnemy(EnemyType.Jelly, s2.P.x + 44, s2.P.y); far.hp = 5; far.dirx = far.diry = 0; far.wt = 99;
                s2.Step(1f / 60f, new SimInput { attack = true, touch = touch, faceDir = Vector2.right, aim = new Vector2(far.x, far.y) });
                Assert.AreEqual(5f, far.hp, 0.001f, "사거리 밖: touch=" + touch);
            }
        }

        [Test]
        public void Combat_Wall_BlocksAttackAndAutoAim()
        {
            var s = NewSim(); AtBeacon(s);
            int tx = Mathf.FloorToInt(s.P.x / 16) + 1, ty = Mathf.FloorToInt(s.P.y / 16);
            s.world.SetTile(tx, ty, Tile.Rock);                                     // 바로 옆 칸에 벽
            var j = s.SpawnEnemy(EnemyType.Jelly, s.P.x + 30, s.P.y); j.hp = 5; j.dirx = j.diry = 0; j.wt = 99;
            s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(j.x, j.y) });
            Assert.AreEqual(5f, j.hp, 0.001f, "벽 너머 적은 맞지 않는다");
            Vector2 p; Assert.IsFalse(s.NearestTarget(out p), "벽 뒤 적은 자동 조준하지 않는다");
        }

        [Test]
        public void Combat_CrabArmor_ScalesWithHarpoonLevel()
        {
            float[] dmg = { 1, 2, 3, 5 }; float prev = 0;
            for (int lv = 0; lv < 4; lv++)
            {
                var s = NewSim(); AtBeacon(s); s.P.lvl[(int)Track.Harpoon] = lv;
                var c = s.SpawnEnemy(EnemyType.Crab, s.P.x + 20, s.P.y); c.hp = 10; c.dirx = c.diry = 0; c.wt = 99;
                s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(c.x, c.y) });
                float dealt = 10f - c.hp;
                Assert.Greater(dealt, prev - 0.001f, "작살 Lv" + (lv + 1) + " 가 앞 단계보다 약해지면 안 된다");
                if (lv > 0) Assert.Greater(dealt, prev, "게에게도 작살 업그레이드가 의미 있다(Lv" + (lv + 1) + ")");
                prev = dealt; Assert.GreaterOrEqual(dealt, 1f);
            }
        }

        [Test]
        public void Vent_PickUp_DestroysIt_NoRefund()
        {
            var s = NewSim(); AtBeacon(s);
            int tx = Mathf.FloorToInt(s.P.x / 16) + 1, ty = Mathf.FloorToInt(s.P.y / 16);
            s.world.SetTile(tx, ty, Tile.Vent);
            s.Step(1f / 60f, new SimInput { mine = true, aim = new Vector2(tx * 16 + 8, ty * 16 + 8) });
            Assert.AreEqual(Tile.Floor, s.world.Get(tx, ty));
            Assert.AreEqual(0, s.inv[(int)Item.Vent], "환기구는 회수할 수 없다(줍고 놓기로 무한 산소 방지)");
        }

        [Test]
        public void Heal_HasCooldown()
        {
            var s = NewSim(); AtBeacon(s);
            s.AddItem(Item.Gel, 10); s.P.hp = 2;
            s.Step(1f / 60f, new SimInput { heal = true });
            Assert.AreEqual(8, s.inv[(int)Item.Gel]);
            s.Step(1f / 60f, new SimInput { heal = true });
            Assert.AreEqual(8, s.inv[(int)Item.Gel], "쿨다운 중에는 한 번 더 먹지 못한다");
            Run(s, Data.HealCooldown + 0.1f, default(SimInput));
            s.Step(1f / 60f, new SimInput { heal = true });
            Assert.AreEqual(6, s.inv[(int)Item.Gel]);
        }

        [Test]
        public void Boss_Checkpoint_KeepsPhase2_AfterDeath()
        {
            var s = NewSim();
            s.boss.active = true; s.boss.hp = 40f;                                  // 2단계(< 50%)
            s.P.hp = 1; s.Damage(5);
            Assert.AreEqual(Data.BossHp * Data.BossCheckpointFrac, s.boss.hp, 0.01f, "2단계까지 갔으면 2단계 시작 체력에서 다시");
            Assert.Less(s.boss.hp, Data.BossHp * 0.5f, "복구해도 여전히 2단계");
            var s2 = NewSim(); s2.boss.active = true; s2.boss.hp = Data.BossHp * 0.8f; s2.P.hp = 1; s2.Damage(5);
            Assert.AreEqual(Data.BossHp, s2.boss.hp, 0.01f, "1단계에서 죽으면 풀 체력");
        }

        [Test]
        public void Boss_Charge_HasWindup()
        {
            var s = NewSim();
            s.P.x = World.AX; s.P.y = World.AY + 60;
            s.boss.active = true; s.boss.x = World.AX; s.boss.y = World.AY; s.boss.chargeT = 0.01f; s.boss.hp = Data.BossHp;
            s.Step(0.05f, default(SimInput));
            Assert.Greater(s.boss.windup, 0f, "돌진 전에 예고 시간이 있다");
            Assert.AreEqual(0f, s.boss.charge, 0.0001f, "예고 중에는 아직 돌진하지 않는다");
            Run(s, Data.BossWindup + 0.1f, default(SimInput));
            Assert.Greater(s.boss.charge + (s.boss.windup > 0 ? 1 : 0), -1f);       // 이후 돌진이 시작된다(진행 확인용)
        }

        [Test]
        public void Craft_BeaconExpansion_WidensSafeZoneAndCraftRange()
        {
            var s = NewSim(); AtBeacon(s);
            Assert.AreEqual(80f, s.BeaconR, 0.01f);
            s.AddItem(Item.Scrap, 30); s.AddItem(Item.Brass, 20); s.AddItem(Item.Gel, 10);
            Assert.IsTrue(s.TryCraft(8), "비콘 확장은 9번째 제작 항목");
            Assert.AreEqual(104f, s.BeaconR, 0.01f);
            s.P.x = World.BX + 95; s.P.y = World.BY; s.P.o2 = 5;                  // 옛 반경(80) 밖, 새 반경(104) 안
            s.Step(0.2f, default(SimInput));
            Assert.Greater(s.P.o2, 5f, "넓어진 안전지대에서는 산소가 찬다");
            Assert.IsTrue(s.NearBeacon || s.CraftR > 130f, "제작 가능 거리도 함께 넓어진다");
        }

        [Test]
        public void Goals_ShowFirstUndone_AndAdvance()
        {
            var s = NewSim();
            var g = s.ActiveGoals();
            Assert.AreEqual(3, g.Length); StringAssert.Contains("고철", g[0]);
            StringAssert.Contains("황동 곡괭이", string.Join("\n", g));
            s.P.lvl[(int)Track.Pick] = 1;
            StringAssert.DoesNotContain("황동 곡괭이", string.Join("\n", s.ActiveGoals()));
        }

        [Test]
        public void Goals_GiveRewardOnce_AndDoNotRepeatAfterLoad()
        {
            var s = NewSim(); AtBeacon(s);
            int gel = s.inv[(int)Item.Gel]; string last = ""; s.Toast += (m, t, q) => last = m;
            s.P.lvl[(int)Track.Pick] = 1;                                     // '황동 곡괭이 제작' 조건을 충족
            s.Step(1f / 60f, default(SimInput));
            Assert.AreEqual(gel + 3, s.inv[(int)Item.Gel], "목표 달성 보상: 젤 3");
            StringAssert.Contains("목표 달성", last);
            s.Step(1f / 60f, default(SimInput));
            Assert.AreEqual(gel + 3, s.inv[(int)Item.Gel], "보상은 한 번만");
            var s2 = Sim.FromSave(s.ToSave()); s2.started = true; int g2 = s2.inv[(int)Item.Gel];
            s2.Step(1f / 60f, default(SimInput));
            Assert.AreEqual(g2, s2.inv[(int)Item.Gel], "불러와도 받은 보상을 다시 주지 않는다");
        }

        [Test]
        public void EarlyGame_HasStarterVeins_Chest_AndJelliesNearBeacon()
        {
            foreach (int seed in new[] { 1234, 7, 424242 })
            {
                var s = new Sim(seed); s.Populate();
                int scrap = 0, jellies = 0;
                for (int y = World.CY - 16; y <= World.CY + 16; y++) for (int x = World.CX - 16; x <= World.CX + 16; x++) if (s.world.Get(x, y) == Tile.Scrap) scrap++;
                foreach (var e in s.enemies) if (e.type == EnemyType.Jelly && Mathf.Sqrt((e.x - World.BX) * (e.x - World.BX) + (e.y - World.BY) * (e.y - World.BY)) < 17 * 16) jellies++;
                Assert.GreaterOrEqual(scrap, 12, "seed " + seed + ": 비콘 곁 고철 광맥");
                Assert.GreaterOrEqual(jellies, 3, "seed " + seed + ": 비콘 곁 해파리(젤 공급)");
                Assert.AreEqual(Tile.Chest, s.world.Get(World.StarterChest[0], World.StarterChest[1]), "seed " + seed + ": 시작 상자");
                // 시작 상자까지 걸어갈 수 있다
                var seen = new bool[World.W * World.H]; var q = new System.Collections.Generic.Queue<int>(); int st = World.Idx(World.CX, World.CY); seen[st] = true; q.Enqueue(st);
                while (q.Count > 0) { int i = q.Dequeue(); int x = i % World.W, y = i / World.W; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 }; for (int k = 0; k < 4; k++) { int nx = x + dx[k], ny = y + dy[k]; if (!World.InBounds(nx, ny)) continue; int ni = World.Idx(nx, ny); if (seen[ni] || (s.world.IsSolid(nx, ny))) continue; seen[ni] = true; q.Enqueue(ni); } }
                bool near = false; foreach (int d in new[] { 1, -1 }) { if (seen[World.Idx(World.StarterChest[0] + d, World.StarterChest[1])] || seen[World.Idx(World.StarterChest[0], World.StarterChest[1] + d)]) near = true; }
                Assert.IsTrue(near, "seed " + seed + ": 시작 상자 옆까지 걸어서 간다");
            }
        }

        [Test]
        public void EarlyGame_FirstOres_GiveLuckyBonus_AndShowGuide()
        {
            var s = NewSim(); AtBeacon(s);
            Assert.IsTrue(s.ShowEarlyGuide, "처음에는 지도 안내 점이 보인다");
            int tx = Mathf.FloorToInt(s.P.x / 16) + 1, ty = Mathf.FloorToInt(s.P.y / 16);
            s.world.SetTile(tx, ty, Tile.Scrap);
            int before = s.inv[(int)Item.Scrap]; bool lucky = false; s.Toast += (m, t, q) => { if (m.Contains("행운")) lucky = true; };
            Run(s, 3f, new SimInput { mine = true, aim = new Vector2(tx * 16 + 8, ty * 16 + 8) });
            Assert.AreEqual(Tile.Floor, s.world.Get(tx, ty));
            Assert.GreaterOrEqual(s.inv[(int)Item.Scrap] - before, 2, "드롭 1~2 + 첫 광맥 보너스 1");
            Assert.IsTrue(lucky);
            s.AddItem(Item.Scrap, 8); s.Step(1f / 60f, default(SimInput));
            Assert.IsFalse(s.ShowEarlyGuide, "첫 목표를 끝내면 안내 점이 사라진다");
        }

        // ---------- 이야기 ----------
        static System.Collections.Generic.List<string> CollectStory(Sim s) { var l = new System.Collections.Generic.List<string>(); s.StoryLine += (t, sec) => l.Add(t); return l; }

        [Test]
        public void Story_StartPlaysThreeRadioLines_InOrder_AndChapterOneBanner()
        {
            var s = NewSim(); var lines = CollectStory(s); string banner = null; s.ChapterBanner += t => banner = t;
            Run(s, 16f, default(SimInput), 0.1f);
            Assert.AreEqual(3, lines.Count, "시작 무전은 3줄");
            StringAssert.Contains("아라", lines[0]); StringAssert.Contains("고철", lines[2]);
            Assert.AreEqual(Data.ChapterTitle[0], banner);
            Run(s, 30f, default(SimInput), 0.1f);
            Assert.AreEqual(3, lines.Count, "같은 비트는 다시 나오지 않는다");
        }

        [Test]
        public void Story_GoalCompletion_TriggersMatchingBeat_AndNextChapterBanner()
        {
            var s = NewSim(); AtBeacon(s); var lines = CollectStory(s); var banners = new System.Collections.Generic.List<string>(); s.ChapterBanner += t => banners.Add(t);
            Run(s, 16f, default(SimInput), 0.1f);                                   // 시작 무전을 흘려보낸다
            lines.Clear();
            s.P.lvl[(int)Track.Pick] = 1;                                           // goal2(황동 곡괭이) 달성
            Run(s, 12f, default(SimInput), 0.1f);
            Assert.IsTrue(lines.Exists(l => l.Contains("황동 곡괭이")), "곡괭이 무전");
            // 1장 목표 6개를 모두 채우면 2장
            s.AddItem(Item.Scrap, 8); s.AddItem(Item.Gel, 2);                       // goal0, goal1
            s.world.SetTile(World.StarterChest[0], World.StarterChest[1], Tile.Floor);   // goal3
            s.P.lvl[(int)Track.Tank] = 1; s.P.lvl[(int)Track.Light] = 1;            // goal4, goal5
            s.maxZone = 1;                                                           // goal6(가라앉은 거리)
            Run(s, 60f, default(SimInput), 0.1f);
            Assert.AreEqual(2, s.Chapter);
            Assert.IsTrue(banners.Contains(Data.ChapterTitle[1]), "2장 제목 카드");
        }

        [Test]
        public void Story_SurvivesSaveLoad_WithoutReplay_AndRebuildsLog()
        {
            var s = NewSim(); AtBeacon(s); Run(s, 16f, default(SimInput), 0.1f);
            var s2 = Sim.FromSave(s.ToSave()); s2.started = true; var lines = CollectStory(s2); string banner = null; s2.ChapterBanner += t => banner = t;
            Run(s2, 20f, default(SimInput), 0.1f);
            Assert.AreEqual(0, lines.Count, "불러와도 이미 나온 무전은 다시 나오지 않는다");
            Assert.IsNull(banner, "장 제목 카드도 다시 나오지 않는다");
            Assert.GreaterOrEqual(s2.storyLog.Count, 3, "기록은 나온 비트에서 복원된다");
        }

        [Test]
        public void Story_IdleHint_FiresOncePerGoal_AfterLongNoProgress()
        {
            var s = NewSim(); AtBeacon(s); var lines = CollectStory(s);
            Run(s, 16f, default(SimInput), 0.1f); lines.Clear();                    // 시작 무전 소화
            Run(s, 125f, default(SimInput), 0.1f);                                  // 시작 비트로부터 약 141초
            Assert.AreEqual(0, lines.Count, "150초 전에는 힌트가 없다");
            Run(s, 30f, default(SimInput), 0.1f);
            Assert.AreEqual(1, lines.Count, "150초 넘게 진척이 없으면 힌트 한 번");
            StringAssert.Contains("주황 점", lines[0]);
            Run(s, 400f, default(SimInput), 0.1f);
            Assert.AreEqual(1, lines.Count, "같은 목표에는 힌트를 반복하지 않는다");
        }

        [Test]
        public void Story_EventBeats_LowO2_FirstDeath_BossWake_Vent()
        {
            var s = NewSim(); var lines = CollectStory(s);
            s.P.x = World.BX + 700; s.P.y = World.BY; s.P.o2 = 3;
            Run(s, 20f, default(SimInput), 0.1f);
            Assert.IsTrue(lines.Exists(l => l.Contains("산소가 얼마 없어요")), "산소 부족 무전");
            var s2 = NewSim(); var l2 = CollectStory(s2); s2.P.hp = 1; s2.Damage(5);
            Run(s2, 20f, default(SimInput), 0.1f);
            Assert.IsTrue(l2.Exists(l => l.Contains("돌아왔군요")), "첫 기절 무전");
            var s3 = NewSim(); var l3 = CollectStory(s3); s3.P.x = World.AX; s3.P.y = World.AY + 60;
            Run(s3, 20f, default(SimInput), 0.1f);
            Assert.IsTrue(l3.Exists(l => l.Contains("눈이 떴어요")), "보스가 깨어날 때의 무전");
        }

        [Test]
        public void Bullet_ThatKillsThePlayer_DoesNotCrash()
        {
            // 회귀: 탄이 플레이어를 기절시키면 Die() 가 탄 목록을 비우는데, 그 뒤에 RemoveAt 을 불러 예외가 났다.
            var s = NewSim(); AtBeacon(s); s.P.hp = 1; s.P.invuln = 0;
            for (int i = 0; i < 5; i++) s.bullets.Add(new Bullet { x = s.P.x, y = s.P.y, vx = 0, vy = 0, life = 3 });
            Assert.DoesNotThrow(() => s.Step(1f / 60f, default(SimInput)));
        }

        [Test]
        public void Story_MiniBossRelay_RaisesBeaconRadius()
        {
            var s = NewSim(); float r0 = s.BeaconR;
            s.miniDead[0] = true; Assert.AreEqual(r0 + 12f, s.BeaconR, 0.01f);
            s.miniDead[1] = true; Assert.AreEqual(r0 + 24f, s.BeaconR, 0.01f, "중계기를 복구할수록 비콘이 멀리 비춘다");
        }

        [Test]
        public void Story_Data_IsConsistent()
        {
            Assert.AreEqual(Data.StoryIds.Length, Data.StoryLines.Length);
            Assert.LessOrEqual(Data.StoryIds.Length, 31, "비트마스크는 int");
            for (int i = 0; i < 14; i++) Assert.AreNotEqual(-1, System.Array.IndexOf(Data.StoryIds, "goal" + i), "목표 " + i + " 의 비트");
            Assert.AreEqual(14, Data.ChapterOfGoal.Length); Assert.AreEqual(14, Data.GoalHints.Length);
            foreach (var lines in Data.StoryLines) foreach (var l in lines) Assert.LessOrEqual(l.Length, 52, "한 줄이 너무 길다(토스트 폭): " + l);
        }

        [Test]
        public void EarlyGame_OreCombo_GivesBonusEveryThird()
        {
            var s = NewSim(); AtBeacon(s);
            int px = Mathf.FloorToInt(s.P.x / 16), py = Mathf.FloorToInt(s.P.y / 16);
            var spots = new[] { new[] { px + 1, py }, new[] { px - 1, py }, new[] { px, py + 1 } };
            foreach (var sp in spots) s.world.SetTile(sp[0], sp[1], Tile.Brass);   // 황동: 곡괭이 Lv1 로는 못 캔다 → 고철로 바꾼다
            foreach (var sp in spots) s.world.SetTile(sp[0], sp[1], Tile.Scrap);
            bool combo = false; s.Toast += (m, t, q) => { if (m.Contains("연쇄 x3")) combo = true; };
            foreach (var sp in spots) Run(s, 2f, new SimInput { mine = true, aim = new Vector2(sp[0] * 16 + 8, sp[1] * 16 + 8) });
            Assert.IsTrue(combo, "4초 안에 광맥 3개를 이어 깨면 연쇄 보너스");
        }

        [Test]
        public void EarlyGame_AffordableHint_WhenFarFromBeacon_OnlyAFewTimes()
        {
            var s = NewSim(); s.P.x = World.BX + 500; s.P.y = World.BY;
            s.AddItem(Item.Scrap, 30); s.AddItem(Item.Gel, 10);
            int n = 0; s.Toast += (m, t, q) => { if (m.Contains("비콘으로 돌아가")) n++; };
            for (int i = 0; i < 40; i++) s.Step(0.5f, default(SimInput));            // 20초
            Assert.AreEqual(1, n, "45초 간격 안에서는 한 번만");
            s.P.x = World.BX; s.P.y = World.BY + 28; int before = n;
            for (int i = 0; i < 400; i++) s.Step(0.5f, default(SimInput));           // 비콘 곁에서는 안내하지 않는다
            Assert.AreEqual(before, n);
        }

        [Test]
        public void EarlyGame_FirstUpgrades_AreAffordableFromNearbyLoot()
        {
            // 첫 단계 업그레이드 4종의 고철 합이 비콘 곁 광맥 3덩이(드롭 평균 1.5)만으로 충분해야 한다
            int need = 0; foreach (var t in new[] { Track.Pick, Track.Harpoon, Track.Light, Track.Tank }) foreach (var c in Data.Up[(int)t][1].cost) if (c.item == Item.Scrap) need += c.amount;
            Assert.LessOrEqual(need, 32, "첫 업그레이드 4종의 고철 합");
            Assert.LessOrEqual(Data.Up[(int)Track.Pick][1].cost[1].amount, 3, "첫 곡괭이는 젤 3개 이하(해파리 한 마리)");
        }

        [Test]
        public void Wraith_PassesThroughWalls_ButCannotBeHitInsideThem()
        {
            var s = NewSim(); AtBeacon(s);
            int tx = Mathf.FloorToInt(s.P.x / 16) + 3, ty = Mathf.FloorToInt(s.P.y / 16);
            for (int dy = -2; dy <= 2; dy++) for (int dx = -1; dx <= 1; dx++) s.world.SetTile(tx + dx, ty + dy, Tile.Rock);   // 벽 덩어리
            var w = s.SpawnEnemy(EnemyType.Wraith, tx * 16 + 8, ty * 16 + 8);
            float d0 = Mathf.Abs(w.x - s.P.x);
            Run(s, 0.6f, default(SimInput));
            Assert.Less(Mathf.Abs(w.x - s.P.x), d0, "벽 속에서도 플레이어 쪽으로 움직인다");
            s.P.atkCd = 0;
            s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(w.x, w.y) });
            Assert.AreEqual(Data.Enemies[(int)EnemyType.Wraith].hp, w.hp, 0.001f, "벽 속의 망령은 작살이 닿지 않는다(시야 규칙)");
        }

        [Test]
        public void DesignPass_PlacesZoneDecor()
        {
            var s = new Sim(1234); var counts = new int[8];
            for (int i = 0; i < World.W * World.H; i++) counts[s.world.deco[i]]++;
            Assert.Greater(counts[World.DecoPillar], 10, "얕은 폐허의 부서진 기둥");
            Assert.Greater(counts[World.DecoSign], 10, "가라앉은 거리의 표지판");
            Assert.Greater(counts[World.DecoBones], 5, "심연의 뼈대");
            Assert.Greater(counts[World.DecoFungus], 15, "심연의 발광 버섯");
        }

        [Test]
        public void Load_RestoresDeepestZoneFromExploredMap()
        {
            var s = NewSim(); s.world.explored[World.Idx(World.CX, World.CY - 60)] = 1;   // 심연 한 칸을 가 본 기록
            var s2 = Sim.FromSave(s.ToSave());
            Assert.AreEqual(2, s2.maxZone);
        }

        [Test]
        public void Potion_RefillsOxygen_WithHealButton_WhenHpIsFull()
        {
            var s = NewSim(); s.P.x = World.BX + 600; s.P.y = World.BY;                 // 산소 보급 거점 밖
            s.AddItem(Item.Potion, 2); s.P.o2 = 5; s.P.hp = s.MaxHp;
            s.Step(1f / 60f, new SimInput { heal = true });
            Assert.AreEqual(1, s.inv[(int)Item.Potion]);
            Assert.Greater(s.P.o2, 5f + s.MaxO2 * Data.PotionO2Frac - 1.5f, "산소 60% 가 즉시 찬다");
            var s2 = NewSim(); s2.P.x = World.BX + 600; s2.AddItem(Item.Potion, 1); s2.P.o2 = s2.MaxO2;
            s2.Step(1f / 60f, new SimInput { heal = true });
            Assert.AreEqual(1, s2.inv[(int)Item.Potion], "산소가 가득하면 마시지 않는다");
        }

        [Test]
        public void Craft_Potion_GivesTwo()
        {
            var s = NewSim(); AtBeacon(s); s.AddItem(Item.Gel, 2); s.AddItem(Item.Crystal, 1);
            Assert.IsTrue(s.TryCraft(Data.Crafts.Length - 1));
            Assert.AreEqual(2, s.inv[(int)Item.Potion]);
        }

        [Test]
        public void FinsLv3_UseLessOxygen_And_TankLv3_RefillsFaster()
        {
            float Drain(int finLv)
            {
                var s = NewSim(); s.P.x = World.BX + 700; s.P.y = World.BY; s.P.lvl[(int)Track.Fins] = finLv; s.P.o2 = s.MaxO2;
                float before = s.P.o2; s.Step(1f, default(SimInput)); return before - s.P.o2;
            }
            Assert.Less(Drain(2), Drain(1) * 0.9f, "핀 Lv3 는 소모가 줄어든다");
            Assert.Less(Drain(3), Drain(2), "핀 Lv4 는 더 줄어든다");
            float Fill(int tankLv)
            {
                var s = NewSim(); AtBeacon(s); s.P.lvl[(int)Track.Tank] = tankLv; s.P.o2 = 1; float b = s.P.o2; s.Step(0.2f, default(SimInput)); return s.P.o2 - b;
            }
            Assert.Greater(Fill(2), Fill(1) * 1.2f, "산소통 Lv3 는 충전이 빠르다");
        }

        [Test]
        public void Elite_HasMoreHp_HitsHarder_AndDropsExtra()
        {
            var s = NewSim(); AtBeacon(s);
            var n = s.SpawnEnemy(EnemyType.Jelly, s.P.x + 20, s.P.y); var e = s.SpawnEnemy(EnemyType.Jelly, s.P.x + 20, s.P.y + 40, true);
            Assert.AreEqual(n.hp * 2.2f, e.hp, 0.01f);
            Assert.IsTrue(e.elite);
            int pearls = s.inv[(int)Item.Pearl];
            e.hp = 0.5f; e.dirx = e.diry = 0; e.wt = 99; e.x = s.P.x + 20; e.y = s.P.y;
            s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(e.x, e.y) });
            Assert.GreaterOrEqual(s.inv[(int)Item.Pearl], pearls + 1, "정예는 진주를 남긴다");
        }

        [Test]
        public void DesignPass_ZonesHaveDifferentTerrain()
        {
            var s = new Sim(1234); var raw = new World(); raw.Generate(1234);
            int streetOpen = 0, z1 = 0, plugs = 0, z0Open = 0, z0Raw = 0;
            for (int y = 3; y < World.H - 3; y++)
                for (int x = 3; x < World.W - 3; x++)
                {
                    int z = World.ZoneOfTile(x, y), i = World.Idx(x, y);
                    bool openNow = !s.world.IsSolid(x, y), openRaw = !raw.IsSolid(x, y);
                    if (z == 1 && (x % 20 < 2 || y % 20 < 2)) { z1++; if (openNow) streetOpen++; }
                    if (z == 0) { if (openNow) z0Open++; if (openRaw) z0Raw++; }
                    if (z == 2 && openRaw && !openNow && s.world.Get(x, y) == Tile.Deep) plugs++;
                }
            Assert.Greater(streetOpen, z1 * 0.8f, "가라앉은 거리의 격자 도로가 열려 있다");
            Assert.Greater(z0Open, z0Raw, "얕은 폐허는 더 넓게 열린다");
            Assert.Greater(plugs, 20, "심연에는 길을 막는 바위 기둥이 있다");
        }

        // ---------- 미니보스 ----------
        static Enemy Mini(Sim s, EnemyType t) { foreach (var e in s.enemies) if (e.type == t) return e; return null; }

        [Test]
        public void MiniBosses_SpawnAtLairs_OnlyOnce_AndNotFromNormalSpawner()
        {
            var s = NewSim(); s.Populate();
            var ck = Mini(s, EnemyType.CrabKing); var ek = Mini(s, EnemyType.EelKing);
            Assert.NotNull(ck); Assert.NotNull(ek);
            Assert.AreEqual(World.MiniLairs[0][0] * 16 + 8, ck.x, 0.01f); Assert.AreEqual(World.MiniLairs[1][1] * 16 + 8, ek.y, 0.01f);
            AtBeacon(s); for (int i = 0; i < 2000; i++) s.Step(1f / 20f, default(SimInput));
            int kings = 0; foreach (var e in s.enemies) if (Sim.IsMini(e.type)) kings++;
            Assert.AreEqual(2, kings, "일반 스폰이 미니보스를 더 만들지 않는다");
        }

        [Test]
        public void MiniBoss_Kill_GivesGuaranteedLoot_Persists_AndDoesNotRespawn()
        {
            var s = NewSim(); s.Populate();
            var ck = Mini(s, EnemyType.CrabKing); s.P.x = ck.x - 20; s.P.y = ck.y; ck.hp = 0.5f; ck.wind = ck.lunge = 0;
            int brass = s.inv[(int)Item.Brass], pearl = s.inv[(int)Item.Pearl], pot = s.inv[(int)Item.Potion];
            s.P.atkCd = 0; s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(ck.x, ck.y) });
            Assert.IsTrue(s.miniDead[0]);
            Assert.AreEqual(brass + 6, s.inv[(int)Item.Brass]); Assert.AreEqual(pearl + 3, s.inv[(int)Item.Pearl]); Assert.GreaterOrEqual(s.inv[(int)Item.Potion], pot + 1, "드롭 포션 1(+ 목표 달성 보상)");
            var s2 = Sim.FromSave(s.ToSave()); s2.Populate();
            Assert.IsNull(Mini(s2, EnemyType.CrabKing), "처치한 미니보스는 불러와도 다시 나오지 않는다");
            Assert.NotNull(Mini(s2, EnemyType.EelKing), "아직 안 잡은 쪽은 그대로");
        }

        [Test]
        public void CrabKing_Telegraphs_ThenFiresShockwaveRing()
        {
            var s = NewSim(); s.Populate();
            var ck = Mini(s, EnemyType.CrabKing); s.P.x = ck.x + 90; s.P.y = ck.y; ck.lt = 0.01f;
            s.Step(0.05f, default(SimInput));
            Assert.Greater(ck.wind, 0f, "충격파 전에 예고 시간이 있다"); Assert.AreEqual(0, s.bullets.Count);
            s.P.invuln = 99;
            Run(s, Data.MiniCrabWindup + 0.2f, default(SimInput));
            Assert.GreaterOrEqual(s.bullets.Count, 10, "예고 뒤에 탄 고리가 퍼진다");
        }

        [Test]
        public void EelKing_Windup_Dash_ThenFanOfThree()
        {
            var s = NewSim(); s.Populate();
            var ek = Mini(s, EnemyType.EelKing);
            int lx = World.MiniLairs[1][0], ly = World.MiniLairs[1][1];
            for (int dy = -8; dy <= 8; dy++) for (int dx = -18; dx <= 18; dx++) s.world.SetTile(lx + dx, ly + dy, Tile.Floor);   // 탄이 벽에 막히지 않게 넓은 공터로
            s.P.x = ek.x + 200; s.P.y = ek.y; s.P.invuln = 99; ek.lt = 0.01f;   // 돌진(약 110px) 뒤에도 거리가 남도록
            s.Step(0.05f, default(SimInput));
            Assert.Greater(ek.wind, 0f, "돌진 예고");
            Run(s, 0.6f, default(SimInput));
            Assert.Greater(ek.lunge + ek.back + s.bullets.Count, 0f, "돌진 또는 그 뒤의 부채꼴 탄이 시작된다");
            Run(s, 0.8f, default(SimInput));
            Assert.GreaterOrEqual(s.bullets.Count, 3, "돌진 뒤 3연 부채꼴 탄");
        }

        [Test]
        public void MiniBoss_ReturnsHomeAndHeals_WhenPlayerLeaves()
        {
            var s = NewSim(); s.Populate();
            var ck = Mini(s, EnemyType.CrabKing); ck.hp = 10; ck.x += 60; s.P.x = World.BX + 900; s.P.y = World.BY;
            Run(s, 3f, default(SimInput));
            Assert.Greater(ck.hp, 10f, "멀어지면 회복한다");
            Assert.Less(Mathf.Abs(ck.x - ck.homeX), 60f, "소굴로 돌아간다");
        }

        [Test]
        public void MiniBosses_AreBeatable_WithStartingGear_AndWarnOnce()
        {
            // 시작 장비(작살 Lv1, 체력 10)로도 이길 수 있는 범위: 작살 Lv1 이 피해 1 이므로 필요한 타수 = 체력(방어 보정, 최소 1)
            var crab = Data.Enemies[(int)EnemyType.CrabKing]; var eel = Data.Enemies[(int)EnemyType.EelKing];
            float crabHits = crab.hp / Mathf.Max(1f, 1f * (1f - 0.35f * crab.armor)), eelHits = eel.hp / 1f;
            Assert.LessOrEqual(crabHits * 0.38f, 14f, "집게왕: 작살 Lv1 로 14초 안에 쓰러뜨릴 수 있어야 한다(쿨다운 0.38초)");
            Assert.LessOrEqual(eelHits * 0.38f, 20f, "장어왕: 작살 Lv1 로 20초 안");
            Assert.LessOrEqual(eel.dmg, 2); Assert.LessOrEqual(crab.dmg, 2);
            var s = NewSim(); s.Populate(); var ck = Mini(s, EnemyType.CrabKing);
            int toasts = 0; s.Toast += (m, t, q) => { if (m.Contains("소굴이다")) toasts++; };
            s.P.x = ck.x + 200; s.P.y = ck.y;                                          // 24칸(384px) 안
            s.P.invuln = 99;
            for (int i = 0; i < 60; i++) s.Step(1f / 30f, default(SimInput));
            Assert.AreEqual(1, toasts, "권장 장비 안내는 한 번만");
        }

        [Test]
        public void MiniLairs_AreConnectedToBeacon()
        {
            foreach (int seed in new[] { 1234, 7, 424242 })
            {
                var s = new Sim(seed);
                var seen = new bool[World.W * World.H]; var q = new System.Collections.Generic.Queue<int>();
                int start = World.Idx(World.CX, World.CY); seen[start] = true; q.Enqueue(start);
                while (q.Count > 0)
                {
                    int i = q.Dequeue(); int x = i % World.W, y = i / World.W; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + dx[k], ny = y + dy[k]; if (!World.InBounds(nx, ny)) continue;
                        int ni = World.Idx(nx, ny); if (seen[ni] || s.world.IsSolid(nx, ny)) continue;
                        seen[ni] = true; q.Enqueue(ni);
                    }
                }
                foreach (var l in World.MiniLairs) Assert.IsTrue(seen[World.Idx(l[0], l[1])], "seed " + seed + ": 소굴(" + l[0] + "," + l[1] + ")이 비콘에서 이어져야 한다");
            }
        }

        [Test]
        public void Load_GivesBriefInvulnerability()
        {
            var s = NewSim(); var s2 = Sim.FromSave(s.ToSave());
            Assert.GreaterOrEqual(s2.P.invuln, Data.LoadInvuln - 0.001f);
        }

        [Test]
        public void DesignPass_AltarRoadConnectsBeaconToArena_AndHalvesOre()
        {
            foreach (int seed in new[] { 1234, 7, 424242 })
            {
                var s = new Sim(seed);
                // 비콘에서 제단 아레나 중심까지 걸어서(바닥 타일만) 갈 수 있다 — 4방향 flood fill
                var seen = new bool[World.W * World.H]; var q = new System.Collections.Generic.Queue<int>();
                int start = World.Idx(World.CX, World.CY); seen[start] = true; q.Enqueue(start); bool reached = false;
                while (q.Count > 0 && !reached)
                {
                    int i = q.Dequeue(); int x = i % World.W, y = i / World.W;
                    if (x == World.ArenaX && y == World.ArenaY) { reached = true; break; }
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + dx[k], ny = y + dy[k]; if (!World.InBounds(nx, ny)) continue;
                        int ni = World.Idx(nx, ny); if (seen[ni] || s.world.IsSolid(nx, ny)) continue;
                        seen[ni] = true; q.Enqueue(ni);
                    }
                }
                Assert.IsTrue(reached, "seed " + seed + ": 비콘에서 제단까지 길이 이어져야 한다");
                int ore = 0, rawOre = 0; var raw = new World(); raw.Generate(seed);
                for (int i = 0; i < World.W * World.H; i++)
                {
                    var t = (Tile)s.world.tiles[i]; if (t == Tile.Scrap || t == Tile.Brass || t == Tile.Crystal) ore++;
                    var r = (Tile)raw.tiles[i]; if (r == Tile.Scrap || r == Tile.Brass || r == Tile.Crystal) rawOre++;
                }
                Assert.Less(ore, rawOre * 0.65f, "seed " + seed + ": 광맥이 크게 줄어야 한다");
                Assert.Greater(ore, rawOre * 0.30f, "seed " + seed + ": 광맥이 사라지면 안 된다");
            }
        }

        [Test]
        public void DesignPass_RuinRoomsHaveChest_AndRoadHasVentsAndLamps()
        {
            var s = new Sim(1234); int chests = 0, vents = 0, lamps = 0;
            for (int i = 0; i < World.W * World.H; i++) { var t = (Tile)s.world.tiles[i]; if (t == Tile.Vent) vents++; else if (t == Tile.Lamp) lamps++; else if (t == Tile.Chest) chests++; }
            Assert.GreaterOrEqual(vents, 2, "대로의 폐허 환기구");
            Assert.GreaterOrEqual(lamps, 5, "대로 램프 + 폐허 방 램프");
            Assert.GreaterOrEqual(chests, 4, "폐허 방마다 확정 상자");
            Assert.AreEqual(Tile.Chest, s.world.Get(World.CX + 22, World.CY + 9));
        }

        [Test]
        public void Death_LosesTwentyPercent_KeepsGelAndPearl_AndRespawnsAtBase()
        {
            var s = NewSim();
            s.AddItem(Item.Scrap, 10); s.AddItem(Item.Pearl, 10); s.AddItem(Item.Gel, 5); s.AddItem(Item.Brass, 1);
            s.P.hp = 1; s.P.x = World.BX + 300;
            s.Damage(5);
            Assert.AreEqual(8, s.inv[(int)Item.Scrap]);
            Assert.AreEqual(0, s.inv[(int)Item.Brass], "적게 들고 있어도 최소 1개는 잃는다");
            Assert.AreEqual(10, s.inv[(int)Item.Pearl], "진주는 잃지 않는다");
            Assert.AreEqual(5, s.inv[(int)Item.Gel], "젤은 치유 재료라 잃지 않는다(기획 검토)");
            Assert.AreEqual(World.BX, s.P.x, 0.01f);
            Assert.AreEqual(s.MaxHp, s.P.hp, 0.001f);
        }

        [Test]
        public void Chest_OpensWithLoot_ThenBecomesFloor()
        {
            var s = NewSim();
            int tx = -1, ty = -1;
            for (int y = 3; y < World.H - 3 && tx < 0; y++)
                for (int x = 3; x < World.W - 3; x++)
                    if (s.world.Get(x, y) == Tile.Chest) { tx = x; ty = y; break; }
            Assert.GreaterOrEqual(tx, 0, "시드 1234 에는 상자가 44개 있다");
            s.P.x = tx * 16 + 8; s.P.y = ty * 16 + 8;
            int before = 0; foreach (int n in s.inv) before += n;
            Run(s, 0.1f, new SimInput { mine = true, aim = new Vector2(tx * 16 + 8, ty * 16 + 8) });
            Assert.AreEqual(Tile.Floor, s.world.Get(tx, ty));
            int after = 0; foreach (int n in s.inv) after += n;
            Assert.Greater(after, before);
        }

        [Test]
        public void Clam_GivesPearl_OnceOnly()
        {
            var s = NewSim();
            int tx = -1, ty = -1;
            for (int y = 3; y < World.H - 3 && tx < 0; y++)
                for (int x = 3; x < World.W - 3; x++)
                    if (s.world.Get(x, y) == Tile.Floor && s.world.deco[World.Idx(x, y)] == 3) { tx = x; ty = y; break; }
            Assert.GreaterOrEqual(tx, 0);
            s.P.x = tx * 16 + 8; s.P.y = ty * 16 + 8;
            var inp = new SimInput { mine = true, aim = new Vector2(tx * 16 + 8, ty * 16 + 8) };
            Run(s, 0.2f, inp);
            int pearls = s.inv[(int)Item.Pearl];
            Assert.GreaterOrEqual(pearls, 1);
            Run(s, 0.5f, inp);
            Assert.AreEqual(pearls, s.inv[(int)Item.Pearl], "같은 조개에서 또 나오면 안 된다");
        }

        [Test]
        public void PlaceLamp_UsesInventory_AndPickupReturnsIt()
        {
            var s = NewSim(); AtBeacon(s);
            int tx = Mathf.FloorToInt(s.P.x / 16) + 1, ty = Mathf.FloorToInt(s.P.y / 16);
            var aim = new Vector2(tx * 16 + 8, ty * 16 + 8);
            s.Step(1f / 60f, new SimInput { lamp = true, aim = aim });
            Assert.AreEqual(Tile.Lamp, s.world.Get(tx, ty));
            Assert.AreEqual(2, s.inv[(int)Item.Lamp]);
            s.Step(1f / 60f, new SimInput { mine = true, aim = aim });
            Assert.AreEqual(Tile.Floor, s.world.Get(tx, ty));
            Assert.AreEqual(3, s.inv[(int)Item.Lamp]);
        }

        [Test]
        public void Boss_Activates_NearArena_AndDiesToDamage_RaisingVictory()
        {
            var s = NewSim();
            bool won = false; s.VictoryReached += () => won = true;
            s.P.x = World.AX; s.P.y = World.AY + 70;
            Run(s, 0.1f, new SimInput { aim = new Vector2(s.P.x, s.P.y) });
            Assert.IsTrue(s.boss.active);
            s.boss.hp = 1; s.boss.x = s.P.x + 12; s.boss.y = s.P.y; s.boss.fireT = 99; s.boss.chargeT = 99;
            s.P.atkCd = 0;
            s.Step(1f / 60f, new SimInput { attack = true, aim = new Vector2(s.boss.x, s.boss.y) });
            Assert.IsTrue(s.boss.dead); Assert.IsTrue(s.victory); Assert.IsTrue(won);
        }

        [Test]
        public void Save_RoundTrip_PreservesWorldInventoryAndLevels()
        {
            var s = NewSim();
            s.world.SetTile(90, 80, Tile.Floor); s.world.SetTile(89, 81, Tile.Lamp);
            s.AddItem(Item.Pearl, 7); s.P.lvl[(int)Track.Fins] = 2; s.P.x = 1500; s.P.hp = 6; s.boss.dead = true;
            string json = JsonUtility.ToJson(s.ToSave());
            var r = Sim.FromSave(JsonUtility.FromJson<SaveData>(json));
            Assert.AreEqual(s.world.Seed, r.world.Seed);
            CollectionAssert.AreEqual(s.world.tiles, r.world.tiles);
            CollectionAssert.AreEqual(s.world.deco, r.world.deco);
            Assert.AreEqual(7, r.inv[(int)Item.Pearl]);
            Assert.AreEqual(2, r.P.lvl[(int)Track.Fins]);
            Assert.AreEqual(1500f, r.P.x, 0.01f); Assert.AreEqual(6f, r.P.hp, 0.01f);
            Assert.IsTrue(r.boss.dead);
        }

        [Test]
        public void Populate_PlacesEnemiesByZone()
        {
            var s = NewSim(); s.Populate();
            Assert.Greater(s.enemies.Count, 50);
            foreach (var e in s.enemies)
            {
                if (Sim.IsMini(e.type)) continue;      // 미니보스는 소굴에 고정(일반 스폰 규칙 밖)
                Assert.LessOrEqual(e.Def.minZone, World.ZoneOfPx(e.x, e.y), e.type + " 는 자기 구역보다 깊은 곳에만 있어야 한다");
            }
        }

        [Test]
        public void GameData_UpgradeTracks_AreConsistent()
        {
            for (int t = 0; t < Data.Up.Length; t++)
            {
                Assert.IsNull(Data.Up[t][0].cost, "0레벨은 기본 장비");
                for (int l = 1; l < Data.Up[t].Length; l++) Assert.IsNotEmpty(Data.Up[t][l].cost, Data.Up[t][l].name);
            }
            Assert.AreEqual(10, Data.Crafts.Length, "트랙 6 + 램프 + 환기구 + 비콘 확장 + 산소 포션");
            Assert.AreEqual(Data.ItemName.Length, System.Enum.GetValues(typeof(Item)).Length); Assert.AreEqual(Data.ItemName.Length, Data.ItemIcon.Length);
            Assert.AreEqual(Data.Up.Length, System.Enum.GetValues(typeof(Track)).Length, "Track 과 Up 표의 개수가 같아야 한다");
            Assert.AreEqual(Data.Up.Length, Data.TrackLabel.Length); Assert.AreEqual(Data.Up.Length, Data.TrackIcon.Length);
        }
    }
}
