using UnityEngine;

namespace Abyss
{
    /// <summary>
    /// 어둠 마스크. 웹의 renderLight() 와 같은 알고리즘: 화면 전체를 어둡게 칠하고, 광원마다 4단 밴드 원으로 어둠을 걷어낸다.
    /// URP Light2D 를 쓰지 않는 이유: 웹과 룩이 똑같고, 파이프라인 설정 없이 모든 플랫폼에서 동일하게 동작한다.
    /// (나중에 Light2D 로 바꾸려면 이 클래스만 교체하면 된다.)
    /// </summary>
    public sealed class LightMask
    {
        public const int H = 180;
        // 6단 밴드(웹은 4단): 단계가 잘게 나뉘어 빛의 가장자리가 부드럽다. 중심 투명도는 웹과 거의 같게 맞췄다.
        static readonly float[] BandScale = { 1f, 0.86f, 0.72f, 0.58f, 0.44f, 0.30f };
        static readonly float[] BandAlpha = { 0.15f, 0.22f, 0.30f, 0.38f, 0.46f, 0.55f };
        const float BaseDark = 0.93f;

        public Texture2D Texture { get; private set; }
        public Sprite Sprite { get; private set; }
        int w;
        float[] dark;
        Color32[] pixels;

        public void Resize(int width)
        {
            if (Texture != null && w == width) return;
            w = width;
            dark = new float[w * H]; pixels = new Color32[w * H]; hue = new Color32[w * H]; amt = new float[w * H];
            if (Texture != null) Object.Destroy(Texture);
            Texture = new Texture2D(w, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Sprite = Sprite.Create(Texture, new Rect(0, 0, w, H), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
        }

        static int Rnd(float v) { return (int)Mathf.Floor(v + 0.5f); }

        void DiscMul(int cx, int cy, int r, float mul)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy; if (y < 0 || y >= H) continue;
                int ww = Mathf.FloorToInt(Mathf.Sqrt(r * r - dy * dy + 0.5f));
                int x0 = Mathf.Max(0, cx - ww), x1 = Mathf.Min(w - 1, cx + ww);
                for (int x = x0; x <= x1; x++) dark[y * w + x] *= mul;
            }
        }

        // 색 있는 광원: 가장 바깥 원 안에서 어둠 색을 광원 색 쪽으로 물들인다(어둠이 남은 가장자리가 그 색 후광으로 보인다)
        static readonly Color32 DarkCol = new Color32(5, 10, 22, 255);
        public static readonly Color32 Warm = new Color32(70, 38, 6, 255), Teal = new Color32(4, 58, 62, 255), Cyan = new Color32(8, 48, 78, 255),
            Gold = new Color32(68, 50, 8, 255), Violet = new Color32(54, 16, 78, 255), Crimson = new Color32(74, 8, 24, 255);
        Color32[] hue; float[] amt;

