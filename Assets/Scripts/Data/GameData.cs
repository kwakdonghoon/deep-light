using System.Collections.Generic;
using UnityEngine;

namespace Abyss
{
    // data.js 포팅. 수치를 바꿀 때는 이 파일만 고치면 된다. (나중에 ScriptableObject 로 옮기기 쉽게 순수 데이터로 유지)
    public enum Tile : byte { Floor = 0, Rock = 1, Hard = 2, Deep = 3, Scrap = 4, Brass = 5, Crystal = 6, Bedrock = 7, Lamp = 8, Vent = 9, Chest = 10 }
    public enum Item { Scrap = 0, Brass = 1, Crystal = 2, Pearl = 3, Gel = 4, Lamp = 5, Vent = 6, Potion = 7 }
    public enum EnemyType { Jelly, Angler, Urchin, Crab, Eel, Wraith, CrabKing, EelKing }   // CrabKing 부터는 구역 미니보스(소굴에 한 마리씩, 처치하면 다시 안 나온다)
    public enum Track { Pick = 0, Harpoon = 1, Light = 2, Tank = 3, Fins = 4, Armor = 5, Beacon = 6 }

    public struct Cost
    {
        public Item item; public int amount;
        public Cost(Item item, int amount) { this.item = item; this.amount = amount; }
    }

    public struct DropRange
    {
        public Item item; public int min, max;
        public DropRange(Item item, int min, int max) { this.item = item; this.min = min; this.max = max; }
    }

    public sealed class TileDef
    {
        public string name;
        public bool solid;
        public float hp;
        public int tier;
        public bool unbreakable;
        public DropRange[] drop;
        public int glow;
        public Color32 mini;
        public Color32[] baseColors;   // 3색
        public Color32[] ore;          // 2색 (광맥만)
    }

    public sealed class Level
    {
        public string name;
        public int tier; public float power;     // 곡괭이
        public int dmg;                          // 작살
        public int r;                            // 랜턴 시야(px)
        public int max;                          // 산소통
        public float drain;                      // 추진 핀: 산소 소모 배율(0 이면 1배)
        public float refill;                     // 산소통: 비콘·환기구 곁 충전 속도 배율(0 이면 1배)
        public float speed;                      // 추진 핀
        public int hp;                           // 방호복
        public Cost[] cost;                      // null 이면 기본 장비
    }

    public sealed class CraftDef
    {
        public string id, label;
        public bool upgrade;
        public Track track;
        public Item give; public int giveCount;
        public Cost[] cost;
    }

    public sealed class EnemyDef
    {
        public string name; public float hp, speed; public int dmg, r, armor, minZone, weight;
        public DropRange[] drop;
    }

    public static class Data
    {
        public static Color32 Hex(string s)
        {
            return new Color32(System.Convert.ToByte(s.Substring(1, 2), 16), System.Convert.ToByte(s.Substring(3, 2), 16), System.Convert.ToByte(s.Substring(5, 2), 16), 255);
        }

        static Color32[] Cols(params string[] h) { var a = new Color32[h.Length]; for (int i = 0; i < h.Length; i++) a[i] = Hex(h[i]); return a; }

        public static readonly TileDef[] Tiles = BuildTiles();

