using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abyss.Tests
{
    /// <summary>실제 게임 오브젝트(카메라, 타일맵, UI)를 띄워서 몇 프레임 돌려보고 스크린샷을 남긴다.</summary>
    public class PlaySmokeTests
    {
        static string ShotDir { get { var d = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots"); Directory.CreateDirectory(d); return d; } }

#if UNITY_EDITOR
        /// <summary>에디터 Game 뷰를 가로 해상도로 맞춘다(기본은 세로 폰 해상도라 터치 UI 가 '가로로 돌려주세요'를 띄운다). 실패하면 조용히 넘어간다.</summary>
        static void SetGameViewSize(int w, int h)
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var single = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
                var inst = single.GetProperty("instance").GetValue(null, null);
                var group = sizesType.GetProperty("currentGroup").GetValue(inst, null);
                var groupType = group.GetType();
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                var size = System.Activator.CreateInstance(sizeType, System.Enum.ToObject(enumType, 1), w, h, "Abyss " + w + "x" + h);
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = UnityEditor.EditorWindow.GetWindow(gvType);
                gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(gv, new object[] { index, size });
            }
            catch (System.Exception) { }
        }
#endif

        [UnitySetUp]
        public IEnumerator SizeSetUp()
        {
#if UNITY_EDITOR
            SetGameViewSize(1920, 1080);
#endif
            for (int i = 0; i < 3; i++) yield return null;
        }

        [SetUp]
        public void SetUp()
        {
            GameBootstrap.NextSeed = 1234; GameBootstrap.NoSave = true;
            SaveSystem.PathOverride = Path.Combine(Application.temporaryCachePath, "abyss_test_save.json"); SaveSystem.Delete();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(go.gameObject);
            HudController.ForceTouch = false; GameBootstrap.NextSeed = 0;
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Boots_AndRunsManyFrames_WithoutErrors_DesktopMode()
        {
            HudController.ForceTouch = false;
            var b = new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            Assert.IsNotNull(b.Sim); Assert.IsNotNull(b.View.Cam);
            yield return Shot("01_desktop_start");
            b.Hud.BeginPlay();
            for (int i = 0; i < 20; i++) yield return null;
            yield return Shot("80_chapter_banner");                                  // 제1장 제목 카드
            yield return new WaitForSeconds(4.6f);
            yield return Shot("81_story_radio");                                     // 아라의 무전(시작 비트 둘째 줄 부근)
            b.Sim.P.x = World.BX + 40; b.Sim.P.y = World.BY + 20;
            for (int i = 0; i < 120; i++) yield return null;
            Assert.IsTrue(b.Sim.started);
            yield return Shot("02_desktop_play");
            for (int i = 0; i < 14; i++) { b.Sim.P.vx = 60f; b.Sim.P.vy = 0; yield return null; }      // 오른쪽으로 헤엄친다
            yield return Shot("70_swim_side");
            for (int i = 0; i < 14; i++) { b.Sim.P.vx = 0; b.Sim.P.vy = 60f; yield return null; }      // 아래로
            yield return Shot("71_swim_down");
            b.Sim.P.swing = 0.16f; b.Sim.P.swingAng = 0f; yield return null;
            yield return Shot("03_diver_atk_side");
            b.Sim.P.swing = 0.16f; b.Sim.P.swingAng = Mathf.PI / 2; yield return null;
            yield return Shot("04_diver_atk_down");
            b.Sim.P.swing = 0.16f; b.Sim.P.swingAng = -Mathf.PI / 2; yield return null;
            yield return Shot("05_diver_atk_up");
            var sm = b.Sim; float ex = sm.P.x - 60, ey = sm.P.y - 30;
            foreach (var t in new[] { EnemyType.Jelly, EnemyType.Angler, EnemyType.Urchin, EnemyType.Crab, EnemyType.Eel }) { sm.SpawnEnemy(t, ex, ey); ex += 28; }
            yield return null;
            yield return Shot("06_enemies");
            // 보스: 제단 근처에서 깨우고 두 단계 모습을 찍는다
            foreach (var e in new System.Collections.Generic.List<Enemy>(sm.enemies)) sm.enemies.Remove(e);
            sm.P.x = World.AX; sm.P.y = World.AY + 30; sm.P.hp = sm.MaxHp;
            sm.boss.active = true; sm.boss.x = World.AX; sm.boss.y = World.AY - 20; sm.boss.hp = Data.BossHp; sm.boss.t = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            yield return Shot("07_boss_phase1");
            sm.boss.hp = Data.BossHp * 0.4f; sm.boss.flash = 0.15f; yield return null;
            yield return Shot("08_boss_phase2_flash");
            sm.boss.flash = 0; sm.boss.t = 0.15f; yield return null;
            yield return Shot("09_boss_blink_phase2");
            // 타일: 수정·고철·황동 광맥 곁으로 가서 찍는다 (플레이어가 닿을 수 있는 바닥 칸을 찾는다)
            sm.boss.active = false; sm.boss.dead = true;
            // 채굴 균열: 암반 한 칸을 단계별로 캐다 멈추고 찍는다 (깨지기 직전까지)
            {
                int rx = -1, ry = -1;
                for (int ty = 45; ty < World.H - 45 && rx < 0; ty++)
                    for (int tx = 45; tx < World.W - 45 && rx < 0; tx++)
                        if (sm.world.Get(tx, ty) == Tile.Rock && !sm.world.IsSolid(tx, ty + 1) && !sm.world.IsSolid(tx, ty + 2) && sm.world.IsSolid(tx - 1, ty) && sm.world.IsSolid(tx + 1, ty)) { rx = tx; ry = ty; }
                Assert.GreaterOrEqual(rx, 0, "no rock tile found for crack test");
                sm.P.x = rx * World.TS + 8; sm.P.y = (ry + 2) * World.TS + 8;
                var inp = new SimInput { mine = true, aim = new Vector2(rx * World.TS + 8, ry * World.TS + 8) };
                float[] dts = { 0.3f, 0.25f, 0.2f };   // 누적 피해 0.9 / 1.65 / 2.25 (암반 체력 3, 곡괭이 3)
                for (int st = 0; st < 3; st++)
                {
                    sm.Step(dts[st], inp);
                    yield return null;
                    yield return Shot("3" + st + "_crack_stage" + (st + 1));
                }
            }
            foreach (var kv in new[] { new KeyValuePair<Tile, string>(Tile.Crystal, "20_tiles_crystal"), new KeyValuePair<Tile, string>(Tile.Scrap, "21_tiles_scrap"), new KeyValuePair<Tile, string>(Tile.Brass, "22_tiles_brass") })
            {
                bool found = false;
                // 월드 전체에서 '아래 두 칸이 뚫린' 광맥을 찾는다(수정은 깊은 곳에만 있어 범위를 좁히면 못 찾는다)
                for (int ty = 2; ty < World.H - 3 && !found; ty++)
                    for (int tx = 2; tx < World.W - 2 && !found; tx++)
                        if (sm.world.Get(tx, ty) == kv.Key && !sm.world.IsSolid(tx, ty + 1) && !sm.world.IsSolid(tx, ty + 2))
                        { sm.P.x = tx * World.TS + 8; sm.P.y = (ty + 1) * World.TS + 8; found = true; }   // 광맥 바로 아래 칸: 토스트·칩에 가리지 않는다
                Assert.IsTrue(found, "no " + kv.Key + " tile with open floor below");
                yield return new WaitForSeconds(2.6f);   // 구역 전환 토스트가 사라질 때까지(광맥을 가린다)
                if (found) yield return Shot(kv.Value);
            }
            // 대로: 폐허 환기구 곁에서 찍는다(램프·환기구가 길을 밝힌다)
            for (int ty = 2; ty < World.H - 2; ty++)
                for (int tx = 2; tx < World.W - 2; tx++)
                    if (sm.world.Get(tx, ty) == Tile.Vent) { sm.P.x = tx * World.TS + 8; sm.P.y = (ty + 3) * World.TS + 8; ty = World.H; break; }
            yield return new WaitForSeconds(3.4f);
            yield return Shot("40_road_vent");
            // 구역 장식: 얕은 폐허 기둥, 가라앉은 거리 표지판, 심연 뼈대·발광 버섯(+망령)
            foreach (var spot in new[] { new KeyValuePair<byte, string>(World.DecoPillar, "41_deco_pillar"), new KeyValuePair<byte, string>(World.DecoSign, "42_deco_sign"), new KeyValuePair<byte, string>(World.DecoFungus, "43_deco_fungus_wraith") })
            {
                for (int ty = 2; ty < World.H - 2; ty++)
                    for (int tx = 2; tx < World.W - 2; tx++)
                        if (sm.world.deco[World.Idx(tx, ty)] == spot.Key && !sm.world.IsSolid(tx, ty + 1)) { sm.P.x = tx * World.TS + 8; sm.P.y = (ty + 1) * World.TS + 8; ty = World.H; break; }
                if (spot.Key == World.DecoFungus) sm.SpawnEnemy(EnemyType.Wraith, sm.P.x + 34, sm.P.y - 14);
                yield return new WaitForSeconds(3.4f);
                yield return Shot(spot.Value);
            }
            // 가라앉은 거리의 격자 도로 + 정예 개체(금빛)
            for (int ty = 3; ty < World.H - 3; ty++)
                for (int tx = 3; tx < World.W - 3; tx++)
                    if (World.ZoneOfTile(tx, ty) == 1 && tx % 20 == 0 && ty % 20 == 1 && !sm.world.IsSolid(tx, ty)) { sm.P.x = tx * World.TS + 8; sm.P.y = ty * World.TS + 8; ty = World.H; break; }
            sm.SpawnEnemy(EnemyType.Crab, sm.P.x + 40, sm.P.y + 6, true);
            sm.P.hp = sm.MaxHp;
            yield return new WaitForSeconds(3.4f);
            yield return Shot("44_street_grid_elite");
            // 시작 지점: 비콘 곁 고철 광맥·상자 굴·해파리 + 목표 달성 보상
            sm.Populate(); sm.P.x = World.BX - 60; sm.P.y = World.BY + 10; sm.P.hp = sm.MaxHp; sm.P.invuln = 1f;
            yield return new WaitForSeconds(0.6f);
            sm.P.lvl[(int)Track.Pick] = 1;
            yield return new WaitForSeconds(0.25f);
            yield return Shot("60_start_area_goal_reward");
            // 미니보스 소굴: 집게왕(충격파 직후)과 장어왕
            sm.Populate();      // 앞에서 지운 적을 다시 배치(미니보스 포함)
            for (int mi = 0; mi < 2; mi++)
            {
                var lair = World.MiniLairs[mi];
                sm.P.x = lair[0] * World.TS + 8 + 70; sm.P.y = lair[1] * World.TS + 8 + 10; sm.P.hp = sm.MaxHp; sm.P.invuln = 0.5f;
                yield return new WaitForSeconds(mi == 0 ? 4.2f : 3.6f);
                yield return Shot(mi == 0 ? "50_crab_king" : "51_eel_king");
            }
            sm.P.invuln = 99f;
            b.Hud.OpenMenu(); b.Hud.OpenLog();                                       // 이야기 기록 화면(마지막에 찍는다: 위에 덮인다)
            yield return null; yield return null;
            yield return Shot("82_story_log");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Boots_TouchMode_ShowsControls_AndSheetOpens()
        {
            HudController.ForceTouch = true;
            var b = new GameObject("Bootstrap").AddComponent<GameBootstrap>();
            yield return null;
            Assert.IsTrue(b.Hud.TouchMode);
            yield return Shot("10_touch_start");
            b.Hud.BeginPlay();
            var s = b.Sim; s.P.x = World.BX + 210; s.P.y = World.BY + 20; s.P.hp = 3; s.P.o2 = 7;
            s.AddItem(Item.Scrap, 20); s.AddItem(Item.Gel, 10); s.AddItem(Item.Brass, 5); s.AddItem(Item.Pearl, 3);
            s.SpawnEnemy(EnemyType.Jelly, s.P.x + 40, s.P.y - 10); s.SpawnEnemy(EnemyType.Angler, s.P.x + 70, s.P.y + 30);
            for (int i = 0; i < 90; i++) yield return null;
            yield return Shot("11_touch_play");
            s.P.x = World.BX; s.P.y = World.BY + 28; s.P.hp = s.MaxHp;
            for (int i = 0; i < 30; i++) yield return null;
            b.Hud.OpenSheet();
            for (int i = 0; i < 5; i++) yield return null;
            yield return Shot("12_touch_sheet");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
