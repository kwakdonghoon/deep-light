using System;
using UnityEngine;

namespace Abyss
{
    /// <summary>월드 데이터(타일/손상/장식/탐험)와 생성·충돌. game.js 의 generate()/collides() 포팅. Unity 오브젝트에 의존하지 않아 EditMode 테스트가 가능하다.</summary>
    public sealed class World
    {
        public const int W = 160, H = 160, TS = 16;
        public const int CX = 80, CY = 80;
        public const int ArenaX = 80, ArenaY = 20, ArenaR = 9;
        public const float BX = CX * TS, BY = CY * TS;                    // 비콘(기지) 중심, px
        public const float AX = ArenaX * TS + 8, AY = ArenaY * TS + 8;    // 보스 아레나 중심, px

        public readonly byte[] tiles = new byte[W * H];
        public readonly float[] dmg = new float[W * H];
        public readonly byte[] deco = new byte[W * H];        // 1=해초 2=조개껍질 3=진주 조개
        public readonly byte[] explored = new byte[W * H];
        public int Seed { get; private set; }

        public static int Idx(int x, int y) { return y * W + x; }
        public static bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < W && y < H; }

        public Tile Get(int x, int y) { return InBounds(x, y) ? (Tile)tiles[Idx(x, y)] : Tile.Bedrock; }
        public bool IsSolid(int x, int y) { return Data.Tiles[(int)Get(x, y)].solid; }

        public static int ZoneOfTile(int x, int y)
        {
            double d = Math.Sqrt((double)((x - CX) * (x - CX) + (y - CY) * (y - CY)));
            return d < 30 ? 0 : d < 55 ? 1 : 2;
        }
        public static int ZoneOfPx(float x, float y) { return ZoneOfTile(Mathf.FloorToInt(x / TS), Mathf.FloorToInt(y / TS)); }

        static bool IsRock(byte t) { return t == (byte)Tile.Rock || t == (byte)Tile.Hard || t == (byte)Tile.Deep; }

        static Tile PickOre(int z, double r)
        {
            if (z == 0) return r < 0.85 ? Tile.Scrap : Tile.Brass;
            if (z == 1) return r < 0.65 ? Tile.Brass : r < 0.90 ? Tile.Scrap : Tile.Crystal;
            return r < 0.25 ? Tile.Crystal : r < 0.65 ? Tile.Brass : Tile.Scrap;
        }

        static double Hyp(double a, double b) { return Math.Sqrt(a * a + b * b); }