        static TileDef[] BuildTiles()
        {
            var t = new TileDef[11];
            var rock = Cols("#3d527c", "#4b6590", "#2f4062");
            var hard = Cols("#54447d", "#665293", "#42356a");
            var deep = Cols("#6e3b6c", "#834c80", "#552d53");
            t[(int)Tile.Floor] = new TileDef { name = "해저", solid = false, mini = Hex("#16486b") };
            t[(int)Tile.Rock] = new TileDef { name = "암반", solid = true, hp = 3, tier = 0, baseColors = rock, mini = Hex("#2a4060") };
            t[(int)Tile.Hard] = new TileDef { name = "경암", solid = true, hp = 6, tier = 1, baseColors = hard, mini = Hex("#3a2f58") };
            t[(int)Tile.Deep] = new TileDef { name = "심암", solid = true, hp = 10, tier = 2, baseColors = deep, mini = Hex("#4f2c4d") };
            t[(int)Tile.Scrap] = new TileDef { name = "고철 광맥", solid = true, hp = 4, tier = 0, baseColors = rock, ore = Cols("#e0904a", "#ffd3a0"), drop = new[] { new DropRange(Item.Scrap, 1, 2) }, mini = Hex("#e0904a") };
            t[(int)Tile.Brass] = new TileDef { name = "황동 광맥", solid = true, hp = 7, tier = 1, baseColors = hard, ore = Cols("#f2c14e", "#fff0a8"), drop = new[] { new DropRange(Item.Brass, 1, 2) }, mini = Hex("#f2c14e") };
            t[(int)Tile.Crystal] = new TileDef { name = "심해 수정", solid = true, hp = 11, tier = 2, baseColors = deep, ore = Cols("#4ff0e0", "#d8fffa"), drop = new[] { new DropRange(Item.Crystal, 1, 2) }, glow = 24, mini = Hex("#4ff0e0") };
            t[(int)Tile.Bedrock] = new TileDef { name = "기반암", solid = true, unbreakable = true, baseColors = Cols("#0d1220", "#121828", "#0a0e19"), mini = Hex("#05080f") };
            t[(int)Tile.Lamp] = new TileDef { name = "램프", solid = false, mini = Hex("#ffd86b") };
            t[(int)Tile.Vent] = new TileDef { name = "환기구", solid = false, mini = Hex("#7fe3ff") };
            t[(int)Tile.Chest] = new TileDef { name = "보물 상자", solid = false, mini = Hex("#ffd86b") };
            return t;
        }

        public static readonly string[] ItemName = { "고철", "황동", "수정", "진주", "젤", "램프", "환기구", "포션" };
        public static readonly string[] ItemIcon = { "scrap", "brass", "crystal", "pearl", "gel", "lamp", "vent", "potion" };
        public static readonly Color32[] ItemColor = Cols("#e0904a", "#f2c14e", "#4ff0e0", "#f4f0ff", "#d98cff", "#ffd86b", "#7fe3ff", "#4fc3f7");
        public static readonly string[] TrackLabel = { "곡괭이", "작살", "랜턴", "산소통", "추진 핀", "방호복", "비콘 확장" };
        public static readonly string[] TrackIcon = { "pick", "harpoon", "lamp", "tank", "fins", "armor", "beacon" };

        static Cost[] C(params object[] kv)
        {
            var a = new Cost[kv.Length / 2];
            for (int i = 0; i < a.Length; i++) a[i] = new Cost((Item)kv[i * 2], (int)kv[i * 2 + 1]);
            return a;
        }

        public static readonly Level[][] Up =
        {
            new[] { // 곡괭이
                new Level { name = "고철 곡괭이", tier = 0, power = 3 },
                new Level { name = "황동 곡괭이", tier = 1, power = 4, cost = C(Item.Scrap, 10, Item.Gel, 3) },     // 기획 검토: 3/5/8 → 3/4/6 (단단한 돌일수록 시간이 더 걸린다)
                new Level { name = "수정 곡괭이", tier = 2, power = 6, cost = C(Item.Brass, 20, Item.Gel, 8, Item.Pearl, 2) },
            },
            new[] { // 작살
                new Level { name = "작살 Lv1", dmg = 1 },
                new Level { name = "작살 Lv2", dmg = 2, cost = C(Item.Scrap, 8, Item.Gel, 2) },
                new Level { name = "작살 Lv3", dmg = 3, cost = C(Item.Brass, 14, Item.Gel, 5, Item.Pearl, 2) },
                new Level { name = "작살 Lv4", dmg = 5, cost = C(Item.Crystal, 10, Item.Brass, 12, Item.Pearl, 3) },
            },
            new[] { // 랜턴
                new Level { name = "랜턴 Lv1", r = 48 },
                new Level { name = "랜턴 Lv2", r = 68, cost = C(Item.Scrap, 6, Item.Gel, 1) },
                new Level { name = "랜턴 Lv3", r = 90, cost = C(Item.Brass, 12, Item.Gel, 4) },
                new Level { name = "랜턴 Lv4", r = 116, cost = C(Item.Crystal, 8, Item.Brass, 10, Item.Pearl, 2) },
            },
            new[] { // 산소통
                new Level { name = "산소통 Lv1", max = 40 },
                new Level { name = "산소통 Lv2", max = 65, cost = C(Item.Scrap, 8, Item.Gel, 2) },
                new Level { name = "산소통 Lv3", max = 100, refill = 1.3f, cost = C(Item.Brass, 12, Item.Gel, 6, Item.Pearl, 2) },     // Lv3+: 충전 속도도 빨라진다
                new Level { name = "산소통 Lv4", max = 150, refill = 1.6f, cost = C(Item.Crystal, 8, Item.Brass, 12, Item.Pearl, 3) },
            },
            new[] { // 추진 핀
                new Level { name = "추진 핀 Lv1", speed = 55 },
                new Level { name = "추진 핀 Lv2", speed = 65, cost = C(Item.Scrap, 12, Item.Pearl, 3) },
                new Level { name = "추진 핀 Lv3", speed = 76, drain = 0.85f, cost = C(Item.Brass, 14, Item.Pearl, 5) },     // Lv3+: 헤엄이 효율적이라 산소를 덜 쓴다
                new Level { name = "추진 핀 Lv4", speed = 90, drain = 0.7f, cost = C(Item.Crystal, 10, Item.Pearl, 5) },
            },
            new[] { // 방호복
                new Level { name = "방호복 Lv1", hp = 10 },
                new Level { name = "방호복 Lv2", hp = 14, cost = C(Item.Scrap, 12, Item.Gel, 5, Item.Pearl, 2) },
                new Level { name = "방호복 Lv3", hp = 18, cost = C(Item.Brass, 14, Item.Gel, 8, Item.Pearl, 4) },
                new Level { name = "방호복 Lv4", hp = 24, cost = C(Item.Crystal, 10, Item.Gel, 10, Item.Pearl, 4) },
            },
            new[] { // 비콘 확장: 산소 안전지대(r)와 제작 가능 거리가 함께 넓어진다 — 남는 광물의 소비처(기획 검토)
                new Level { name = "비콘 Lv1", r = 80 },
                new Level { name = "비콘 Lv2", r = 104, cost = C(Item.Scrap, 24, Item.Brass, 16, Item.Gel, 6) },
                new Level { name = "비콘 Lv3", r = 130, cost = C(Item.Brass, 24, Item.Crystal, 12, Item.Pearl, 4, Item.Gel, 8) },
            },
        };

