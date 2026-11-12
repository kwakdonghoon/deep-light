using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss
{
    public enum Sfx { Mine, Break, Swing, Hit, Hurt, Pick, Craft, Chest, Boss }

    public sealed class Player
    {
        public float x, y, vx, vy, hp, o2;
        public int face = 1;
        public float invuln, atkCd, swing, swingAng, bob, suffocate, bubbleT;
        public readonly int[] lvl = new int[7];
    }

    public sealed class Enemy
    {
        public EnemyType type;
        public float x, y, kx, ky, hp, t, flash, lunge, lt, dirx, diry, wt, lx, ly, wind, back, faceX;
        public bool elite;     // 정예 개체: 체력 2.2배, 피해 +1, 보상 2배 + 진주
        public float maxHp, homeX, homeY;   // 미니보스: 소굴 위치와 최대 체력(멀어지면 소굴로 돌아가 회복)
        public EnemyDef Def { get { return Data.Enemies[(int)type]; } }
    }

    public sealed class Boss
    {
        public bool active, dead;
        public float x = World.AX, y = World.AY, hp = Data.BossHp, t, fireT, chargeT, charge, cdx, cdy, flash, windup;
    }

    public sealed class Bullet { public float x, y, vx, vy, life; }

    public struct Particle
    {
        public float x, y, vx, vy, life, max; public Color32 color; public int size;
    }

    /// <summary>한 프레임의 입력. 키보드/마우스/터치 어느 쪽이든 이 구조로 변환해서 Sim 에 넣는다.</summary>
    public struct SimInput
    {
        public Vector2 move;       // -1..1 (키보드는 정규화, 터치는 아날로그)
        public Vector2 aim;        // 조준 지점(월드 px)
        public bool mine, attack, lamp, vent, heal;
        public bool touch;         // 터치 보정(작살 사거리 확대, 좌우 방향 고정)
        public Vector2 faceDir;    // 터치에서 바라보는 방향
    }

    [Serializable]
    public sealed class SaveData
    {
        public int seed;
        public string tiles, deco, explored;
        public float px, py, hp, o2;
        public int[] lvl, inv;
        public bool bossDead;
        public int miniDead;      // 처치한 미니보스 비트마스크(0=집게왕, 1=장어왕)
        public int goalMask;      // 보상을 받은 목표 비트
        public int storyMask;     // 이미 나온 이야기 비트
    }

    /// <summary>게임 규칙 전체(game.js 의 update 계열 포팅). Unity 오브젝트에 의존하지 않는다.</summary>
    public sealed class Sim
    {
        public readonly World world = new World();
        public readonly Player P = new Player();
        public readonly int[] inv = new int[8];
        public readonly bool[] miniDead = new bool[2];      // 미니보스(소굴) 처치 여부
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly Boss boss = new Boss();
        public readonly List<Bullet> bullets = new List<Bullet>();
        public readonly List<Particle> particles = new List<Particle>();
        public readonly List<int> exploredDirty = new List<int>();

        public float time, camShake;
        public bool started, victory, mining;
        public int cursorTx, cursorTy; public bool cursorReach;

        public event Action<int, int> TileChanged;
        public event Action<string, float, bool> Toast;       // 메시지, 초, quiet
        public event Action<Sfx> SfxEvent;
        public event Action<int> Buzz;
        public event Action VictoryReached;
        public event Action SaveRequested;

        readonly System.Random rand;
        float hintCd, spawnT, healCd;
        bool bossWarned;
        int lastPtx = -1, lastPty = -1;

        public Sim(int seed)
        {
            world.Generate(seed);
            world.DesignPass(seed);        // 기획 검토 반영(광맥 절반, 대로, 폐허 방): 웹과 같은 기본 생성(Generate)은 그대로 두고 그 위에 덧입힌다
            rand = new System.Random(seed ^ 0x5F3759DF);
            ResetPlayer();
            inv[(int)Item.Lamp] = 3;
        }

        // ---------- 파생 값 ----------
        public int MaxHp { get { return Data.Up[(int)Track.Armor][P.lvl[(int)Track.Armor]].hp; } }
        public int MaxO2 { get { return Data.Up[(int)Track.Tank][P.lvl[(int)Track.Tank]].max; } }
        public int LightR { get { return Data.Up[(int)Track.Light][P.lvl[(int)Track.Light]].r; } }
        public Level PickLevel { get { return Data.Up[(int)Track.Pick][P.lvl[(int)Track.Pick]]; } }
        public int HarpoonDmg { get { return Data.Up[(int)Track.Harpoon][P.lvl[(int)Track.Harpoon]].dmg; } }
        public float Speed { get { return Data.Up[(int)Track.Fins][P.lvl[(int)Track.Fins]].speed; } }
        public int maxZone;        // 지금까지 도달한 가장 깊은 구역(세션 목표용)

        // ---------- 이야기: 무전(비트), 장(chapter), 한동안 막혔을 때의 힌트 ----------
        struct StoryItem { public float t; public string text; }
        readonly List<StoryItem> storyQ = new List<StoryItem>();
        float storyNextT, lastProgressT, hintT; int hintedGoal = -1, lastChapter;
        public int storyMask;                                       // 이미 나온 비트(저장됨)
        public readonly List<string> storyLog = new List<string>(); // 지금까지 나온 무전 줄(메뉴의 '이야기 기록')
        public event Action<string, float> StoryLine;               // (텍스트, 표시 시간)
        public event Action<string> ChapterBanner;                  // 새 장이 시작될 때(장 제목)
        public const float StoryGap = 4.8f, StoryShow = 4.4f;

        /// <summary>비트를 한 번만 재생한다. 줄들은 StoryGap 간격으로 줄 서서 나온다.</summary>
        public void Beat(string id)
        {
            int bit = Array.IndexOf(Data.StoryIds, id);
            if (bit < 0 || (storyMask & (1 << bit)) != 0) return;
            storyMask |= 1 << bit;
            float t = Mathf.Max(time, storyNextT);
            foreach (var line in Data.StoryLines[bit]) { storyQ.Add(new StoryItem { t = t, text = line }); t += StoryGap; }
            storyNextT = t; lastProgressT = time;
        }

        void UpdateStory(float dt)
        {
            for (int i = 0; i < storyQ.Count; )
            {
                if (time >= storyQ[i].t)
                {
                    var it = storyQ[i]; storyQ.RemoveAt(i);
                    storyLog.Add(it.text); if (storyLog.Count > 60) storyLog.RemoveAt(0);
                    if (StoryLine != null) StoryLine(it.text, StoryShow);
                }
                else i++;
            }
            // 장: 가장 앞의 미달성 목표가 속한 장. 올라갈 때만 알린다.
            int g = CurrentGoalIndex(); int ch = g < 0 ? Data.ChapterTitle.Length : Data.ChapterOfGoal[g];
            if (ch > lastChapter)
            {
                lastChapter = ch;
                var title = Data.ChapterTitle[ch - 1]; storyLog.Add("— " + title + " —");
                if (ChapterBanner != null) ChapterBanner(title);
            }
            // 한동안 진행이 없으면 지금 목표에 대한 힌트를 한 번 건넨다(목표당 한 번)
            hintT += dt;
            if (hintT >= 1f)
            {
                hintT = 0;
                if (g >= 0 && g != hintedGoal && storyQ.Count == 0 && time - lastProgressT > 150f)
                {
                    hintedGoal = g; lastProgressT = time;
                    string h = Data.GoalHints[g]; storyLog.Add(h); if (StoryLine != null) StoryLine(h, StoryShow);
                }
            }
        }

        /// <summary>보상을 받지 않은 가장 앞의 목표 번호(없으면 -1).</summary>
        int CurrentGoalIndex()
        {
            for (int i = 0; i < Data.ChapterOfGoal.Length; i++) if ((goalMask & (1 << i)) == 0) return i;
            return -1;
        }

        public int Chapter { get { int g = CurrentGoalIndex(); return g < 0 ? Data.ChapterTitle.Length : Data.ChapterOfGoal[g]; } }

        // ---------- 세션 목표 + 달성 보상(초반 재미: 몇 분마다 즉시 보상이 터지게) ----------
        struct Goal { public string text; public bool cond; public Item item; public int n; }
        public int goalMask;                       // 보상을 받은 목표 비트(저장됨)
        int oreBroken;                             // 이번 판에 캔 광맥 수(첫 3개 보너스용)
        int oreCombo, affordHints; float lastOreT = -99f, lastAffordHintT = -99f, affordT;   // 연쇄 채굴 / '제작할 수 있다' 안내
        /// <summary>지도에 초반 안내 점(비콘 곁 광맥)을 보일지: 첫 목표(고철 8개)를 끝내기 전까지.</summary>
        public bool ShowEarlyGuide { get { return (goalMask & 1) == 0; } }
        public readonly int[] gotTotal = new int[8];   // 지금까지 얻은 양(채굴·드롭·상자). 저장하지 않는다.

        Goal[] BuildGoals()
        {
            bool starterChestOpened = world.Get(World.StarterChest[0], World.StarterChest[1]) != Tile.Chest;
            return new[]
            {
                new Goal { text = "광맥에서 고철 8개 모으기", cond = gotTotal[(int)Item.Scrap] >= 8, item = Item.Gel, n = 1 },
                new Goal { text = "해파리를 잡아 젤 얻기", cond = gotTotal[(int)Item.Gel] >= 2, item = Item.Scrap, n = 4 },
                new Goal { text = "황동 곡괭이 제작", cond = P.lvl[(int)Track.Pick] >= 1, item = Item.Gel, n = 3 },
                new Goal { text = "비콘 근처 보물 상자 열기", cond = starterChestOpened, item = Item.Potion, n = 1 },
                new Goal { text = "산소통 Lv2 제작", cond = P.lvl[(int)Track.Tank] >= 1, item = Item.Scrap, n = 6 },
                new Goal { text = "랜턴 Lv2 제작", cond = P.lvl[(int)Track.Light] >= 1, item = Item.Gel, n = 2 },
                new Goal { text = "가라앉은 거리에 도착", cond = maxZone >= 1, item = Item.Pearl, n = 2 },
                new Goal { text = "녹슨 집게왕 처치(붉은 점)", cond = miniDead[0], item = Item.Potion, n = 1 },
                new Goal { text = "번개 장어왕 처치", cond = miniDead[1], item = Item.Potion, n = 1 },
                new Goal { text = "작살 Lv3 제작", cond = P.lvl[(int)Track.Harpoon] >= 2, item = Item.Crystal, n = 2 },
                new Goal { text = "방호복 Lv2 제작", cond = P.lvl[(int)Track.Armor] >= 1, item = Item.Gel, n = 3 },
                new Goal { text = "대로를 따라 심연으로", cond = maxZone >= 2, item = Item.Potion, n = 2 },
                new Goal { text = "수정 곡괭이 제작", cond = P.lvl[(int)Track.Pick] >= 2, item = Item.Pearl, n = 2 },
                new Goal { text = "심연의 눈을 쓰러뜨려라", cond = boss.dead, item = Item.Gel, n = 0 },
            };
        }

        /// <summary>재료가 모였는데 비콘에서 멀리 있으면 '돌아가 제작하자'고 알려 준다(초반 3번만, 45초 간격). 모으기→제작 루프를 닫아 준다.</summary>
        void HintAffordable(float dt)
        {
            affordT += dt; if (affordT < 1f) return; affordT = 0;
            if (affordHints >= 3 || NearBeacon || time - lastAffordHintT < 45f) return;
            foreach (var t in new[] { Track.Pick, Track.Harpoon, Track.Light, Track.Tank, Track.Armor })
            {
                var next = NextLevel(t);
                if (next == null || next.cost == null || !CanAfford(next.cost)) continue;
                affordHints++; lastAffordHintT = time;
                SayToast("재료가 모였다! 비콘으로 돌아가 " + next.name + " 제작하자", 3.6f);
                PlaySfx(Sfx.Craft);
                return;
            }
        }

        /// <summary>달성한 목표의 보상을 한 번만 준다.</summary>
        void CheckGoals()
        {
            var goals = BuildGoals();
            for (int i = 0; i < goals.Length; i++)
            {
                if ((goalMask & (1 << i)) != 0 || !goals[i].cond) continue;
                goalMask |= 1 << i;
                Beat("goal" + i);                       // 목표 달성에 맞는 무전 한 장면
                var g = goals[i];
                if (g.n > 0) AddItem(g.item, g.n);
                SayToast("목표 달성! " + g.text + (g.n > 0 ? "  →  " + Data.ItemName[(int)g.item] + " +" + g.n : ""), 3.4f);
                PlaySfx(Sfx.Craft); if (Buzz != null) Buzz(25);
                for (int k = 0; k < 14; k++) SpawnParticle(P.x, P.y - 4, Rr(-60, 60), Rr(-70, -10), 0.8f, new Color32(255, 226, 150, 255), 2);
            }
        }

        /// <summary>HUD 의 '지금 할 일' — 순서대로 가장 앞의 미완료 3개. 모두 끝나면 마무리 문구.</summary>
        public string[] ActiveGoals()
        {
            var goals = BuildGoals(); var list = new List<string>();
            for (int i = 0; i < goals.Length && list.Count < 3; i++)
                if ((goalMask & (1 << i)) == 0 && !goals[i].cond) list.Add("· " + goals[i].text);
            if (list.Count == 0) list.Add("· 더 깊은 곳의 불빛을 찾아라");
            return list.ToArray();
        }

        // 비콘 반경: 확장 단계 + 복구한 중계기(미니보스)마다 +12
        public float BeaconR { get { return Data.Up[(int)Track.Beacon][P.lvl[(int)Track.Beacon]].r + 12f * ((miniDead[0] ? 1 : 0) + (miniDead[1] ? 1 : 0)); } }       // 산소 안전지대 반경(비콘 확장으로 커진다)
        public float CraftR { get { return BeaconR * (Data.CraftRadius / Data.BeaconRadius); } }
        public bool NearBeacon { get { return Hyp(P.x - World.BX, P.y - World.BY) < CraftR; } }
        public int Zone { get { return World.ZoneOfPx(P.x, P.y); } }

        static float Hyp(float a, float b) { return Mathf.Sqrt(a * a + b * b); }
        float Rr(float a, float b) { return a + (float)rand.NextDouble() * (b - a); }
        int Rint(int a, int b) { return a + (int)Math.Floor(rand.NextDouble() * (b - a + 1)); }

        void ResetPlayer()
        {
            P.x = World.BX; P.y = World.BY + 28; P.vx = P.vy = 0;
            P.hp = MaxHp; P.o2 = MaxO2;
        }

        public void SayToast(string msg, float sec = 2.2f, bool quiet = false) { if (Toast != null) Toast(msg, sec, quiet); }
        void PlaySfx(Sfx s) { if (SfxEvent != null) SfxEvent(s); }

        public void AddItem(Item item, int n) { inv[(int)item] += n; if (n > 0) gotTotal[(int)item] += n; }
        public bool CanAfford(Cost[] cost) { foreach (var c in cost) if (inv[(int)c.item] < c.amount) return false; return true; }

        void SpawnParticle(float x, float y, float vx, float vy, float life, Color32 color, int size = 1)
        {
            if (particles.Count > 400) return;
            particles.Add(new Particle { x = x, y = y, vx = vx, vy = vy, life = life, max = life, color = color, size = size });
        }

        void DropFrom(DropRange[] drop)
        {
            if (drop == null) return;
            foreach (var d in drop)
            {
                int n = Rint(d.min, d.max);
                if (n <= 0) continue;
                AddItem(d.item, n); SayToast("+" + n + " " + Data.ItemName[(int)d.item], 0.9f, true); PlaySfx(Sfx.Pick);
                for (int k = 0; k < 4; k++) SpawnParticle(P.x + Rr(-6, 6), P.y - 4, Rr(-14, 14), Rr(-50, -20), 0.5f, Data.ItemColor[(int)d.item], 1);   // 획득 반짝임: 아이템 색 점이 위로 오른다
            }
        }

        // ---------- 초기 적 배치 ----------
        public void Populate()
        {
            enemies.Clear();
            for (int y = 3; y < World.H - 3; y++)
                for (int x = 3; x < World.W - 3; x++)
                {
                    if (world.tiles[World.Idx(x, y)] != (byte)Tile.Floor) continue;
                    if (Hyp(x - World.CX, y - World.CY) < 14) continue;
                    if (Hyp(x - World.ArenaX, y - World.ArenaY) <= World.ArenaR + 1) continue;
                    if (Hyp(x * World.TS + 8 - P.x, y * World.TS + 8 - P.y) < 10 * World.TS) continue;   // 불러온 위치 바로 옆에 적이 생기지 않게
                    int z = World.ZoneOfTile(x, y);
                    if (rand.NextDouble() < 0.009 + z * 0.0025) SpawnEnemy(PickType(z), x * World.TS + 8, y * World.TS + 8, RollElite(z));
                }
            SpawnEarlyJellies();
            SpawnMinis();
        }

        /// <summary>초반 재미: 비콘에서 11~14칸 거리에 해파리 3마리를 확정 배치한다(젤을 빨리 얻어 첫 업그레이드까지 막히지 않게).</summary>
        void SpawnEarlyJellies()
        {
            for (int k = 0; k < 3; k++)
            {
                float baseA = k * 2.1f + 0.7f; bool placed = false;
                for (int dd = 0; dd < 8 && !placed; dd++)
                    for (int da = -3; da <= 3 && !placed; da++)
                    {
                        float a = baseA + da * 0.12f, d = 11 + dd * 0.5f;
                        int tx = World.CX + Mathf.RoundToInt(Mathf.Cos(a) * d), ty = World.CY + Mathf.RoundToInt(Mathf.Sin(a) * d);
                        if (world.Get(tx, ty) != Tile.Floor) continue;
                        SpawnEnemy(EnemyType.Jelly, tx * World.TS + 8, ty * World.TS + 8); placed = true;
                    }
            }
        }

        EnemyType PickType(int zone)
        {
            var list = new List<int>(); int total = 0;
            for (int i = 0; i < Data.Enemies.Length; i++) if (Data.Enemies[i].minZone <= zone) { list.Add(i); total += Data.Enemies[i].weight; }
            double r = rand.NextDouble() * total;
            foreach (int i in list) { r -= Data.Enemies[i].weight; if (r <= 0) return (EnemyType)i; }
            return (EnemyType)list[0];
        }

        public Enemy SpawnEnemy(EnemyType type, float x, float y, bool elite = false)
        {
            var e = new Enemy { type = type, x = x, y = y, hp = Data.Enemies[(int)type].hp * (elite ? 2.2f : 1f), t = Rr(0, 10), lt = Rr(1.5f, 3f), elite = elite, homeX = x, homeY = y };
            e.maxHp = e.hp;
            enemies.Add(e);
            return e;
        }

        public static bool IsMini(EnemyType t) { return t >= EnemyType.CrabKing; }
        static int MiniIndex(EnemyType t) { return (int)t - (int)EnemyType.CrabKing; }

        /// <summary>플레이어가 가까이 있는(220px) 살아 있는 미니보스. HUD 체력 바에 쓴다.</summary>
        public Enemy ActiveMini
        {
            get
            {
                foreach (var e in enemies) if (IsMini(e.type) && Hyp(e.x - P.x, e.y - P.y) < 220f) return e;
                return null;
            }
        }

        readonly bool[] miniWarned = new bool[2];

        /// <summary>소굴에 가까워지면(약 24칸 이내) 한 번 권장 장비를 알려 준다.</summary>
        void WarnMinis()
        {
            foreach (var e in enemies)
            {
                if (!IsMini(e.type)) continue;
                int i = MiniIndex(e.type);
                if (miniWarned[i] || Hyp(e.x - P.x, e.y - P.y) > 24 * World.TS) continue;
                miniWarned[i] = true;
                SayToast(Data.Enemies[(int)e.type].name + "의 소굴이다 — " + (i == 0 ? "작살 Lv2 · 방호복 Lv2 권장" : "작살 Lv3 · 방호복 Lv2 권장"), 4f);
            }
        }

        void SpawnMinis()
        {
            for (int i = 0; i < World.MiniLairs.Length; i++)
            {
                if (miniDead[i]) continue;
                var l = World.MiniLairs[i];
                SpawnEnemy((EnemyType)((int)EnemyType.CrabKing + i), l[0] * World.TS + 8, l[1] * World.TS + 8);
            }
        }

        void Shockwave(Enemy e, int n, float speed)
        {
            float off = Rr(0, 6.28f);
            for (int i = 0; i < n; i++) { float a = off + i * 6.28318f / n; bullets.Add(new Bullet { x = e.x, y = e.y, vx = Mathf.Cos(a) * speed, vy = Mathf.Sin(a) * speed, life = 3.5f }); }
            camShake = 0.2f; PlaySfx(Sfx.Boss);
        }

        void FanShot(Enemy e, int n, float spread, float speed)
        {
            float baseA = Mathf.Atan2(P.y - e.y, P.x - e.x);
            for (int i = 0; i < n; i++) { float a = baseA + (i - (n - 1) / 2f) * spread; bullets.Add(new Bullet { x = e.x, y = e.y, vx = Mathf.Cos(a) * speed, vy = Mathf.Sin(a) * speed, life = 4f }); }
        }

        bool RollElite(int zone)
        {
            float p = zone >= 2 ? Data.EliteChanceZone2 : zone == 1 ? Data.EliteChanceZone1 : 0f;
            return p > 0 && rand.NextDouble() < p;
        }

        // ---------- 프레임 ----------
        public void Step(float dt, SimInput inp)
        {
            time += dt;
            if (hintCd > 0) hintCd -= dt;
            if (healCd > 0) healCd -= dt;
            if (camShake > 0) camShake -= dt;
            if (!started) return;
            UpdatePlayer(dt, inp);
            UpdateMining(dt, inp);
            UpdateCombat(dt, inp);
            UpdateEnemies(dt);
            UpdateBoss(dt);
            UpdateBullets(dt);
            UpdateParticles(dt);
            UpdateSpawner(dt);
            WarnMinis();
            Beat("start");
            CheckGoals();
            UpdateStory(dt);
            HintAffordable(dt);
            Explore();
        }

        // ---------- 플레이어 ----------
        public void Damage(float n, bool ignoreInv = false)
        {
            if (!started) return;
            if (P.invuln > 0 && !ignoreInv) return;
            P.hp -= n; if (!ignoreInv) P.invuln = 0.9f;
            camShake = 0.25f; PlaySfx(Sfx.Hurt); if (Buzz != null) Buzz(40);
            for (int i = 0; i < 6; i++) SpawnParticle(P.x, P.y, Rr(-40, 40), Rr(-40, 40), 0.4f, new Color32(255, 107, 107, 255));
            if (P.hp <= 0) Die();
        }

        void Die()
        {
            // 젤은 치유 재료라 잃지 않는다(기획 검토: 희소한 젤만 크게 아팠다). 나머지는 20%, 적게 들고 있어도 최소 1개는 잃는다.
            foreach (var it in new[] { Item.Scrap, Item.Brass, Item.Crystal })
            {
                int have = inv[(int)it]; if (have <= 0) continue;
                inv[(int)it] = have - Mathf.Max(1, Mathf.CeilToInt(have * Data.DeathLossRate));
            }
            ResetPlayer(); P.invuln = 1.5f;
            boss.active = false; ResetBossHp(); bullets.Clear();
            Beat("firstDeath");
            SayToast("기절해서 기지로 돌아왔다… 자원 일부를 잃었다", 3.5f);
        }

        /// <summary>보스를 놓쳤을 때: 1단계면 풀 체력, 2단계까지 갔다면 2단계 시작 체력에서 다시(체크포인트).</summary>
        void ResetBossHp()
        {
            boss.hp = boss.hp < Data.BossHp * 0.5f ? Data.BossHp * Data.BossCheckpointFrac : Data.BossHp;
            boss.windup = 0; boss.charge = 0;
        }

        bool NearVent()
        {
            int ptx = Mathf.FloorToInt(P.x / World.TS), pty = Mathf.FloorToInt(P.y / World.TS), R = Data.VentRadiusTiles;
            for (int y = pty - R; y <= pty + R; y++)
                for (int x = ptx - R; x <= ptx + R; x++)
                    if (world.Get(x, y) == Tile.Vent && Hyp(x * World.TS + 8 - P.x, y * World.TS + 8 - P.y) <= R * World.TS) return true;
            return false;
        }

        void UpdatePlayer(float dt, SimInput inp)
        {
            float spd = Speed, k = Mathf.Min(1f, dt * 7f);
            P.vx += (inp.move.x * spd - P.vx) * k;
            P.vy += (inp.move.y * spd - P.vy) * k;
            world.MoveBox(ref P.x, ref P.y, P.vx * dt, P.vy * dt, 5f);
            P.bob += dt * 4;
            if (!inp.touch || Mathf.Abs(inp.faceDir.x) > 0.2f) P.face = inp.aim.x >= P.x ? 1 : -1;
            if (P.invuln > 0) P.invuln -= dt;

            float dB = Hyp(P.x - World.BX, P.y - World.BY);
            if (Zone > maxZone) maxZone = Zone;
            bool inBeacon = dB < BeaconR;
            if (inBeacon || NearVent())
            {
                if (inBeacon && P.hp < MaxHp) P.hp = Mathf.Min(MaxHp, P.hp + 0.5f * dt);   // 비콘 안에서는 체력도 천천히 회복
                float rf = Data.Up[(int)Track.Tank][P.lvl[(int)Track.Tank]].refill;   // 산소통 Lv3+ 는 충전이 빠르다
                P.o2 = Mathf.Min(MaxO2, P.o2 + Data.O2Refill * (rf > 0 ? rf : 1f) * dt);
                P.suffocate = 0;
            }
            else
            {
                float dm = Data.Up[(int)Track.Fins][P.lvl[(int)Track.Fins]].drain;   // 추진 핀 Lv3+ 는 산소를 덜 쓴다
                P.o2 -= (Data.O2DrainBase + Zone * Data.O2DrainPerZone) * (dm > 0 ? dm : 1f) * dt;
                if (P.o2 < MaxO2 * 0.22f) Beat("lowO2");
                if (P.o2 <= 0)
                {
                    P.o2 = 0; P.suffocate += dt;
                    if (P.suffocate >= 1) { P.suffocate = 0; Damage(1, true); SayToast("산소 부족! 비콘이나 환기구로 돌아가자", 1.5f, true); }
                }
            }
            P.bubbleT -= dt;
            if (P.bubbleT <= 0) { P.bubbleT = Rr(0.3f, 0.7f); SpawnParticle(P.x + P.face * 3, P.y - 6, Rr(-4, 4), -14, 1.4f, new Color32(159, 232, 255, 255)); }

            if (inp.heal)
            {
                if (inv[(int)Item.Gel] >= 2 && P.hp < MaxHp && healCd <= 0) { inv[(int)Item.Gel] -= 2; P.hp = Mathf.Min(MaxHp, P.hp + 3); healCd = Data.HealCooldown; SayToast("젤을 먹고 체력 회복"); }
                else if (inv[(int)Item.Potion] > 0 && P.o2 < MaxO2 * 0.85f && healCd <= 0)   // 체력이 가득하거나 젤이 없을 때 같은 버튼으로 산소 포션
                {
                    inv[(int)Item.Potion]--; P.o2 = Mathf.Min(MaxO2, P.o2 + MaxO2 * Data.PotionO2Frac); P.suffocate = 0; healCd = Data.HealCooldown; SayToast("산소 포션을 마셨다"); Beat("potion");
                }
                else SayToast(inv[(int)Item.Gel] < 2 && inv[(int)Item.Potion] <= 0 ? "젤이 2개 필요하다" : "체력이 가득 찼다", 1.2f);
            }
        }

        // ---------- 채굴 / 설치 ----------
        void Hint(string msg) { if (hintCd <= 0) { SayToast(msg, 1.4f); hintCd = 1.2f; } }

        void BreakTile(int tx, int ty)
        {
            int i = World.Idx(tx, ty); var def = Data.Tiles[world.tiles[i]];
            Color32 col = def.ore != null ? def.ore[0] : def.baseColors[1];
            for (int k = 0; k < 8; k++) SpawnParticle(tx * World.TS + 8, ty * World.TS + 8, Rr(-50, 50), Rr(-50, 50), 0.5f, col, 2);
            if (def.ore != null)
            {
                // 광석 조각이 플레이어에게 날아든다(채굴 손맛). 0.45초 뒤 플레이어 위치에 도착하도록 속도를 잡는다.
                for (int k = 0; k < 6; k++)
                {
                    float life = 0.45f, ox = tx * World.TS + 8, oy = ty * World.TS + 8;
                    SpawnParticle(ox, oy, (P.x - ox) / life + Rr(-14, 14), (P.y - oy) / life + Rr(-14, 14), life, def.ore[k % 2], 2);
                }
            }
            DropFrom(def.drop);
            if (def.ore != null && def.drop != null && oreBroken++ < 3)      // 첫 광맥 세 개는 보너스(운이 좋은 느낌)
            {
                foreach (var d in def.drop) AddItem(d.item, 1);
                SayToast("행운! 첫 광맥 보너스 +1", 1.6f, true);
            }
            if (def.ore != null && def.drop != null)                        // 연쇄 채굴: 4초 안에 광맥을 이어 깨면 3개째마다 보너스
            {
                oreCombo = (time - lastOreT <= 4f) ? oreCombo + 1 : 1; lastOreT = time;
                if (oreCombo >= 3 && oreCombo % 3 == 0)
                {
                    foreach (var d in def.drop) AddItem(d.item, 1);
                    SayToast("연쇄 x" + oreCombo + "! 보너스 +1", 1.6f, true); PlaySfx(Sfx.Chest);
                    for (int k = 0; k < 8; k++) SpawnParticle(P.x, P.y - 6, Rr(-40, 40), Rr(-60, -10), 0.6f, new Color32(255, 226, 150, 255), 2);
                }
            }
            PlaySfx(Sfx.Break);
            world.tiles[i] = (byte)Tile.Floor; world.dmg[i] = 0; world.deco[i] = 0; crackShown[i] = 0;
            if (TileChanged != null) TileChanged(tx, ty);
        }

        void OpenChest(int tx, int ty)
        {
            var loot = Data.ChestLoot[World.ZoneOfTile(tx, ty)]; var got = new List<string>();
            foreach (var rc in World.RuinCenters) if (rc[0] == tx && rc[1] == ty) Beat("ruin");
            world.tiles[World.Idx(tx, ty)] = (byte)Tile.Floor;
            if (TileChanged != null) TileChanged(tx, ty);
            foreach (var d in loot)
            {
                int n = Rint(d.min, d.max);
                if (n > 0) { AddItem(d.item, n); got.Add(Data.ItemName[(int)d.item] + " " + n); }
            }
            PlaySfx(Sfx.Chest); if (Buzz != null) Buzz(20);
            SayToast("보물 상자! " + string.Join(", ", got) + "  ·  " + Data.ChestNotes[Rint(0, Data.ChestNotes.Length - 1)], 4.2f);   // 환경 서사: 유실물 쪽지
            var cols = new[] { new Color32(255, 216, 107, 255), new Color32(255, 240, 168, 255), new Color32(255, 255, 255, 255) };
            for (int k = 0; k < 12; k++) SpawnParticle(tx * World.TS + 8, ty * World.TS + 8, Rr(-50, 50), Rr(-60, 10), 0.7f, cols[k % 3], 2);
        }

        void UpdateMining(float dt, SimInput inp)
        {
            int tx = Mathf.FloorToInt(inp.aim.x / World.TS), ty = Mathf.FloorToInt(inp.aim.y / World.TS);
            cursorTx = tx; cursorTy = ty;
            cursorReach = Hyp(tx * World.TS + 8 - P.x, ty * World.TS + 8 - P.y) <= Data.Reach;
            mining = false;
            if (inp.mine && cursorReach && World.InBounds(tx, ty))
            {
                int i = World.Idx(tx, ty); var t = (Tile)world.tiles[i]; var def = Data.Tiles[(int)t];
                if (t == Tile.Chest) OpenChest(tx, ty);
                else if (t == Tile.Lamp || t == Tile.Vent)
                {
                    world.tiles[i] = (byte)Tile.Floor;
                    if (t == Tile.Lamp) AddItem(Item.Lamp, 1);
                    else SayToast("환기구가 부서졌다(회수할 수 없다)", 1.6f);   // 기획 검토: 줍고 놓기로 산소 사거리가 무한해졌다
                    if (TileChanged != null) TileChanged(tx, ty);
                }
                else if (!def.solid && world.deco[i] == 3)
                {
                    world.deco[i] = 0;
                    int n = World.ZoneOfTile(tx, ty) == 2 ? 2 : 1;
                    AddItem(Item.Pearl, n); SayToast("+" + n + " 진주", 0.9f, true); PlaySfx(Sfx.Pick);
                    for (int k = 0; k < 6; k++) SpawnParticle(tx * World.TS + 8, ty * World.TS + 12, Rr(-30, 30), Rr(-40, 0), 0.5f, new Color32(255, 255, 255, 255));
                    if (TileChanged != null) TileChanged(tx, ty);
                }
                else if (def.solid)
                {
                    if (def.unbreakable) Hint("기반암은 깰 수 없다");
                    else if (def.tier > PickLevel.tier) Hint(def.name + ": 더 좋은 곡괭이가 필요하다");
                    else
                    {
                        world.dmg[i] += PickLevel.power * dt; mining = true;
                        if (time - lastMineSfx > 0.14f) { lastMineSfx = time; PlaySfx(Sfx.Mine); }
                        if (rand.NextDouble() < dt * 14) SpawnParticle(tx * World.TS + 8, ty * World.TS + 8, Rr(-30, 30), Rr(-30, 30), 0.35f, def.ore != null ? def.ore[0] : def.baseColors[1], 1);
                        if (world.dmg[i] >= def.hp) BreakTile(tx, ty);
                        else
                        {
                            int st = CrackStage(world.dmg[i], def.hp);
                            if (crackShown[i] != st) { crackShown[i] = (byte)st; if (TileChanged != null) TileChanged(tx, ty); }
                        }
                    }
                }
            }
            if (inp.lamp) PlaceItem(Item.Lamp, Tile.Lamp);
            if (inp.vent) PlaceItem(Item.Vent, Tile.Vent);
        }

        float lastMineSfx;
        public readonly byte[] crackShown = new byte[World.W * World.H];   // 화면에 표시 중인 균열 단계(타일별)
        public static int CrackStage(float dmg, float hp) { return dmg <= 0 ? 0 : Mathf.Min(3, 1 + (int)(dmg / hp * 3f)); }

        void PlaceItem(Item item, Tile tile)
        {
            int tx = cursorTx, ty = cursorTy;
            if (inv[(int)item] <= 0) { SayToast(Data.ItemName[(int)item] + "이(가) 없다 — 기지에서 제작하자", 1.6f); return; }
            if (!cursorReach || world.Get(tx, ty) != Tile.Floor) { SayToast("여기에는 놓을 수 없다", 1.2f); return; }
            world.tiles[World.Idx(tx, ty)] = (byte)tile; world.deco[World.Idx(tx, ty)] = 0; inv[(int)item]--;
            if (tile == Tile.Vent) Beat("vent");
            if (TileChanged != null) TileChanged(tx, ty);
        }

        // ---------- 전투 ----------
        void UpdateCombat(float dt, SimInput inp)
        {
            P.atkCd -= dt; if (P.swing > 0) P.swing -= dt;
            if (!inp.attack || P.atkCd > 0) return;
            P.atkCd = 0.38f; P.swing = 0.16f; PlaySfx(Sfx.Swing);
            float ax = inp.aim.x - P.x, ay = inp.aim.y - P.y, al = Hyp(ax, ay); if (al < 0.0001f) al = 1;
            float nx = ax / al, ny = ay / al;
            P.swingAng = Mathf.Atan2(ny, nx);
            float d = HarpoonDmg, range = Data.AttackRange;     // 터치/마우스 사거리 통일
            foreach (var e in new List<Enemy>(enemies))
            {
                float dx = e.x - P.x, dy = e.y - P.y, dist = Hyp(dx, dy), er = e.Def.r;
                if (dist > range + er) continue;
                if (dist > 10 && (dx * nx + dy * ny) / dist < 0.25f) continue;
                if (!world.LineClear(P.x, P.y, e.x, e.y)) continue;   // 벽 너머로는 맞지 않는다
                HitEnemy(e, d, nx, ny);
            }
            if (boss.active)
            {
                float dx = boss.x - P.x, dy = boss.y - P.y, dist = Hyp(dx, dy);
                if (dist <= range + 14 && !(dist > 10 && (dx * nx + dy * ny) / dist < 0.25f) && world.LineClear(P.x, P.y, boss.x, boss.y)) HitBoss(d);
            }
        }

        void HitEnemy(Enemy e, float d, float nx, float ny)
        {
            var def = e.Def;
            if (def.armor > 0) d = Mathf.Max(1f, d * (1f - 0.35f * def.armor));   // 방어는 비율로(전엔 d-1 이라 작살 Lv1·Lv2 가 같은 피해였다)
            e.hp -= d; e.flash = 0.15f; PlaySfx(Sfx.Hit);
            float kb = IsMini(e.type) ? 22f : 90f;   // 미니보스는 거의 밀리지 않는다
            e.kx = nx * kb; e.ky = ny * kb;
            for (int k = 0; k < 5; k++) SpawnParticle(e.x, e.y, Rr(-40, 40), Rr(-40, 40), 0.35f, new Color32(255, 255, 255, 255));
            for (int k = 0; k < 6; k++)     // 타격 불꽃: 공격 방향 쪽으로 튀는 밝은 큰 점 + 사방 작은 점
            {
                float a = Mathf.Atan2(ny, nx) + Rr(-0.7f, 0.7f), sp = Rr(70, 120);
                SpawnParticle(e.x, e.y, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, 0.22f, k % 2 == 0 ? new Color32(255, 240, 160, 255) : new Color32(255, 160, 80, 255), 2);
            }
            if (e.hp <= 0)
            {
                DropFrom(def.drop);
                if (e.elite) { DropFrom(def.drop); AddItem(Item.Pearl, 1); SayToast("정예 처치! 보상이 두 배 + 진주", 2.4f); Beat("elite"); }
                if (IsMini(e.type))
                {
                    miniDead[MiniIndex(e.type)] = true;
                    SayToast(def.name + "을(를) 쓰러뜨렸다! 소굴에 보상이 흩어졌다", 3.6f); PlaySfx(Sfx.Boss);
                    for (int k = 0; k < 24; k++) SpawnParticle(e.x, e.y, Rr(-80, 80), Rr(-80, 80), 1f, new Color32(255, 216, 107, 255), 2);
                    if (SaveRequested != null) SaveRequested();
                }
                enemies.Remove(e);
            }
        }

        void HitBoss(float d)
        {
            boss.hp -= d; boss.flash = 0.15f; PlaySfx(Sfx.Hit);
            for (int k = 0; k < 5; k++) SpawnParticle(boss.x, boss.y, Rr(-40, 40), Rr(-40, 40), 0.35f, new Color32(255, 255, 255, 255));
            if (boss.hp <= 0) KillBoss();
        }

        // ---------- 적 ----------
        void Wander(Enemy e, float dt, float spd, out float vx, out float vy)
        {
            e.wt -= dt;
            if (e.wt <= 0) { e.wt = Rr(1.5f, 3f); float a = Rr(0, 6.28f); e.dirx = Mathf.Cos(a); e.diry = Mathf.Sin(a); }
            vx = e.dirx * spd; vy = e.diry * spd;
        }

        void UpdateEnemies(float dt)
        {
            foreach (var e in new List<Enemy>(enemies))
            {
                var def = e.Def;
                e.t += dt; if (e.flash > 0) e.flash -= dt;
                float dx = P.x - e.x, dy = P.y - e.y, dist = Hyp(dx, dy); if (dist < 0.0001f) dist = 1;
                float vx = 0, vy = 0;
                switch (e.type)
                {
                    case EnemyType.Jelly:
                    {
                        e.wt -= dt;
                        if (e.wt <= 0) { e.wt = Rr(1.5f, 3.5f); float a = Rr(0, 6.28f); e.dirx = Mathf.Cos(a); e.diry = Mathf.Sin(a); }
                        float pulse = 0.5f + 0.5f * Mathf.Sin(e.t * 3);
                        vx = e.dirx * def.speed * pulse; vy = e.diry * def.speed * pulse;
                        break;
                    }
                    case EnemyType.Angler:
                        e.lt -= dt;
                        if (e.lunge > 0) { e.lunge -= dt; vx = e.lx * 120; vy = e.ly * 120; }
                        else if (dist < 120)
                        {
                            vx = dx / dist * def.speed; vy = dy / dist * def.speed;
                            if (e.lt <= 0 && dist < 80) { e.lunge = 0.35f; e.lx = dx / dist; e.ly = dy / dist; e.lt = Rr(2.5f, 3.5f); }
                        }
                        else Wander(e, dt, 12, out vx, out vy);
                        e.faceX = e.lunge > 0 ? e.lx : dx;
                        break;
                    case EnemyType.Crab:
                        if (dist < 100) { vx = dx / dist * def.speed; vy = dy / dist * def.speed; }
                        else Wander(e, dt, 10, out vx, out vy);
                        break;
                    case EnemyType.Eel:
                        e.lt -= dt;
                        if (e.lunge > 0) { e.lunge -= dt; vx = e.lx * 170; vy = e.ly * 170; if (e.lunge <= 0) e.back = 0.9f; }
                        else if (e.back > 0) { e.back -= dt; vx = -dx / dist * 40; vy = -dy / dist * 40; }   // 돌진 후 물러나기
                        else if (e.wind > 0) { e.wind -= dt; if (e.wind <= 0) { e.lunge = 0.5f; e.lx = dx / dist; e.ly = dy / dist; } }
                        else if (dist < 130 && e.lt <= 0) { e.wind = 0.5f; e.lt = Rr(2.2f, 3.2f); }
                        else if (dist < 160) { vx = dx / dist * 18; vy = dy / dist * 18; }
                        else Wander(e, dt, 14, out vx, out vy);
                        e.faceX = e.lunge > 0 ? e.lx : dx;
                        break;
                    case EnemyType.Wraith:
                        if (dist < 150) { vx = dx / dist * def.speed; vy = dy / dist * def.speed; }
                        else Wander(e, dt, 8, out vx, out vy);
                        break;
                    case EnemyType.CrabKing:     // 느리게 추격하다 멈춰서 0.6초 예고 후 충격파(탄 고리)
                    {
                        e.lt -= dt;
                        float hd = Hyp(e.homeX - e.x, e.homeY - e.y);
                        if (e.wind > 0) { e.wind -= dt; if (e.wind <= 0) { Shockwave(e, 10, Data.MiniWaveSpeed); e.lt = 3.2f; } }
                        else if (dist < 240) { vx = dx / dist * def.speed; vy = dy / dist * def.speed; if (e.lt <= 0 && dist < 150) e.wind = Data.MiniCrabWindup; }
                        else if (hd > 8) { vx = (e.homeX - e.x) / hd * 40; vy = (e.homeY - e.y) / hd * 40; e.hp = Mathf.Min(e.maxHp, e.hp + 8 * dt); }   // 멀어지면 소굴로 돌아가 회복
                        e.faceX = dx;
                        break;
                    }
                    case EnemyType.EelKing:      // 0.5초 예고 → 직선 돌진 → 물러나며 부채꼴 3연탄
                    {
                        e.lt -= dt;
                        float hd = Hyp(e.homeX - e.x, e.homeY - e.y);
                        if (e.lunge > 0) { e.lunge -= dt; vx = e.lx * Data.MiniEelDash; vy = e.ly * Data.MiniEelDash; if (e.lunge <= 0) { e.back = 1f; FanShot(e, 3, 0.4f, Data.MiniFanSpeed); } }
                        else if (e.back > 0) { e.back -= dt; vx = -dx / dist * 35; vy = -dy / dist * 35; }
                        else if (e.wind > 0) { e.wind -= dt; if (e.wind <= 0) { e.lunge = 0.55f; e.lx = dx / dist; e.ly = dy / dist; } }
                        else if (dist < 240 && e.lt <= 0) { e.wind = Data.MiniEelWindup; e.lt = Rr(2.3f, 3.3f); }
                        else if (dist < 240) { vx = dx / dist * def.speed * 0.6f; vy = dy / dist * def.speed * 0.6f; }
                        else if (hd > 8) { vx = (e.homeX - e.x) / hd * 45; vy = (e.homeY - e.y) / hd * 45; e.hp = Mathf.Min(e.maxHp, e.hp + 8 * dt); }
                        e.faceX = e.lunge > 0 ? e.lx : dx;
                        break;
                    }
                }
                float kd = Mathf.Pow(0.88f, dt * 60f);   // 프레임레이트와 무관한 넉백 감쇠
                vx += e.kx; vy += e.ky; e.kx *= kd; e.ky *= kd;
                if (Mathf.Abs(vx) > 0.0001f || Mathf.Abs(vy) > 0.0001f)
                {
                    if (e.type == EnemyType.Wraith)   // 벽을 통과한다(월드 밖으로는 못 나간다)
                    {
                        e.x = Mathf.Clamp(e.x + vx * dt, 40f, World.W * World.TS - 40f); e.y = Mathf.Clamp(e.y + vy * dt, 40f, World.H * World.TS - 40f);
                    }
                    else
                    {
                        bool hit = world.MoveBox(ref e.x, ref e.y, vx * dt, vy * dt, def.r - 1);
                        if (hit) { e.dirx = -e.dirx; e.diry = -e.diry; }
                    }
                }
                if (dist < def.r + 5)
                {
                    Damage(def.dmg + (e.elite ? 1 : 0));
                    if (P.invuln > 0.8f) { P.vx = -dx / dist * 120; P.vy = -dy / dist * 120; }
                }
            }
        }

        void UpdateSpawner(float dt)
        {
            spawnT -= dt;
            if (spawnT > 0 || enemies.Count > 45) return;
            spawnT = 3;
            for (int tries = 0; tries < 12; tries++)
            {
                float a = Rr(0, 6.28f), d = Rr(150, 260);
                float x = P.x + Mathf.Cos(a) * d, y = P.y + Mathf.Sin(a) * d;
                int tx = Mathf.FloorToInt(x / World.TS), ty = Mathf.FloorToInt(y / World.TS);
                if (!World.InBounds(tx, ty) || world.tiles[World.Idx(tx, ty)] != (byte)Tile.Floor) continue;
                if (Hyp(x - World.BX, y - World.BY) < 200) continue;
                if (Hyp(tx - World.ArenaX, ty - World.ArenaY) <= World.ArenaR + 1) continue;
                int zsp = World.ZoneOfTile(tx, ty);
                SpawnEnemy(PickType(zsp), tx * World.TS + 8, ty * World.TS + 8, RollElite(zsp));
                break;
            }
        }

        // ---------- 보스 ----------
        void UpdateBoss(float dt)
        {
            if (boss.dead) return;
            float dA = Hyp(P.x - World.AX, P.y - World.AY);
            if (!boss.active)
            {
                if (dA < (World.ArenaR + 3) * World.TS && started)
                {
                    boss.active = true; boss.x = World.AX; boss.y = World.AY - 40;   // 체력은 그대로(체크포인트 유지)
                    boss.t = 0; boss.fireT = 2.5f; boss.chargeT = 6; boss.charge = 0; boss.windup = 0;
                    SayToast("심연의 눈이 깨어났다!", 3f); PlaySfx(Sfx.Boss); Beat("bossWake");
                }
                else if (!bossWarned && dA < (World.ArenaR + 24) * World.TS && started)
                {
                    bossWarned = true;   // 기획 검토: 아무 안내 없이 보스가 깨어났다
                    SayToast("앞쪽에서 거대한 기척이 느껴진다… 작살 Lv3 · 방호복 Lv2 이상을 권한다", 4.5f);
                }
                return;
            }
            if (dA > (World.ArenaR + 14) * World.TS) { boss.active = false; ResetBossHp(); return; }
            boss.t += dt; if (boss.flash > 0) boss.flash -= dt;
            bool phase2 = boss.hp < Data.BossHp * 0.5f;
            if (phase2) Beat("bossPhase2");
            float dx = P.x - boss.x, dy = P.y - boss.y, dist = Hyp(dx, dy); if (dist < 0.0001f) dist = 1;
            if (boss.charge > 0) { boss.charge -= dt; boss.x += boss.cdx * 140 * dt; boss.y += boss.cdy * 140 * dt; }
            else if (boss.windup > 0)
            {
                // 돌진 예고: 제자리에서 떨며 방향을 정해 둔다(그 사이 옆으로 비킬 수 있다)
                boss.windup -= dt;
                if (boss.windup <= 0) boss.charge = 0.7f;
            }
            else
            {
                float sp = phase2 ? 30 : 22;
                boss.x += dx / dist * sp * dt; boss.y += dy / dist * sp * dt;
                boss.chargeT -= dt;
                if (boss.chargeT <= 0) { boss.windup = Data.BossWindup; boss.cdx = dx / dist; boss.cdy = dy / dist; boss.chargeT = phase2 ? 4 : 6; }
            }
            boss.fireT -= dt;
            if (boss.fireT <= 0)
            {
                boss.fireT = phase2 ? 1.5f : 2.4f;
                int n = phase2 ? 12 : 10; float off = Rr(0, 6.28f);
                for (int i = 0; i < n; i++)
                {
                    float a = off + i * 6.28318f / n;
                    bullets.Add(new Bullet { x = boss.x, y = boss.y, vx = Mathf.Cos(a) * 55, vy = Mathf.Sin(a) * 55, life = 6 });
                }
            }
            float bdx = boss.x - World.AX, bdy = boss.y - World.AY, bd = Hyp(bdx, bdy), lim = (World.ArenaR - 2) * World.TS;
            if (bd > lim) { boss.x = World.AX + bdx / bd * lim; boss.y = World.AY + bdy / bd * lim; }
            if (dist < 18) Damage(Data.BossContactDmg);
        }

        void KillBoss()
        {
            boss.dead = true; boss.active = false; bullets.Clear();
            var cols = new[] { new Color32(255, 107, 107, 255), new Color32(255, 216, 107, 255), new Color32(79, 240, 224, 255) };
            for (int k = 0; k < 40; k++) SpawnParticle(boss.x, boss.y, Rr(-90, 90), Rr(-90, 90), 1.2f, cols[k % 3], 2);
            AddItem(Item.Crystal, 10); AddItem(Item.Brass, 10);
            victory = true; PlaySfx(Sfx.Boss);
            if (SaveRequested != null) SaveRequested();
            if (VictoryReached != null) VictoryReached();
        }

        void UpdateBullets(float dt)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                b.x += b.vx * dt; b.y += b.vy * dt; b.life -= dt;
                if (b.life <= 0 || world.IsSolid(Mathf.FloorToInt(b.x / World.TS), Mathf.FloorToInt(b.y / World.TS))) { bullets.RemoveAt(i); continue; }
                if (Hyp(b.x - P.x, b.y - P.y) < 6)
                {
                    Damage(Data.BossBulletDmg);                       // 이 피해로 기절하면 Die() 가 탄을 전부 지운다 — 그때는 지울 것이 없다
                    if (i < bullets.Count) bullets.RemoveAt(i);
                    if (bullets.Count == 0) break;
                }
            }
        }

        void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i]; p.life -= dt;
                if (p.life <= 0) { particles.RemoveAt(i); continue; }
                p.x += p.vx * dt; p.y += p.vy * dt; p.vx *= 0.96f; p.vy *= 0.96f;
                particles[i] = p;
            }
        }

        // ---------- 탐험(미니맵) ----------
        void Explore()
        {
            int ptx = Mathf.FloorToInt(P.x / World.TS), pty = Mathf.FloorToInt(P.y / World.TS);
            if (ptx == lastPtx && pty == lastPty) return;
            lastPtx = ptx; lastPty = pty;
            int R = Mathf.CeilToInt(LightR / (float)World.TS) + 1;
            for (int y = pty - R; y <= pty + R; y++)
                for (int x = ptx - R; x <= ptx + R; x++)
                {
                    if (!World.InBounds(x, y) || Hyp(x - ptx, y - pty) > R) continue;
                    int i = World.Idx(x, y);
                    if (world.explored[i] == 0) { world.explored[i] = 1; exploredDirty.Add(i); }
                }
        }

        // ---------- 제작 ----------
        public bool TryCraft(int index)
        {
            if (index < 0 || index >= Data.Crafts.Length) return false;
            var c = Data.Crafts[index];
            if (!NearBeacon) { SayToast("기지 비콘 근처에서만 제작할 수 있다", 1.6f); return false; }
            if (c.upgrade)
            {
                var levels = Data.Up[(int)c.track]; int lv = P.lvl[(int)c.track];
                if (lv + 1 >= levels.Length) { SayToast("이미 최대 등급이다", 1.2f); return false; }
                var next = levels[lv + 1];
                if (!CanAfford(next.cost)) { SayToast("재료가 부족하다", 1.2f); return false; }
                foreach (var k in next.cost) inv[(int)k.item] -= k.amount;
                P.lvl[(int)c.track]++;
                SayToast(next.name + " 제작 완료!");
                if (c.track == Track.Tank) P.o2 = MaxO2;
                if (c.track == Track.Beacon) Beat("beacon");
                if (c.track == Track.Armor) P.hp = MaxHp;
            }
            else
            {
                if (!CanAfford(c.cost)) { SayToast("재료가 부족하다", 1.2f); return false; }
                foreach (var k in c.cost) inv[(int)k.item] -= k.amount;
                AddItem(c.give, c.giveCount);
                SayToast(c.label + " 제작 완료");
            }
            PlaySfx(Sfx.Craft); if (Buzz != null) Buzz(15);
            if (SaveRequested != null) SaveRequested();
            return true;
        }

        /// <summary>업그레이드 카드가 보여줄 '다음 단계'. 최대면 null.</summary>
        public Level NextLevel(Track t)
        {
            var levels = Data.Up[(int)t]; int lv = P.lvl[(int)t];
            return lv + 1 < levels.Length ? levels[lv + 1] : null;
        }

        // ---------- 터치 보조 조준 ----------
        /// <summary>가까운 적(작살 거리 보정 30px 이내)의 위치. 없으면 false.</summary>
        public bool NearestTarget(out Vector2 pos, float limit = 30f)
        {
            float best = 1e9f; pos = Vector2.zero; bool found = false;
            foreach (var e in enemies)
            {
                float d = Hyp(e.x - P.x, e.y - P.y) - e.Def.r;
                if (d < best && world.LineClear(P.x, P.y, e.x, e.y)) { best = d; pos = new Vector2(e.x, e.y); found = true; }   // 벽 뒤 적은 조준하지 않는다
            }
            if (boss.active)
            {
                float d = Hyp(boss.x - P.x, boss.y - P.y) - 14;
                if (d < best && world.LineClear(P.x, P.y, boss.x, boss.y)) { best = d; pos = new Vector2(boss.x, boss.y); found = true; }
            }
            return found && best <= limit;
        }

        /// <summary>바라보는 방향으로 광선을 쏴 처음 만나는 벽/광맥/상자/조개. 램프·환기구는 제외.</summary>
        public bool FrontTile(Vector2 face, out int tx, out int ty)
        {
            for (float d = 6; d <= 32; d += 4)
            {
                tx = Mathf.FloorToInt((P.x + face.x * d) / World.TS); ty = Mathf.FloorToInt((P.y + face.y * d) / World.TS);
                if (!World.InBounds(tx, ty)) break;
                int i = World.Idx(tx, ty); var t = (Tile)world.tiles[i];
                if (Data.Tiles[(int)t].solid || t == Tile.Chest || (t == Tile.Floor && world.deco[i] == 3)) return true;
            }
            tx = ty = 0; return false;
        }

        // ---------- 저장 / 불러오기 ----------
        public SaveData ToSave()
        {
            return new SaveData
            {
                seed = world.Seed, tiles = Convert.ToBase64String(world.tiles), deco = Convert.ToBase64String(world.deco),
                explored = Convert.ToBase64String(world.explored), px = P.x, py = P.y, hp = P.hp, o2 = P.o2,
                lvl = (int[])P.lvl.Clone(), inv = (int[])inv.Clone(), bossDead = boss.dead,
                miniDead = (miniDead[0] ? 1 : 0) | (miniDead[1] ? 2 : 0), goalMask = goalMask, storyMask = storyMask,
            };
        }

        public static Sim FromSave(SaveData s)
        {
            var sim = new Sim(s.seed);
            Buffer.BlockCopy(Convert.FromBase64String(s.tiles), 0, sim.world.tiles, 0, sim.world.tiles.Length);
            Buffer.BlockCopy(Convert.FromBase64String(s.deco), 0, sim.world.deco, 0, sim.world.deco.Length);
            Buffer.BlockCopy(Convert.FromBase64String(s.explored), 0, sim.world.explored, 0, sim.world.explored.Length);
            for (int i = 0; i < s.lvl.Length && i < sim.P.lvl.Length; i++) sim.P.lvl[i] = s.lvl[i];
            for (int i = 0; i < s.inv.Length && i < sim.inv.Length; i++) sim.inv[i] = s.inv[i];
            sim.P.x = s.px; sim.P.y = s.py; sim.P.hp = s.hp; sim.P.o2 = s.o2; sim.boss.dead = s.bossDead;
            sim.miniDead[0] = (s.miniDead & 1) != 0; sim.miniDead[1] = (s.miniDead & 2) != 0;
            sim.goalMask = s.goalMask; sim.storyMask = s.storyMask;
            sim.lastChapter = sim.Chapter;                                           // 불러오면 장 안내를 다시 띄우지 않는다
            for (int b = 0; b < Data.StoryIds.Length; b++)                          // 이야기 기록은 나온 비트에서 복원한다
                if ((sim.storyMask & (1 << b)) != 0) foreach (var line in Data.StoryLines[b]) sim.storyLog.Add(line);
            sim.P.invuln = Data.LoadInvuln;   // 불러온 직후 잠깐 무적
            for (int i = 0; i < sim.world.explored.Length; i++)   // 가장 깊이 가 본 구역을 탐험 기록에서 복원
                if (sim.world.explored[i] != 0) { int z = World.ZoneOfTile(i % World.W, i / World.W); if (z > sim.maxZone) sim.maxZone = z; }
            return sim;
        }

        public void RequestSave() { if (SaveRequested != null) SaveRequested(); }
    }
}
