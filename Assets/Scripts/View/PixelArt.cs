using System.Collections.Generic;
using UnityEngine;

namespace Abyss
{
    /// <summary>행(위→아래)으로 저장하는 작은 픽셀 캔버스. canvas 2D 코드(game.js)를 옮기기 쉽게 y 는 위에서 아래로 센다.</summary>
    public sealed class PixelCanvas
    {
        public readonly int w, h;
        public readonly Color32[] px;

        public PixelCanvas(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

        public void Clear() { for (int i = 0; i < px.Length; i++) px[i] = default; }

        public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }

        public void Fill(int x, int y, int rw, int rh, Color32 c)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c);
        }

        /// <summary>기존 픽셀 위에 알파 합성 (canvas 의 rgba fillStyle 과 같은 효과)</summary>
        public void Blend(int x, int y, int rw, int rh, Color32 c, float a)
        {
            for (int j = y; j < y + rh; j++)
                for (int i = x; i < x + rw; i++)
                {
                    if (i < 0 || j < 0 || i >= w || j >= h) continue;
                    Color32 d = px[j * w + i]; float da = d.a / 255f, oa = a + da * (1 - a);
                    if (oa <= 0) continue;
                    px[j * w + i] = new Color32(
                        (byte)Mathf.RoundToInt((c.r * a + d.r * da * (1 - a)) / oa),
                        (byte)Mathf.RoundToInt((c.g * a + d.g * da * (1 - a)) / oa),
                        (byte)Mathf.RoundToInt((c.b * a + d.b * da * (1 - a)) / oa),
                        (byte)Mathf.RoundToInt(oa * 255));
                }
        }

        /// <summary>game.js 의 disc(): 행마다 가로선으로 채우는 픽셀 정확한 원</summary>
        public void Disc(int cx, int cy, int r, Color32 c)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int ww = Mathf.FloorToInt(Mathf.Sqrt(r * r - dy * dy + 0.5f));
                Fill(cx - ww, cy + dy, ww * 2 + 1, 1, c);
            }
        }

        public void Blit(PixelCanvas src, int ox, int oy)
        {
            for (int j = 0; j < src.h; j++) for (int i = 0; i < src.w; i++) { var c = src.px[j * src.w + i]; if (c.a > 0) Set(ox + i, oy + j, c); }
        }

        public void Upload(Texture2D tex)
        {
            var flipped = new Color32[px.Length];
            for (int j = 0; j < h; j++) System.Array.Copy(px, j * w, flipped, (h - 1 - j) * w, w);
            tex.SetPixels32(flipped); tex.Apply(false);
        }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Upload(t); return t;
        }

        public Sprite ToSprite(float pivotX = 0.5f, float pivotY = 0.5f, int ppu = 16)
        {
            var t = ToTexture();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(pivotX, pivotY), ppu, 0, SpriteMeshType.FullRect);
        }
    }

    /// <summary>게임 안의 모든 스프라이트를 코드로 만든다 (game.js 의 SPR / makeTex 포팅). 외부 아트 파일 없이도 동작.</summary>
    public static class PixelArt
    {
        static Color32 H(string s) { return Data.Hex(s); }

        public static PixelCanvas FromRows(string[] rows, Dictionary<char, Color32> pal)
        {
            var c = new PixelCanvas(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (pal.TryGetValue(rows[y][x], out var col)) c.Set(x, y, col);
            return c;
        }

        static Dictionary<char, Color32> Pal(params object[] kv)
        {
            var d = new Dictionary<char, Color32>();
            for (int i = 0; i < kv.Length; i += 2) d[(char)kv[i]] = H((string)kv[i + 1]);
            return d;
        }

        // ---------- 엔티티 ----------
        public static Sprite Player()
        {
            var pal = Pal('o', "#0a1626", 'h', "#e8b23c", 'H', "#a87a1c", 'g', "#8fe9ff", 'G', "#3aa0c4", 's', "#3a74b8", 'S', "#25508a", 't', "#cfd9e2", 'f', "#e4572e");
            var c = FromRows(new[] {
                "...oooooo...", "..ohhhhhho..", ".ohhggggGho.", ".ohgggggGho.", ".ohhgggGhHo.", "..ohhhhHHo..",
                ".otoSsssSo..", "ottoSssssSo.", "ottosssssSSo", ".ootSsssSSo.", "..ooSsSSSo..", "...osso.oso.", "..offo..offo" }, pal);
            return c.ToSprite(0.5f, 6f / 13f);   // 웹: drawImage(player, -6, -7) -> 중심이 위에서 7px
        }

        /// <summary>잠수부 시트(Resources/Art/Diver/diver_sheet.png, 16x16 칸 10열x3행). [방향 0=아래 1=위 2=옆(오른쪽), 프레임 0·1=대기 2~7=수영 8·9=공격(예비·찌르기)]. 시트가 없으면 null.</summary>
        public static Sprite[,] DiverSheet()
        {
            var tex = Resources.Load<Texture2D>("Art/Diver/diver_sheet");
            if (tex == null || tex.width < 160 || tex.height < 48) return null;
            tex.filterMode = FilterMode.Point;
            var r = new Sprite[3, 10];
            for (int d = 0; d < 3; d++)
                for (int f = 0; f < 10; f++)
                    r[d, f] = Sprite.Create(tex, new Rect(f * 16, tex.height - (d + 1) * 16, 16, 16), new Vector2(0.47f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            return r;
        }

        /// <summary>pxl 로 그린 적 PNG(Resources/Art/Enemies). 없으면 null → 아래 코드 스프라이트로 대체.</summary>
        static Sprite LoadEnemy(string name, float px = 0.5f, float py = 0.5f)
        {
            var t = Resources.Load<Texture2D>("Art/Enemies/" + name);
            if (t == null) return null;
            t.filterMode = FilterMode.Point;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(px, py), 16, 0, SpriteMeshType.FullRect);
        }

        public static Sprite[] Jelly()
        {
            var j0 = LoadEnemy("jelly_0"); var j1 = LoadEnemy("jelly_1");
            if (j0 != null && j1 != null) return new[] { j0, j1 };
            var pal = Pal('b', "#d98cff", 'B', "#9b5de0", 'e', "#ffffff");
            string[] top = { "....bbbb....", "..bbbbbbbb..", ".bbbbbbbbbb.", ".bbebbbbebb.", ".bbbbbbbbbb.", ".BBBBBBBBBB." };
            string[] a = { "..B.B..B.B..", "..B.B..B.B..", ".B..B..B..B." };
            string[] b = { "...B.B.B.B..", "..B.B.B.B...", "...B.B..B.B." };
            var ra = new List<string>(top); ra.AddRange(a);
            var rb = new List<string>(top); rb.AddRange(b);
            return new[] { FromRows(ra.ToArray(), pal).ToSprite(), FromRows(rb.ToArray(), pal).ToSprite() };
        }

        public static Sprite Angler()
        {
            var png = LoadEnemy("angler", 0.5f, 3f / 8f); if (png != null) return png;
            var pal = Pal('d', "#2d3f6b", 'D', "#1b2748", 'w', "#ffffff", 'e', "#ff4d4d");
            var c = FromRows(new[] { "....dddddd....", "..dddddddddd..", ".ddddewdddddd.", "ddddddddddddDD", "wdwdwddddddDD.", "dDDDDDDDDDDD..", ".DDDDDDDDDD...", "...DD..DD....." }, pal);
            return c.ToSprite(0.5f, 3f / 8f);     // 웹: drawImage(angler, -7, -5)
        }

        public static Sprite Urchin()
        {
            var png = LoadEnemy("urchin"); if (png != null) return png;
            var c = new PixelCanvas(12, 12); Color32 sp = H("#a34fd6");
            for (int a = 0; a < 8; a++)
            {
                float dx = Mathf.Cos(a * Mathf.PI / 4), dy = Mathf.Sin(a * Mathf.PI / 4);
                for (int k = 2; k <= 5; k++) c.Set(Mathf.RoundToInt(6 + dx * k), Mathf.RoundToInt(6 + dy * k), sp);
            }
            c.Fill(4, 4, 4, 4, H("#6b2d8f")); c.Set(5, 5, H("#c98bff"));
            return c.ToSprite();
        }

        public static Sprite Crab()
        {
            var png = LoadEnemy("crab"); if (png != null) return png;
            var pal = Pal('c', "#e05a3a", 'C', "#a53a22", 'e', "#ffffff");
            return FromRows(new[] { "cc........cc", "cC........Cc", ".cccccccccc.", "..cecccecc..", ".CcccccccCc.", "..CCCCCCCC..", ".c.c.cc.c.c.", "c..c....c..c" }, pal).ToSprite();
        }

        public static Sprite Eel()
        {
            var png = LoadEnemy("eel"); if (png != null) return png;
            var pal = Pal('y', "#ffe14d", 'Y', "#b8a000", 'e', "#ff3b3b");
            return FromRows(new[] { "..........yy....", ".....YYYYYYYYyy.", "eYYYYYYYYYYYYYYy", ".YYYYYYYYYYYYYY.", "..yyYYYYYYyyyy..", ".......yy......." }, pal).ToSprite();
        }

        /// <summary>적의 두 번째 애니메이션 프레임(PNG 이름 + "_b"). 없으면 null → 첫 프레임만 쓴다.</summary>
        public static Sprite EnemyFrameB(string name, float px = 0.5f, float py = 0.5f) { return LoadEnemy(name + "_b", px, py); }

        public static Sprite CrabKing() { var s = LoadEnemy("crab_king"); return s != null ? s : Crab(); }
        public static Sprite EelKing() { var s = LoadEnemy("eel_king"); return s != null ? s : Eel(); }

        public static Sprite Wraith()
        {
            var png = LoadEnemy("wraith"); if (png != null) return png;
            var pal = Pal('w', "#c8ecff", 'W', "#7fb6d8", 'k', "#0a1626");   // PNG 가 없을 때의 대체 모양
            return FromRows(new[] { "..wwwwww..", ".wwwwwwww.", "wwkwwwwkww", "wwkwwwwkww", "wwwwwwwwww", "wWwWwWwWwW", "W.W.W.W.W." }, pal).ToSprite();
        }

        /// <summary>물빛 카우스틱 패턴(64x64, 이음매 없음). 화면 전체에 깔려 느리게 흘러간다. 없으면 null.</summary>
        public static Sprite Caustics()
        {
            var t = Resources.Load<Texture2D>("Art/Tiles/caustics");
            if (t == null) return null;
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        }

        /// <summary>구역 장식(기둥·표지판·뼈대·발광 버섯). PNG 가 없으면 null 이라 아무것도 그리지 않는다.</summary>
        public static Sprite DecoProp(string name) { return LoadProp(name); }

        public static Sprite Lure()
        {
            var c = new PixelCanvas(5, 5); c.Disc(2, 2, 2, H("#ffe36b")); c.Set(2, 1, H("#ffffff")); return c.ToSprite();
        }

        public static Sprite Bullet()
        {
            var c = new PixelCanvas(8, 8); c.Disc(4, 4, 3, H("#ff6b6b")); c.Fill(3, 3, 2, 2, H("#ffe0e0")); return c.ToSprite();
        }

        public static Sprite WhitePixel()
        {
            var c = new PixelCanvas(1, 1); c.Set(0, 0, new Color32(255, 255, 255, 255)); return c.ToSprite(0.5f, 0.5f, 16);
        }

        public static Sprite Rect(int w, int h, Color32 col)
        {
            var c = new PixelCanvas(w, h); c.Fill(0, 0, w, h, col); return c.ToSprite();
        }

        public static Sprite Beacon()
        {
            var c = new PixelCanvas(16, 28);   // 중심 (8,14)
            c.Fill(8 - 6, 14 + 4, 12, 6, H("#3b4a63"));
            c.Fill(8 - 3, 14 - 8, 6, 14, H("#5b6b80"));
            c.Fill(8 - 4, 14 - 14, 8, 7, H("#7fe3ff"));
            c.Fill(8 - 2, 14 - 12, 3, 3, H("#e8fbff"));
            return c.ToSprite();
        }

        /// <summary>비콘의 산소 안전지대를 나타내는 점선 원 (반지름 80px)</summary>
        public static Sprite BeaconRing(int radius = -1)
        {
            int r = radius > 0 ? radius : Mathf.RoundToInt(Data.BeaconRadius), size = r * 2 + 4, mid = size / 2;
            var c = new PixelCanvas(size, size); var col = new Color32(127, 227, 255, 89);
            for (float a = 0; a < 6.28f; a += 0.1f) c.Set(Mathf.RoundToInt(mid + Mathf.Cos(a) * r), Mathf.RoundToInt(mid + Mathf.Sin(a) * r), col);
            return c.ToSprite();
        }

        public static Sprite Altar(bool bright)
        {
            var c = new PixelCanvas(16, 28);   // 중심 (8,14)
            c.Fill(0, 14 + 4, 16, 6, H("#35203a"));
            Color32 col = H(bright ? "#ff6b6b" : "#c04040");
            c.Fill(8 - 3, 14 - 10, 6, 14, col); c.Fill(8 - 1, 14 - 14, 2, 4, col);
            return c.ToSprite();
        }

        /// <summary>타깃 코너 브래킷. o = 맥동 오프셋(0/1). 20x20 캔버스에 16x16 타일을 중앙 배치.</summary>
        public static Sprite Brackets(bool reach, int o)
        {
            var c = new PixelCanvas(20, 20); int L = 4;
            int x0 = 2 - o, y0 = 2 - o, x1 = 2 + 16 + o, y1 = 2 + 16 + o;
            int[][] bars = {
                new[] { x0, y0, L, 1 }, new[] { x0, y0, 1, L }, new[] { x1 - L, y0, L, 1 }, new[] { x1 - 1, y0, 1, L },
                new[] { x0, y1 - 1, L, 1 }, new[] { x0, y1 - L, 1, L }, new[] { x1 - L, y1 - 1, L, 1 }, new[] { x1 - 1, y1 - L, 1, L } };
            Color32 dark = H("#0a1626"), col = reach ? new Color32(255, 255, 255, 255) : H("#ff6b6b");
            foreach (var b in bars) c.Fill(b[0] - 1, b[1] - 1, b[2] + 2, b[3] + 2, dark);
            foreach (var b in bars) c.Fill(b[0], b[1], b[2], b[3], col);
            if (!reach) for (int k = -3; k <= 3; k++) { c.Set(2 + 8 + k, 2 + 8 + k, col); c.Set(2 + 8 + k, 2 + 8 - k, col); }
            return c.ToSprite();
        }

        // ---------- 타일 ----------
        static int Fl(double v) { return (int)System.Math.Floor(v); }

        static readonly string[] TileFile = { "floor", "rock", "hard", "deep", "scrap", "brass", "crystal", "bedrock" };

        /// <summary>pxl 로 그린 타일 PNG(Resources/Art/Tiles, 읽기 가능 임포트)를 캔버스로 옮긴다. 없으면 null → 아래 코드 생성으로 대체.</summary>
        static PixelCanvas TileFromPng(Tile tile, int variant)
        {
            int ti = (int)tile; if (ti >= TileFile.Length) return null;
            var tex = Resources.Load<Texture2D>("Art/Tiles/tile_" + TileFile[ti] + "_" + (variant & 3));
            if (tex == null || !tex.isReadable || tex.width != 16 || tex.height != 16) return null;
            var src = tex.GetPixels32(); var c = new PixelCanvas(16, 16);
            for (int j = 0; j < 16; j++) System.Array.Copy(src, (15 - j) * 16, c.px, j * 16, 16);
            return c;
        }

        /// <summary>장식·상자·램프·환기구 PNG(투명 16x16). 없으면 null.</summary>
        static Sprite LoadProp(string name)
        {
            var t = Resources.Load<Texture2D>("Art/Tiles/" + name);
            if (t == null) return null;
            t.filterMode = FilterMode.Point;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        }

        public static PixelCanvas TileTexture(Tile tile, int variant)
        {
            var png = TileFromPng(tile, variant); if (png != null) return png;
            var c = new PixelCanvas(16, 16);
            if (tile == Tile.Floor)
            {
                var r = new Mulberry32(777 + variant);
                c.Fill(0, 0, 16, 16, H("#16365a"));
                Color32[] cols = { H("#1b4268"), H("#112a47"), H("#2f6a8c") };
                for (int i = 0; i < 30; i++) { var col = cols[Fl(r.Next() * 3)]; int x = Fl(r.Next() * 16), y = Fl(r.Next() * 16); c.Fill(x, y, 1, 1, col); }
                return c;
            }
            var def = Data.Tiles[(int)tile];
            var rg = new Mulberry32((int)tile * 97 + variant * 13 + 5);
            c.Fill(0, 0, 16, 16, def.baseColors[0]);
            for (int i = 0; i < 46; i++)
            {
                var col = def.baseColors[1 + (rg.Next() < 0.5 ? 0 : 1)];
                int x = Fl(rg.Next() * 16), y = Fl(rg.Next() * 16), w = rg.Next() < 0.3 ? 2 : 1;
                c.Fill(x, y, w, 1, col);
            }
            if (def.ore != null)
            {
                int n = 3 + Fl(rg.Next() * 3);
                for (int i = 0; i < n; i++)
                {
                    int ox = 2 + Fl(rg.Next() * 11), oy = 2 + Fl(rg.Next() * 11);
                    c.Fill(ox, oy, 2, 2, def.ore[0]); c.Fill(ox - 1, oy, 1, 1, def.ore[0]); c.Fill(ox, oy, 1, 1, def.ore[1]);
                }
            }
            return c;
        }

        /// <summary>edge: 벽이면 '위가 뚫려 있음'(윗면 하이라이트), 바닥이면 '위가 벽'(윗면 그림자)</summary>
        public static Sprite TileSprite(Tile tile, int variant, bool edge)
        {
            var c = TileTexture(tile, variant);
            bool solid = Data.Tiles[(int)tile].solid;
            if (edge)
            {
                if (solid) c.Blend(0, 0, 16, 2, new Color32(255, 255, 255, 255), 0.14f);
                else c.Blend(0, 0, 16, 4, new Color32(0, 0, 0, 255), 0.35f);
            }
            return c.ToSprite();
        }

        // ---------- 장식(투명 16x16) ----------
        public static Sprite Seaweed(int variant)
        {
            var png = LoadProp("deco_seaweed_" + (variant % 3)); if (png != null) return png;
            var c = new PixelCanvas(16, 16); Color32 col = H("#2f8f6b");
            for (int s = 0; s < 3; s++)
            {
                int bx = 3 + s * 5, hh = 5 + ((variant + s) % 3) * 2;
                for (int k = 0; k < hh; k++) c.Set(bx + Mathf.RoundToInt(Mathf.Sin(variant * 1.3f + s) * k * 0.2f), 4 + hh - k, col);
            }
            return c.ToSprite();
        }

        public static Sprite Shell()
        {
            var png = LoadProp("deco_shell"); if (png != null) return png;
            var c = new PixelCanvas(16, 16); c.Fill(6, 11, 3, 2, H("#d9c9a8")); c.Set(7, 11, H("#fff4d6")); return c.ToSprite();
        }

        public static Sprite Clam()
        {
            var png = LoadProp("deco_clam"); if (png != null) return png;
            var pal = Pal('p', "#d9c9e8", 'P', "#9b88b8", 'w', "#ffffff");
            var s = FromRows(new[] { "..PPPP..", ".PppppP.", "PppppppP", "PPPwwPPP", ".PPPPPP." }, pal);
            var c = new PixelCanvas(16, 16); c.Blit(s, 4, 9); return c.ToSprite();
        }

        public static Sprite Chest()
        {
            var png = LoadProp("deco_chest"); if (png != null) return png;
            var pal = Pal('b', "#7a4a22", 'B', "#4a2a10", 'g', "#f2c14e", 'G', "#fff0a8");
            var s = FromRows(new[] { "..BBBBBBBB..", ".BbbbbbbbbB.", "BbbbbbbbbbbB", "BBBBggggBBBB", "BbbbbGGbbbbB", "BbbbbggbbbbB", "BbbbbbbbbbbB", "BbbbbbbbbbbB", "BBBBBBBBBBBB", ".BB......BB." }, pal);
            var c = new PixelCanvas(16, 16); c.Blit(s, 2, 5); return c.ToSprite();
        }

        public static Sprite Lamp()
        {
            var png = LoadProp("deco_lamp"); if (png != null) return png;
            var c = new PixelCanvas(16, 16);
            c.Fill(7, 6, 2, 8, H("#4a5a70")); c.Fill(5, 3, 6, 4, H("#ffd86b")); c.Fill(6, 4, 2, 2, H("#fff4c0"));
            return c.ToSprite();
        }

        public static Sprite Vent()
        {
            var png = LoadProp("deco_vent"); if (png != null) return png;
            var c = new PixelCanvas(16, 16);
            c.Fill(3, 10, 10, 4, H("#5b6b80")); c.Fill(4, 11, 8, 1, H("#7fe3ff"));
            Color32 b = H("#9fe8ff"); c.Set(5, 9, b); c.Set(8, 5, b); c.Set(11, 2, b);
            return c.ToSprite();
        }

        /// <summary>채굴 진행 균열. stage 1~3</summary>
        public static Sprite Crack(int stage)
        {
            var png = LoadProp("crack_" + Mathf.Clamp(stage, 1, 3)); if (png != null) return png;
            var c = new PixelCanvas(16, 16); var r = new Mulberry32(100 + stage);
            int n = stage == 1 ? 5 : stage == 2 ? 9 : 14;
            for (int k = 0; k < n; k++) c.Fill(2 + Fl(r.Next() * 12), 2 + Fl(r.Next() * 12), 1, 2, new Color32(0, 0, 0, 166));
            return c.ToSprite();
        }

        // ---------- 보스: pxl 로 그린 몸통·눈동자(Resources/Art/Boss) + 코드로 그리는 촉수 ----------
        public static Sprite LoadBoss(string name)
        {
            var t = Resources.Load<Texture2D>("Art/Boss/" + name);
            if (t == null) return null;
            t.filterMode = FilterMode.Point;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        }

        /// <summary>일렁이는 촉수 8개(윤곽선 포함). 몸통 뒤에 깔리므로 중심부는 가려진다.</summary>
        public static void DrawBossArms(PixelCanvas c, float t, bool angry)
        {
            c.Clear(); int cx = 32, cy = 32;
            Color32 dark = H("#0a1626"), mid = angry ? H("#8f2d6b") : H("#6b2d8f"), tip = angry ? H("#e0508a") : H("#a34fd6");
            Color32 suck = angry ? H("#ffc0da") : H("#e0b0ff"), belly = angry ? H("#b8407f") : H("#8a45b0");
            float spin = angry ? 0.9f : 0.5f, sway = angry ? 0.34f : 0.22f;
            for (int pass = 0; pass < 2; pass++)
                for (int k = 0; k < 8; k++)
                {
                    float a0 = t * spin + k * Mathf.PI / 4;
                    for (int s = 10; s <= 27; s++)
                    {
                        float a = a0 + Mathf.Sin(t * 3f + s * 0.3f + k * 1.7f) * sway * (s - 10) / 17f;
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        int x = Mathf.RoundToInt(cx + ca * s), y = Mathf.RoundToInt(cy + sa * s);
                        int r = s < 14 ? 3 : s < 18 ? 2 : s < 23 ? 1 : 0;       // 끝으로 갈수록 가늘어진다
                        if (pass == 0) { if (r == 0) c.Disc(x, y, 1, dark); else c.Disc(x, y, r + 1, dark); continue; }
                        Color32 col = s >= 23 ? tip : mid;
                        if (r == 0) c.Set(x, y, col); else c.Disc(x, y, r, col);
                        if (r >= 1 && s % 3 == 0)                                // 한쪽 면에 빨판 점
                        {
                            int side = (k & 1) == 0 ? 1 : -1;
                            c.Set(Mathf.RoundToInt(x - sa * side * (r > 1 ? 1 : 0)), Mathf.RoundToInt(y + ca * side * (r > 1 ? 1 : 0)), suck);
                        }
                        else if (r >= 2) c.Set(Mathf.RoundToInt(x + sa * r * 0.6f), Mathf.RoundToInt(y - ca * r * 0.6f), belly);
                    }
                }
        }

        // ---------- 보스(프레임마다 다시 그림, 예전 방식: PNG 가 없을 때 대체) ----------
        public static void DrawBoss(PixelCanvas c, float t, Vector2 look, bool flash)
        {
            c.Clear(); int cx = 32, cy = 32;
            Color32 tent = H("#6b2d8f");
            for (int k = 0; k < 8; k++)
            {
                float a = t * 0.8f + k * Mathf.PI / 4;
                for (int s = 12; s <= 22; s += 2) c.Fill(Mathf.RoundToInt(cx + Mathf.Cos(a) * s), Mathf.RoundToInt(cy + Mathf.Sin(a) * s), 2, 2, tent);
            }
            c.Disc(cx, cy, 14, flash ? new Color32(255, 255, 255, 255) : H("#e8d8f0"));
            float dl = look.magnitude; if (dl < 0.0001f) { look = Vector2.right; dl = 1; }
            float lx = look.x / dl, ly = look.y / dl;
            int ix = Mathf.RoundToInt(cx + lx * 4), iy = Mathf.RoundToInt(cy + ly * 4);
            c.Disc(ix, iy, 7, H("#c0392b"));
            c.Disc(Mathf.RoundToInt(ix + lx), Mathf.RoundToInt(iy + ly), 3, H("#1a0a0a"));
            c.Fill(ix - 2, iy - 2, 2, 2, new Color32(255, 255, 255, 255));
        }
    }
}