        // 제작 메뉴 순서 = 키 1~9
        public static readonly CraftDef[] Crafts =
        {
            new CraftDef { id = "pick",    label = "곡괭이", upgrade = true, track = Track.Pick },
            new CraftDef { id = "harpoon", label = "작살",   upgrade = true, track = Track.Harpoon },
            new CraftDef { id = "light",   label = "랜턴",   upgrade = true, track = Track.Light },
            new CraftDef { id = "tank",    label = "산소통", upgrade = true, track = Track.Tank },
            new CraftDef { id = "fins",    label = "추진 핀", upgrade = true, track = Track.Fins },
            new CraftDef { id = "armor",   label = "방호복", upgrade = true, track = Track.Armor },
            new CraftDef { id = "lamp",   label = "램프 x2", give = Item.Lamp, giveCount = 2, cost = C(Item.Scrap, 2, Item.Gel, 1) },
            new CraftDef { id = "vent",    label = "환기구", give = Item.Vent, giveCount = 1, cost = C(Item.Scrap, 12, Item.Gel, 5) },   // 기획 검토: 8/3 → 12/5, 그리고 회수 불가
            new CraftDef { id = "beacon",  label = "비콘 확장", upgrade = true, track = Track.Beacon },   // 9번째(키 9) — 앞 번호를 바꾸지 않으려고 맨 뒤에 둔다
            new CraftDef { id = "potion",  label = "산소 포션 x2", give = Item.Potion, giveCount = 2, cost = C(Item.Gel, 2, Item.Crystal, 1) },   // 10번째: 산소 60% 즉시 충전(젤 버튼으로 마신다)
        };