        void DiscTint(int cx, int cy, int r, Color32 col, float strength)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                int y = cy + dy; if (y < 0 || y >= H) continue;
                int ww = Mathf.FloorToInt(Mathf.Sqrt(r * r - dy * dy + 0.5f));
                int x0 = Mathf.Max(0, cx - ww), x1 = Mathf.Min(w - 1, cx + ww);
                for (int x = x0; x <= x1; x++) { int i = y * w + x; if (strength >= amt[i]) { amt[i] = strength; hue[i] = col; } }
            }
        }

        void LightAt(float x, float y, float r) { LightAt(x, y, r, DarkCol, 0f); }

        void LightAt(float x, float y, float r, Color32 col, float tint)
        {
            if (x + r < 0 || y + r < 0 || x - r > w || y - r > H) return;
            int cx = Rnd(x), cy = Rnd(y);
            if (tint > 0f) DiscTint(cx, cy, Rnd(r), col, tint);
            for (int b = 0; b < BandScale.Length; b++) DiscMul(cx, cy, Rnd(r * BandScale[b]), 1f - BandAlpha[b]);
        }

        /// <summary>기본 어둠: 비콘에서 멀어질수록 연속으로 어두워진다(구역 경계에서 갑자기 변하지 않는다).</summary>
        const float AmbientNear = 0.82f, AmbientFar = 0.95f, AmbientFullDist = 60f;   // 비콘 곁 / 가장 깊은 곳 / 그 거리(타일)
        static float Ambient(Sim sim)
        {
            float d = Mathf.Sqrt((sim.P.x - World.BX) * (sim.P.x - World.BX) + (sim.P.y - World.BY) * (sim.P.y - World.BY)) / World.TS;
            return Mathf.Lerp(AmbientNear, AmbientFar, Mathf.Clamp01(d / AmbientFullDist));
        }

        /// <param name="camX">카메라 왼쪽 위 월드 px(정수)</param>
        public void Update(Sim sim, int camX, int camY)
        {
            float ambient = Ambient(sim);
            for (int i = 0; i < dark.Length; i++) { dark[i] = ambient; amt[i] = 0f; }
            float fl = Mathf.Sin(sim.time * 9f) * 1.5f;
            var P = sim.P;
            LightAt(P.x - camX, P.y - camY, sim.LightR + fl);
            LightAt(World.BX - camX, World.BY - camY, (96f + fl) * (sim.BeaconR / Data.BeaconRadius), Cyan, 0.8f);

            int lightLv = sim.P.lvl[(int)Track.Light];
            float senseR = lightLv >= 3 ? sim.LightR * 2.2f : lightLv >= 2 ? sim.LightR * 1.6f : 0f;   // Lv3: 1.6배, Lv4: 2.2배
            int tx0 = Mathf.FloorToInt(camX / (float)World.TS) - 8, tx1 = Mathf.FloorToInt((camX + w) / (float)World.TS) + 8;
            int ty0 = Mathf.FloorToInt(camY / (float)World.TS) - 8, ty1 = Mathf.FloorToInt((camY + H) / (float)World.TS) + 8;
            var wd = sim.world;
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    if (!World.InBounds(tx, ty)) continue;
                    int i = World.Idx(tx, ty); var t = (Tile)wd.tiles[i];
                    if (t == Tile.Lamp) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 6 - camY, 52f + fl, Warm, 0.9f);
                    else if (t == Tile.Vent) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 8 - camY, 30f, Cyan, 0.7f);
                    else if (t == Tile.Crystal) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 8 - camY, Data.Tiles[(int)t].glow + fl * 0.4f, Teal, 0.9f);
                    else if (t == Tile.Chest) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 8 - camY, 22f, Gold, 0.9f);
                    else if (wd.deco[i] == 3) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 12 - camY, 11f, Violet, 0.5f);
                    else if (wd.deco[i] == World.DecoFungus) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 8 - camY, 14f, Teal, 0.8f);
                    else if (senseR > 0f && (t == Tile.Scrap || t == Tile.Brass))
                    {
                        // 랜턴 Lv3 이상: 시야 밖의 고철·황동 광맥이 희미하게 비친다(랜턴 트랙의 고유 효용)
                        float ddx = tx * World.TS + 8 - P.x, ddy = ty * World.TS + 8 - P.y;
                        if (ddx * ddx + ddy * ddy < senseR * senseR) LightAt(tx * World.TS + 8 - camX, ty * World.TS + 8 - camY, 7f, t == Tile.Scrap ? Warm : Gold, 0.9f);
                    }
                }
            foreach (var e in sim.enemies)
            {
                if (Sim.IsMini(e.type)) LightAt(e.x - camX, e.y - camY, 30f + fl, Crimson, 0.9f);   // 미니보스는 붉게 빛나 소굴이 멀리서 보인다
                if (e.elite) LightAt(e.x - camX, e.y - camY, 17f, Gold, 0.9f);   // 정예는 금빛으로 빛나 멀리서도 보인다
                if (e.type == EnemyType.Jelly) LightAt(e.x - camX, e.y - camY, 15f, Violet, 0.8f);
                else if (e.type == EnemyType.Angler) LightAt(e.x - camX + (e.faceX > 0 ? 8 : -8), e.y - camY - 9, 22f + fl, Gold, 0.8f);
                else if (e.type == EnemyType.Eel) LightAt(e.x - camX, e.y - camY, 18f + fl, Gold, 0.6f);
                else if (e.type == EnemyType.Wraith) LightAt(e.x - camX, e.y - camY, 13f, Cyan, 0.8f);
            }
            var b0 = sim.boss;
            float dAltar = Mathf.Sqrt((P.x - World.AX) * (P.x - World.AX) + (P.y - World.AY) * (P.y - World.AY));
            if (b0.active || (!b0.dead && dAltar < 200))
                LightAt((b0.active ? b0.x : World.AX) - camX, (b0.active ? b0.y : World.AY) - camY, 60f + fl * 2, Crimson, 0.9f);
            foreach (var bu in sim.bullets) LightAt(bu.x - camX, bu.y - camY, 12f, Crimson, 0.8f);

            // 위→아래 행 순서를 텍스처(아래→위)로 뒤집어 기록. 색은 어둠 색에서 광원 색 쪽으로 amt 만큼 섞는다.
            for (int y = 0; y < H; y++)
            {
                int dst = (H - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    int s = y * w + x; float k = amt[s]; var hc = hue[s];
                    byte r = (byte)Mathf.RoundToInt(5 + (hc.r - 5) * k), g = (byte)Mathf.RoundToInt(10 + (hc.g - 10) * k), bl = (byte)Mathf.RoundToInt(22 + (hc.b - 22) * k);
                    pixels[dst + x] = new Color32(r, g, bl, (byte)Mathf.RoundToInt(dark[s] * 255f));
                }
            }
            Texture.SetPixels32(pixels); Texture.Apply(false);
        }
    }
}