        /// <summary>같은 seed 면 웹 프로토타입과 같은 월드. rnd() 호출 순서(단락 평가 포함)를 그대로 유지해야 한다.</summary>
        public void Generate(int seed)
        {
            Seed = seed;
            var rng = new Mulberry32(seed);
            Array.Clear(tiles, 0, tiles.Length); Array.Clear(dmg, 0, dmg.Length);
            Array.Clear(deco, 0, deco.Length); Array.Clear(explored, 0, explored.Length);

            var g = new byte[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool border = x < 2 || y < 2 || x >= W - 2 || y >= H - 2;
                    g[Idx(x, y)] = border ? (byte)1 : (rng.Next() < 0.46 ? (byte)1 : (byte)0);
                }
            for (int it = 0; it < 4; it++)
            {
                var n = new byte[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) { n[Idx(x, y)] = 1; continue; }
                        int c = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) c += g[Idx(x + dx, y + dy)];
                        n[Idx(x, y)] = (byte)(c >= 5 ? 1 : 0);
                    }
                g = n;
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Hyp(x - CX, y - CY) < 7) g[Idx(x, y)] = 0;
                    if (Hyp(x - ArenaX, y - ArenaY) <= ArenaR) g[Idx(x, y)] = 0;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) { tiles[i] = (byte)Tile.Bedrock; continue; }
                    if (g[i] == 0) { tiles[i] = (byte)Tile.Floor; continue; }
                    int z = ZoneOfTile(x, y);
                    tiles[i] = (byte)(z == 0 ? Tile.Rock : z == 1 ? Tile.Hard : Tile.Deep);
                }
            // 광맥 군집
            for (int y = 2; y < H - 2; y++)
                for (int x = 2; x < W - 2; x++)
                {
                    if (!IsRock(tiles[Idx(x, y)]) || rng.Next() > 0.024) continue;
                    Tile ore = PickOre(ZoneOfTile(x, y), rng.Next());
                    tiles[Idx(x, y)] = (byte)ore;
                    int[] ox = { 1, -1, 0, 0 }, oy = { 0, 0, 1, -1 };
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + ox[k], ny = y + oy[k];
                        if (InBounds(nx, ny) && IsRock(tiles[Idx(nx, ny)]) && rng.Next() < 0.5) tiles[Idx(nx, ny)] = (byte)ore;
                    }
                }
            // 장식(해초/껍질/조개)과 보물 상자
            for (int y = 2; y < H - 2; y++)
                for (int x = 2; x < W - 2; x++)
                {
                    int i = Idx(x, y);
                    if (tiles[i] != (byte)Tile.Floor) continue;
                    if (IsSolid(x, y - 1) && rng.Next() < 0.14) deco[i] = 1;
                    else if (rng.Next() < 0.02) deco[i] = 2;
                    else if (rng.Next() < 0.008 && Hyp(x - CX, y - CY) > 10) deco[i] = 3;
                    else if (rng.Next() < 0.0035 && Hyp(x - CX, y - CY) > 12 && Hyp(x - ArenaX, y - ArenaY) > ArenaR + 1) tiles[i] = (byte)Tile.Chest;
                }
        }

        // ---------- 기획 검토 반영: 기본 생성(웹과 동일) 위에 덧입히는 후처리 ----------
        static Tile HostRock(int zone) { return zone == 0 ? Tile.Rock : zone == 1 ? Tile.Hard : Tile.Deep; }

        /// <summary>
        /// 웹 프로토타입 월드(Generate)는 그대로 두고, 기획팀 검토를 반영한다. 별도 난수(seed 파생)라 Generate 의 rnd 순서를 건드리지 않는다.
        ///  1) 광맥 절반을 보통 암반으로 되돌린다(광물 과잉), 진주 조개를 늘린다(진주 병목)
        ///  2) 비콘에서 보스 제단까지 '침몰한 대로' 터널: 폭 3, 구불구불, 램프와 폐허 환기구가 줄지어 있다(길과 산소 보급 보장)
        ///  3) 구역마다 숨은 폐허 방(사방이 암반으로 막힌 5x5, 안에 상자와 램프): 랜드마크 겸 보상
        /// </summary>
        public void DesignPass(int seed)
        {
            var r = new Mulberry32(seed ^ 0x2B7E1516);
            // 1) 광물·진주
            for (int y = 2; y < H - 2; y++)
                for (int x = 2; x < W - 2; x++)
                {
                    int i = Idx(x, y); var t = (Tile)tiles[i];
                    if ((t == Tile.Scrap || t == Tile.Brass || t == Tile.Crystal) && r.Next() < 0.5) tiles[i] = (byte)HostRock(ZoneOfTile(x, y));
                    else if (t == Tile.Chest && r.Next() < 0.40) tiles[i] = (byte)Tile.Floor;       // 상자도 너무 흔해(보상 과다) 40%를 걷어낸다(시작 상자·폐허 방 상자는 이 뒤에 확정 배치)
                    else if (t == Tile.Floor && deco[i] == 3 && r.Next() < 0.66) deco[i] = 0;   // 진주 조개가 너무 많아(필요량의 9배) 3분의 2를 걷어낸다 — 밸런스 점검
                }
            // 1.5) 구역별 지형 성격(같은 동굴 규칙에서 벗어나게): 얕은 폐허=넓게 열린 방, 가라앉은 거리=격자 도로, 심연=막힌 바위 기둥이 많은 미로
            var snap = (byte[])tiles.Clone();
            for (int y = 3; y < H - 3; y++)
                for (int x = 3; x < W - 3; x++)
                {
                    int i = Idx(x, y), z = ZoneOfTile(x, y);
                    if (z == 0 && IsRock(snap[i]))
                    {
                        int open = 0;
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if ((dx != 0 || dy != 0) && snap[Idx(x + dx, y + dy)] == (byte)Tile.Floor) open++;
                        if (open >= 4) tiles[i] = (byte)Tile.Floor;                    // 틈새를 깎아 방을 넓힌다
                    }
                    else if (z == 1 && (x % 20 < 2 || y % 20 < 2) && IsRock(snap[i])) tiles[i] = (byte)Tile.Floor;   // 폭 2의 격자 도로
                    else if (z == 2 && snap[i] == (byte)Tile.Floor && r.Next() < 0.022) tiles[i] = (byte)Tile.Deep;  // 길을 막는 바위 기둥
                }
            // 1.7) 미니보스 소굴: 반경 6 의 열린 방 + 비콘 쪽으로 이어지는 폭 2 통로
            foreach (var lair in MiniLairs)
            {
                for (int dy = -6; dy <= 6; dy++)
                    for (int dx = -6; dx <= 6; dx++)
                        if (dx * dx + dy * dy <= 36) CarveRoad(lair[0] + dx, lair[1] + dy);
                double len = Hyp(CX - lair[0], CY - lair[1]); int steps = (int)len;
                for (int s = 0; s <= steps; s++)
                {
                    double t = s / (double)steps; int px2 = (int)Math.Round(lair[0] + (CX - lair[0]) * t), py2 = (int)Math.Round(lair[1] + (CY - lair[1]) * t);
                    if (Hyp(px2 - CX, py2 - CY) < 8) break;
                    CarveRoad(px2, py2); CarveRoad(px2 + 1, py2); CarveRoad(px2, py2 + 1);
                }
            }
            // 1.8) 초반 재미: 비콘 바로 곁에 '고철 광맥 두 덩이'와 '보물 상자 굴'을 확정 배치한다(첫 1~2분 안에 채굴·보상을 맛보게)
            foreach (var blob in StarterVeins)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (dx * dx + dy * dy > 5) continue;
                        int x = blob[0] + dx, y = blob[1] + dy; if (!InBounds(x, y)) continue;
                        int i = Idx(x, y);
                        bool core = dx * dx + dy * dy <= 2;                          // 중심 5칸은 꼭 광맥(바닥이었어도), 바깥 고리는 막힌 바위일 때만
                        if (tiles[i] == (byte)Tile.Bedrock || tiles[i] == (byte)Tile.Lamp || tiles[i] == (byte)Tile.Vent || tiles[i] == (byte)Tile.Chest) continue;
                        if (core || IsRock(tiles[i]) || tiles[i] == (byte)Tile.Scrap) { tiles[i] = (byte)Tile.Scrap; deco[i] = 0; }
                    }
            {
                int sx = StarterChest[0], sy = StarterChest[1];
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) if (dx * dx + dy * dy <= 5) CarveRoad(sx + dx, sy + dy);
                double sl = Hyp(CX - sx, CY - sy); int sst = (int)sl;
                for (int s = 0; s <= sst; s++)
                {
                    double t = s / (double)sst; int qx = (int)Math.Round(sx + (CX - sx) * t), qy = (int)Math.Round(sy + (CY - sy) * t);
                    if (Hyp(qx - CX, qy - CY) < 8) break;
                    CarveRoad(qx, qy); CarveRoad(qx + 1, qy);
                }
                tiles[Idx(sx, sy)] = (byte)Tile.Chest;
            }
            // 2) 대로: 비콘 위쪽 가장자리에서 아레나 아래까지
            int px = CX, pyStart = CY - 6, pyEnd = ArenaY + ArenaR - 1, step = 0;
            for (int py = pyStart; py >= pyEnd; py--, step++)
            {
                double pull = (ArenaX - px) * 0.18;                      // 아레나 쪽으로 서서히 끌어당긴다
                double v = r.Next() - 0.5 + pull * 0.6;
                if (v > 0.3) px++; else if (v < -0.3) px--;
                px = Mathf.Clamp(px, 12, W - 13);
                for (int dx = -1; dx <= 1; dx++) CarveRoad(px + dx, py);
                if (r.Next() < 0.5) CarveRoad(px + (r.Next() < 0.5 ? 2 : -2), py);   // 가장자리가 울퉁불퉁하도록
                if (step > 0 && step % 11 == 0) { int lx = px + (step % 22 == 0 ? 1 : -1); tiles[Idx(lx, py)] = (byte)Tile.Lamp; }
                int total = pyStart - pyEnd;
                if (step == total / 3 || step == total * 2 / 3) tiles[Idx(px, py)] = (byte)Tile.Vent;   // 폐허 환기구: 산소 보급 거점
            }
            // 3) 숨은 폐허 방 4곳(구역마다 하나 이상, 확정 상자)
            foreach (var rm in RuinCenters) CarveRuin(rm[0], rm[1]);
            // 4) 구역 분위기 장식: 얕은 폐허=부서진 기둥, 가라앉은 거리=침몰한 표지판, 심연=거대한 뼈대와 발광 버섯
            for (int y = 3; y < H - 3; y++)
                for (int x = 3; x < W - 3; x++)
                {
                    int i = Idx(x, y);
                    if (tiles[i] != (byte)Tile.Floor || deco[i] != 0 || Hyp(x - CX, y - CY) < 10) continue;
                    int z = ZoneOfTile(x, y); double v = r.Next();
                    if (z == 0 && v < 0.010) deco[i] = DecoPillar;
                    else if (z == 1 && v < 0.010) deco[i] = DecoSign;
                    else if (z == 2) { if (v < 0.008) deco[i] = DecoBones; else if (v < 0.020) deco[i] = DecoFungus; }
                }
        }

        /// <summary>초반 확정 요소: 비콘 곁 고철 광맥 덩이 중심들과 보물 상자 굴(비콘에서 약 15칸).</summary>
        public static readonly int[][] StarterVeins = { new[] { CX - 10, CY - 3 }, new[] { CX + 10, CY - 2 }, new[] { CX + 3, CY + 11 } };
        public static readonly int[] StarterChest = { CX - 12, CY + 9 };

        /// <summary>미니보스 소굴 중심(타일): 0=얕은 폐허의 녹슨 집게왕, 1=가라앉은 거리의 번개 장어왕.</summary>
        public static readonly int[][] MiniLairs = { new[] { CX - 20, CY - 18 }, new[] { CX + 33, CY + 32 } };

        // 장식 코드(웹 월드의 1~3 뒤에 이어서)
        public const byte DecoPillar = 4, DecoSign = 5, DecoBones = 6, DecoFungus = 7;

        /// <summary>숨은 폐허 방의 중심(타일). 중심에 상자가 있다. 지도 표시에도 쓴다.</summary>
        public static readonly int[][] RuinCenters = { new[] { CX + 22, CY + 9 }, new[] { CX - 41, CY + 22 }, new[] { CX + 47, CY - 30 }, new[] { CX - 58, CY - 26 } };

        void CarveRoad(int x, int y)
        {
            if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) return;
            int i = Idx(x, y); tiles[i] = (byte)Tile.Floor; deco[i] = 0; dmg[i] = 0;
        }

        void CarveRuin(int cx, int cy)
        {
            var host = HostRock(ZoneOfTile(cx, cy));
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    int x = cx + dx, y = cy + dy; if (x < 3 || y < 3 || x >= W - 3 || y >= H - 3) continue;
                    int i = Idx(x, y); deco[i] = 0; dmg[i] = 0;
                    bool wall = Math.Abs(dx) == 3 || Math.Abs(dy) == 3;
                    tiles[i] = (byte)(wall ? host : Tile.Floor);          // 바깥 한 겹은 암반(막힘), 안쪽 5x5 는 방
                }
            tiles[Idx(cx, cy)] = (byte)Tile.Chest;
            tiles[Idx(cx - 2, cy - 2)] = (byte)Tile.Lamp;
        }

        /// <summary>두 점 사이에 벽(단단한 타일)이 없는지. 벽 너머 공격·자동 조준을 막는다.</summary>
        public bool LineClear(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0, len = Mathf.Sqrt(dx * dx + dy * dy);
            int n = Mathf.CeilToInt(len / 4f);
            for (int k = 1; k < n; k++)
            {
                float t = k / (float)n;
                if (IsSolid(Mathf.FloorToInt((x0 + dx * t) / TS), Mathf.FloorToInt((y0 + dy * t) / TS))) return false;
            }
            return true;
        }

        // ---------- 충돌 ----------
        public bool Collides(float x, float y, float h)
        {
            int x0 = Mathf.FloorToInt((x - h) / TS), x1 = Mathf.FloorToInt((x + h - 0.01f) / TS);
            int y0 = Mathf.FloorToInt((y - h) / TS), y1 = Mathf.FloorToInt((y + h - 0.01f) / TS);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                    if (IsSolid(tx, ty)) return true;
            return false;
        }

        /// <summary>축 분리 AABB 이동. 어느 축이든 막히면 true.</summary>
        public bool MoveBox(ref float ex, ref float ey, float dx, float dy, float h)
        {
            bool hit = false;
            if (!Collides(ex + dx, ey, h)) ex += dx; else hit = true;
            if (!Collides(ex, ey + dy, h)) ey += dy; else hit = true;
            return hit;
        }

        public void SetTile(int x, int y, Tile t) { tiles[Idx(x, y)] = (byte)t; }
    }
}