        public static readonly EnemyDef[] Enemies =
        {
            new EnemyDef { name = "해파리", hp = 3, speed = 14, dmg = 1, r = 5, minZone = 0, weight = 5, drop = new[] { new DropRange(Item.Gel, 1, 2) } },   // 밸런스 점검: 젤이 필요량의 7배였다 → 해파리 젤 1~2
            new EnemyDef { name = "아귀", hp = 7, speed = 40, dmg = 2, r = 6, minZone = 1, weight = 3, drop = new[] { new DropRange(Item.Gel, 1, 2) } },
            new EnemyDef { name = "성게", hp = 4, speed = 0, dmg = 1, r = 5, minZone = 0, weight = 3, drop = new[] { new DropRange(Item.Scrap, 1, 2), new DropRange(Item.Gel, 0, 1) } },
            new EnemyDef { name = "게", hp = 10, speed = 26, dmg = 2, r = 6, armor = 1, minZone = 1, weight = 2, drop = new[] { new DropRange(Item.Scrap, 2, 3), new DropRange(Item.Pearl, 0, 1) } },
            new EnemyDef { name = "전기뱀장어", hp = 9, speed = 30, dmg = 2, r = 6, minZone = 2, weight = 3, drop = new[] { new DropRange(Item.Gel, 1, 2), new DropRange(Item.Crystal, 0, 1) } },
            // 심연 전용: 벽을 통과해 천천히 다가오는 망령. 벽 속에 있는 동안은 작살이 닿지 않는다(시야 규칙) — 벽에서 나올 때를 노려야 한다.
            new EnemyDef { name = "망령", hp = 8, speed = 22, dmg = 2, r = 6, minZone = 2, weight = 3, drop = new[] { new DropRange(Item.Gel, 1, 2), new DropRange(Item.Pearl, 0, 1), new DropRange(Item.Crystal, 0, 1) } },
            // 구역 미니보스(소굴에 고정, 일반 스폰 제외 minZone=99). 보상은 확정.
            new EnemyDef { name = "녹슨 집게왕", hp = 30, speed = 22, dmg = 2, r = 11, armor = 1, minZone = 99, weight = 0, drop = new[] { new DropRange(Item.Brass, 6, 6), new DropRange(Item.Pearl, 3, 3), new DropRange(Item.Gel, 5, 5), new DropRange(Item.Potion, 1, 1) } },
            new EnemyDef { name = "번개 장어왕", hp = 42, speed = 30, dmg = 2, r = 11, minZone = 99, weight = 0, drop = new[] { new DropRange(Item.Crystal, 5, 5), new DropRange(Item.Pearl, 4, 4), new DropRange(Item.Gel, 6, 6), new DropRange(Item.Potion, 2, 2) } },
        };

        public const float BossHp = 160f;     // 전투 밸런스 점검: 140 → 180 → 160 (작살 Lv3 로 약 20초, Lv4 로 약 13초)
        public const int BossContactDmg = 2, BossBulletDmg = 1;   // 접촉 3 → 2: 붙어서 맞으면 초당 3.3 피해라 방호복 Lv2 로도 버틸 수 없었다

        // 보물 상자 보상 (구역별)
        public static readonly DropRange[][] ChestLoot =
        {
            new[] { new DropRange(Item.Scrap, 3, 6), new DropRange(Item.Gel, 1, 3), new DropRange(Item.Pearl, 0, 1), new DropRange(Item.Lamp, 1, 1) },
            new[] { new DropRange(Item.Brass, 3, 6), new DropRange(Item.Gel, 2, 4), new DropRange(Item.Pearl, 1, 2), new DropRange(Item.Vent, 0, 1), new DropRange(Item.Potion, 0, 1) },
            new[] { new DropRange(Item.Crystal, 2, 4), new DropRange(Item.Brass, 3, 5), new DropRange(Item.Pearl, 2, 3), new DropRange(Item.Gel, 3, 5), new DropRange(Item.Potion, 1, 2) },
        };

        public static readonly string[] ZoneName = { "얕은 폐허", "가라앉은 거리", "심연" };

        // 세계관(기획 검토): 구역에 들어설 때 한 줄, 상자에서 나오는 유실물 쪽지 12종
        // ---------- 이야기 ----------
        // 무전 안내자는 비콘의 관제 AI '아라'. 장면(비트)마다 한 번만, 몇 줄이 4.8초 간격으로 나온다.
        // 비트 ID: goal0~13 은 같은 번호의 목표를 달성했을 때, 나머지는 사건이 처음 일어났을 때. (저장은 이 순서의 비트마스크라 순서를 바꾸지 말 것)
        public static readonly string[] StoryIds =
        {
            "start", "goal0", "goal1", "goal2", "goal3", "goal4", "goal5", "goal6", "goal7", "goal8", "goal9", "goal10", "goal11", "goal12", "goal13",
            "bossWake", "bossPhase2", "lowO2", "firstDeath", "ruin", "beacon", "vent", "elite", "potion",
        };

        public static readonly string[][] StoryLines =
        {
            new[] { "아라: …연결됐어요. 살아 있는 등대지기가 있었군요.", "아라: 저는 비콘의 관제 AI, 아라예요. 출력 12%… 불빛이 꺼지면 저도 꺼져요.", "아라: 비콘 곁에서만 숨을 쉴 수 있어요. 광맥에서 고철부터 모아 주세요." },
            new[] { "아라: 고철이 모였네요! 이제 해파리를 잡아 젤을 구해요 — 제작의 접착제예요." },
            new[] { "아라: 젤 확보! 비콘 앞에서 곡괭이를 강화해 봐요." },
            new[] { "아라: 황동 곡괭이… 더 단단한 벽도 뚫을 수 있어요.", "아라: 근처에 오래된 보물 상자가 있어요. 지도의 금색 점이에요." },
            new[] { "아라: 그 쪽지… 도시가 가라앉던 밤의 기록이에요.", "아라: 모두 이 비콘만 믿고 있었죠. 그러니 끝까지 켜 둬야 해요." },
            new[] { "아라: 산소통이 커졌어요. 더 오래, 더 멀리 다녀올 수 있겠어요." },
            new[] { "아라: 시야가 넓어졌어요. 비콘 출력 25%… 이제 거리로 나가 볼까요?" },
            new[] { "아라: 가라앉은 거리예요. 신호등이 아직 깜박이죠? 도시 전력망이 살아 있다는 뜻이에요.", "아라: 중계기 둘이 고장 났어요. 붉은 점의 집게왕과 장어왕이 지키고 있어요.", "아라: 이 도시엔 항구를 지키던 감시 등대도 있었어요. '눈'이라 불렀죠." },
            new[] { "아라: 첫 번째 중계기 복구! 출력 40%, 비콘이 더 멀리 비춰요." },
            new[] { "아라: 두 번째 중계기도 살아났어요. 출력 55%!", "아라: 기록이 열렸어요. '눈'은 원래 도시를 지키던 감시 등대래요." },
            new[] { "아라: 작살이 묵직해졌어요. 이 소리… 옛 항구의 닻 소리 같아요." },
            new[] { "아라: 방호복이 튼튼해졌네요. 이 도시 사람들도 이렇게 내려갔을까요." },
            new[] { "아라: 심연이에요… 빛이 닿지 않아요. 아니, 무언가가 빛을 먹고 있어요.", "아라: 감시 등대의 '눈'이에요. 빛을 먹으며 변했나 봐요… 제단에 잠들어 있어요.", "아라: 작살 Lv3, 방호복 Lv2 이상은 갖추고 가요." },
            new[] { "아라: 수정이 노래해요… 이제 심연의 암반도 부술 수 있어요." },
            new[] { "아라: 눈이… 감겼어요. 비콘 출력 100%, 되살아났어요!", "아라: 기록이 열렸어요. 눈은 도시를 지키다 빛을 먹게 됐대요.", "아라: 아주 깊은 곳에서 또 다른 불빛이 깜박여요. 신호가 더 있어요.", "아라: 고마워요, 등대지기. 저는… 아직 꺼지지 않았어요." },
            new[] { "아라: 눈이 떴어요! 탄을 조심해요 — 움직이면서 싸워요!" },
            new[] { "아라: 눈이 충혈됐어요! 탄이 빨라져요, 조금만 더!" },
            new[] { "아라: 산소가 얼마 없어요! 비콘이나 환기구 곁으로 돌아와요." },
            new[] { "아라: …돌아왔군요. 쓰러져도 비콘은 당신을 기다려요.", "아라: 자원은 조금 잃었지만, 장비는 그대로예요." },
            new[] { "아라: 이 방은 시민 대피소였나 봐요. 이름 모를 사람들의 흔적이에요." },
            new[] { "아라: 비콘이 더 멀리 비춰요. 제 목소리도 또렷해졌죠?" },
            new[] { "아라: 환기구예요. 놓으면 먼 곳에서도 숨 쉴 수 있어요. 단, 회수는 못 해요." },
            new[] { "아라: 금빛 개체는 보통보다 훨씬 강해요. 쓰러뜨리면 보상도 두 배예요." },
            new[] { "아라: 산소 포션이에요. 급할 때를 위해 아껴 두세요." },
        };

        public static readonly string[] ChapterTitle = { "제1장 · 꺼져가는 불빛", "제2장 · 가라앉은 거리", "제3장 · 깊어지는 어둠", "제4장 · 심연의 눈" };
        public static readonly int[] ChapterOfGoal = { 1, 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 3, 3, 4 };    // 목표 번호 → 장

        // 한동안 진행이 없을 때 아라가 한 번 건네는 힌트(목표 번호 순서)
        public static readonly string[] GoalHints =
        {
            "아라: 지도의 주황 점이 비콘 곁 광맥이에요. 곡괭이로 캐 봐요.",
            "아라: 해파리는 비콘 주변을 떠다녀요. 작살로 잡으면 젤이 나와요.",
            "아라: 재료가 모였으면 비콘 곁에서 곡괭이를 강화해요.",
            "아라: 지도의 금색 점으로 가 봐요. 오래된 상자가 있어요.",
            "아라: 비콘 곁에서 산소통을 강화하면 더 멀리 갈 수 있어요.",
            "아라: 랜턴을 강화하면 어둠 속 광맥이 더 잘 보여요.",
            "아라: 비콘에서 멀어지면 거리가 나와요. 산소를 보면서 천천히 가요.",
            "아라: 붉은 점이 녹슨 집게왕의 소굴이에요. 멈춰 서면 충격파가 와요.",
            "아라: 번개 장어왕은 돌진 전에 몸을 떨어요. 그때 옆으로 피해요.",
            "아라: 황동을 모아 작살을 Lv3까지 강화해요. 심연 전에 꼭 필요해요.",
            "아라: 방호복을 강화하면 체력이 늘어요. 젤도 넉넉히 챙겨요.",
            "아라: 비콘에서 위쪽 대로를 따라가면 심연이에요. 환기구가 쉬어 갈 곳이에요.",
            "아라: 수정 곡괭이에는 진주가 필요해요. 조개를 찾아봐요.",
            "아라: 대로 끝의 제단으로 가요. 눈이 기다리고 있어요.",
        };

        public static readonly string[] ZoneLore =
        {
            "무너진 건물 틈으로 비콘의 불빛이 닿는다",
            "신호등이 아직 깜박인다",
            "불빛이 닿지 않는 곳… 무언가 지켜보고 있다",
        };

        public static readonly string[] ChestNotes =
        {
            "쪽지: 마지막 배는 새벽에 떠났다",
            "쪽지: 신호등이 꺼지면 돌아오지 마",
            "쪽지: 비콘을 지켜라, 눈이 온다",
            "쪽지: 깊은 곳에선 빛이 먹힌다",
            "쪽지: 시계탑은 3시에 멈췄다",
            "쪽지: 아이를 지하철역에 두고 왔다",
            "쪽지: 환기구 곁에서만 숨 쉴 수 있다",
            "쪽지: 눈은 불빛을 따라온다",
            "쪽지: 젤은 먹을 수 있다. 맛은 최악",
            "쪽지: 이 상자를 연 사람에게 행운을",
            "쪽지: 도시는 아직 숨 쉬고 있다",
            "쪽지: 비콘이 꺼지면 모두 끝이다",
        };

        // 튜닝 값
        public const float Reach = 44f, BeaconRadius = 80f, CraftRadius = 112f;
        public const float O2DrainBase = 1f, O2DrainPerZone = 0.4f, O2Refill = 25f;   // 구역당 소모 0.3 → 0.4 (심연 긴장감)
        public const int VentRadiusTiles = 3;
        public const float DeathLossRate = 0.2f;                                       // 30% → 20%, 젤은 면제(Sim 의 사망 처리)
        public const float AttackRange = 32f;                                         // 터치/마우스 작살 사거리 통일(전엔 36 / 26)
        public const float HealCooldown = 1.5f;                                        // 젤 회복 쿨다운
        public const float LoadInvuln = 2f;                                            // 불러온 직후 무적(초)
        public const float BossWindup = 0.5f;                                          // 돌진 예고(초)
        // 미니보스 난이도(시작 장비로도 도전할 수 있되, 작살·방호복 Lv2 면 편하게). 숫자만 바꿔서 조정한다.
        public const float MiniCrabWindup = 0.7f, MiniWaveSpeed = 42f;                 // 집게왕: 충격파 예고 시간 / 탄 속도
        public const float MiniEelWindup = 0.6f, MiniEelDash = 170f, MiniFanSpeed = 55f; // 장어왕: 돌진 예고 / 돌진 속도 / 부채꼴 탄 속도
        public const float PotionO2Frac = 0.6f;                                       // 포션 한 병이 채우는 산소 비율
        public const float EliteChanceZone1 = 0.05f, EliteChanceZone2 = 0.08f;         // 정예 개체 확률(구역 미니보스 대용)
        public const float BossCheckpointFrac = 0.49f;                                 // 2단계에서 죽으면 보스 체력을 이 비율까지만 복구
    }
}
