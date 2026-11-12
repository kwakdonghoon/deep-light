using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss
{
    /// <summary>
    /// 타이틀 화면의 살아 움직이는 배경. 정적인 그림이 아니라 여러 겹(레이어)으로 나눠 각각 다르게 움직인다:
    /// 반짝이는 창문, 일렁이는 해초, 깜박이는 붉은 '눈', 숨 쉬는 비콘 광채, 떠다니는 로고와 반짝임,
    /// 그리고 여러 마리 해파리 · 물고기 떼 · 장어 · 게 · 성게, 잠수부와 아귀의 추격 개그.
    /// 좌표는 원본 그림(428x180) 픽셀 기준으로 두고 화면에 맞춰 환산한다.
    /// </summary>
    public sealed class TitleScene
    {
        const float IW = 428f, IH = 180f;
        RectTransform root; float sc, lw, lh, W, H;

        Image bg, winA, winB, kelpL, kelpR, eye, beacon, logo, logoGlow, beaconGlow, beaconCore;
        Image[] rays = new Image[3], bubbles = new Image[26], sparkH = new Image[6], sparkV = new Image[6];
        Image[] jelly = new Image[5], fish = new Image[9];
        Image eel, crab, urchinA, urchinB, diver, angler;
        Text mark;
        float[] bx, by, bs, jx, jy, jph, jsz;
        Sprite[] jellyS, fishS = new Sprite[1]; Sprite[,] sheet;

        static Sprite Spr(string name) { return Resources.Load<Sprite>("Art/Title/" + name); }

        Image Layer(string name, Color c)
        {
            var im = UiKit.Img(root, Spr(name), c, false, name); im.preserveAspect = false; return im;
        }
        Image Pic(string name, Sprite s, Color c) { var im = UiKit.Img(root, s, c, false, name); im.preserveAspect = true; return im; }

        public void Build(RectTransform parent)
        {
            root = parent;
            var white = PixelArt.WhitePixel(); var glow = IntroSequence.MakeGlow();
            bg = Layer("title_bg", Color.white);
            var rg = new System.Random(11);
            for (int i = 0; i < rays.Length; i++) rays[i] = UiKit.Img(root, white, new Color(0.7f, 0.92f, 1f, 0f), false, "ray");
            eye = Layer("title_eye", Color.white);
            winA = Layer("title_win_a", Color.white); winB = Layer("title_win_b", Color.white);
            logoGlow = UiKit.Img(root, glow, new Color(0.45f, 0.85f, 1f, 0f), false, "logoGlow");
            beaconGlow = UiKit.Img(root, glow, new Color(0.55f, 0.9f, 1f, 0f), false, "beaconGlow");
            beacon = Layer("title_beacon", Color.white);
            beaconCore = UiKit.Img(root, glow, new Color(0.95f, 1f, 1f, 0f), false, "beaconCore");

            // 출연진: 해파리 5, 물고기 떼 9, 장어, 아귀, 잠수부, 게, 성게 2
            jellyS = PixelArt.Jelly();
            jx = new float[5]; jy = new float[5]; jph = new float[5]; jsz = new float[5];
            for (int i = 0; i < jelly.Length; i++)
            {
                jx[i] = 0.1f + (float)rg.NextDouble() * 0.8f; jy[i] = 0.34f + (float)rg.NextDouble() * 0.30f; jph[i] = (float)rg.NextDouble() * 20f; jsz[i] = 0.06f + (float)rg.NextDouble() * 0.06f;
                jelly[i] = Pic("jelly", jellyS != null && jellyS.Length > 0 ? jellyS[0] : null, new Color(1, 1, 1, 0.55f + 0.35f * (float)rg.NextDouble()));
            }
            fishS[0] = MakeFish();
            for (int i = 0; i < fish.Length; i++) fish[i] = Pic("fish", fishS[0], new Color(0.62f, 0.86f, 1f, 0.9f));
            eel = Pic("eel", PixelArt.Eel(), Color.white);
            angler = Pic("angler", PixelArt.Angler(), Color.white);
            sheet = PixelArt.DiverSheet();
            diver = Pic("diver", sheet != null ? sheet[2, 2] : PixelArt.Player(), Color.white);
            crab = Pic("crab", PixelArt.Crab(), Color.white);
            urchinA = Pic("urchinA", PixelArt.Urchin(), Color.white); urchinB = Pic("urchinB", PixelArt.Urchin(), Color.white);
            kelpL = Layer("title_kelp_l", Color.white); kelpR = Layer("title_kelp_r", Color.white);

            bx = new float[bubbles.Length]; by = new float[bubbles.Length]; bs = new float[bubbles.Length];
            for (int i = 0; i < bubbles.Length; i++)
            {
                bx[i] = (float)rg.NextDouble(); by[i] = (float)rg.NextDouble(); bs[i] = 0.025f + (float)rg.NextDouble() * 0.05f;
                bubbles[i] = UiKit.Img(root, white, new Color(0.75f, 0.93f, 1f, 0f), false, "bubble");
            }
            logo = Layer("title_logo", Color.white);
            for (int i = 0; i < sparkH.Length; i++)
            {
                sparkH[i] = UiKit.Img(root, white, new Color(1, 1, 1, 0f), false, "sparkH"); sparkV[i] = UiKit.Img(root, white, new Color(1, 1, 1, 0f), false, "sparkV");
            }
            mark = UiKit.Txt(root, "", UiKit.Font14, 20, new Color32(255, 224, 138, 255), TextAnchor.MiddleCenter, "mark"); UiKit.Outline(mark, 2);
        }

        // 작은 물고기 (오른쪽을 본다)
        static Sprite MakeFish()
        {
            var pal = new Dictionary<char, Color32> { { 'x', new Color32(127, 211, 245, 255) }, { 'X', new Color32(79, 150, 200, 255) }, { 'e', new Color32(10, 22, 38, 255) } };
            return PixelArt.FromRows(new[] { "...xxx..X", ".xxxxxxXX", "xxxxexxXX", ".xxxxxxXX", "...xxx..X" }, pal).ToSprite();
        }

        static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        // 원본 픽셀 (x, y; y 는 위에서 아래) → 월드 좌표(중앙 기준)
        float PX(float x) { return (x / IW - 0.5f) * lw; }
        float PY(float y) { return (0.5f - y / IH) * lh; }

        void Lay(Image im, float pivX = 0.5f, float pivY = 0.5f, float scaleX = 1f, float scaleY = 1f, float ox = 0, float oy = 0)
        {
            var rt = im.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(pivX, pivY); rt.sizeDelta = new Vector2(lw, lh);
            rt.anchoredPosition = new Vector2((pivX - 0.5f) * lw + ox, (pivY - 0.5f) * lh + oy); rt.localScale = new Vector3(scaleX, scaleY, 1);
        }
        void At(Image im, float x, float y, float w, float h, float sx = 1f, float rot = 0f)
        {
            UiKit.Place(im.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, x, y, w, h); im.rectTransform.localScale = new Vector3(sx, 1, 1); im.rectTransform.localEulerAngles = new Vector3(0, 0, rot);
        }
        static void Alpha(Image im, float a) { var c = im.color; c.a = a; im.color = c; }

        public void Tick(float t, float w, float h, int fontSize)
        {
            W = w; H = h; sc = Mathf.Max(W / IW, H / IH); lw = IW * sc; lh = IH * sc;

            Lay(bg);
            // 창문: 두 무리가 서로 다른 박자로 깜박인다
            Lay(winA); Alpha(winA, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 0.9f) * Mathf.Sin(t * 0.37f + 1f)));
            Lay(winB); Alpha(winB, 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 0.7f + 2f) * Mathf.Sin(t * 0.29f)));
            // 해초: 바닥을 축으로 가로로 일렁인다
            Lay(kelpL, 0.21f, 0.1f, 1f + 0.014f * Mathf.Sin(t * 1.1f), 1f); Lay(kelpR, 0.8f, 0.1f, 1f + 0.014f * Mathf.Sin(t * 0.9f + 1.5f), 1f);
            // 붉은 눈: 맥박처럼 밝아졌다 어두워지고, 가끔 눈을 깜박인다
            float blink = Mathf.Clamp01(1f - Mathf.Abs((t % 9f) - 0.15f) / 0.15f);
            Lay(eye, 392f / IW, 1f - 50f / IH, 1f, 1f - 0.9f * blink); Alpha(eye, 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 1.3f)));

            // 비콘: 숨 쉬는 광채와 가끔 한 번씩 꺼질 듯 깜박임
            float pulse = 0.6f + 0.4f * Mathf.Sin(t * 2.4f) * Mathf.Sin(t * 0.9f + 1f);
            // 깜빡임: 늘 미세하게 떨리고, 4.5초마다 '깜빡-깜빡' 두 번 꺼질 듯 점멸한 뒤 환하게 터진다. 13초마다는 오래 불안정하게 깜박인다.
            float ph = t % 4.5f;
            float dip = (ph < 0.10f || (ph > 0.26f && ph < 0.36f)) ? 0.2f : 1f;
            float flare = 1f + 0.5f * Mathf.Exp(-Mathf.Max(0f, ph - 0.36f) * 5f) * (ph > 0.36f ? 1f : 0f);
            float fl = (0.88f + 0.12f * Mathf.Sin(t * 17f) * Mathf.Sin(t * 7.3f)) * dip;
            if ((t % 13f) > 12.2f && Mathf.Sin(t * 40f) > 0f) fl *= 0.4f;
            float bgx = PX(214f), bgy = PY(110f);
            At(beaconGlow, bgx, bgy, H * (0.62f + 0.14f * pulse), H * (0.62f + 0.14f * pulse)); Alpha(beaconGlow, 0.4f * pulse * fl);
            Lay(beacon); beacon.color = new Color(fl, fl, fl, 1f);
            At(beaconCore, bgx, bgy, H * 0.12f * (0.9f + 0.2f * pulse) * flare, H * 0.12f * (0.9f + 0.2f * pulse) * flare); Alpha(beaconCore, Mathf.Clamp01(0.7f * fl * flare));

            // 로고: 천천히 위아래로 떠다니고 숨 쉬듯 밝아지며, 뒤에서 은은한 빛이 번진다
            float bob = Mathf.Sin(t * 0.9f) * lh * 0.004f, br = 1f + 0.08f * Mathf.Sin(t * 1.6f);
            At(logoGlow, PX(214f), PY(33f) + bob, lw * 0.7f, lh * 0.34f); Alpha(logoGlow, 0.22f + 0.1f * Mathf.Sin(t * 1.6f));
            Lay(logo, 0.5f, 0.5f, 1f, 1f, 0, bob); logo.color = new Color(Mathf.Min(1.2f, br), Mathf.Min(1.2f, br), Mathf.Min(1.2f, br), 1f);
            float[] spx = { 100f, 150f, 214f, 250f, 296f, 330f }, spy = { 30f, 24f, 22f, 38f, 26f, 34f };
            for (int i = 0; i < sparkH.Length; i++)
            {
                float a = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.15f + i * 2.1f)), 10f), z = Mathf.Max(3f, lh * 0.03f) * (0.4f + a);
                At(sparkH[i], PX(spx[i]), PY(spy[i]) + bob, z * 3f, Mathf.Max(2f, z * 0.35f)); At(sparkV[i], PX(spx[i]), PY(spy[i]) + bob, Mathf.Max(2f, z * 0.35f), z * 3f);
                sparkH[i].color = new Color(1, 1, 1, a); sparkV[i].color = new Color(1, 1, 1, a);
            }

            // 빛줄기
            for (int i = 0; i < rays.Length; i++)
            {
                float rx = (0.2f + i * 0.3f + Mathf.Sin(t * 0.3f + i * 1.9f) * 0.04f - 0.5f) * W;
                At(rays[i], rx, H * 0.5f + H * 0.1f - H * 0.7f, W * (0.05f + 0.02f * i), H * 1.4f, 1f, 12f + Mathf.Sin(t * 0.4f + i) * 4f);
                Alpha(rays[i], 0.05f * (0.6f + 0.4f * Mathf.Sin(t * 0.9f + i * 2f)));
            }
            // 물방울
            for (int i = 0; i < bubbles.Length; i++)
            {
                float y = Mathf.Repeat(by[i] + t * bs[i], 1.1f) - 0.05f, x = bx[i] + Mathf.Sin(t * 0.8f + i) * 0.012f;
                float s = Mathf.Max(3, W * 0.005f) * (1 + (i % 3));
                At(bubbles[i], (x - 0.5f) * W, (y - 0.5f) * H, s, s); Alpha(bubbles[i], 0.35f);
            }

            Fauna(t);
            Gag(t, fontSize);
        }

        void Fauna(float t)
        {
            // 해파리 5마리: 각자 다른 박자로 둥실 오르내리며 맥동한다
            for (int i = 0; i < jelly.Length; i++)
            {
                float x = (jx[i] - 0.5f) * W + Mathf.Sin(t * 0.22f + jph[i]) * W * 0.07f;
                float y = (jy[i] - 0.5f) * H + Mathf.Sin(t * 0.6f + jph[i]) * H * 0.04f;
                float pul = 1f + 0.13f * Mathf.Sin(t * 3.2f + jph[i]);
                float s = H * jsz[i] * 1.6f;
                if (jellyS != null && jellyS.Length > 1) jelly[i].sprite = jellyS[(int)(t * 2f + jph[i]) % jellyS.Length];
                UiKit.Place(jelly[i].rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, x, y, s, s); jelly[i].rectTransform.localScale = new Vector3(1f / pul * 0.5f + 0.5f, pul, 1);
            }
            // 물고기 떼: 20초마다 오른쪽에서 왼쪽으로 무리 지어 지나간다
            {
                float c = t % 20f, k = c / 10f; bool on = c < 10f;
                for (int i = 0; i < fish.Length; i++)
                {
                    if (!on) { Alpha(fish[i], 0); continue; }
                    float ox = ((i % 3) * 0.035f + (i / 3) * 0.018f), oy = ((i % 3) - 1) * 0.03f + ((i / 3) - 1) * 0.012f;
                    float x = (Mathf.Lerp(1.12f, -0.15f, k) + ox - 0.5f) * W, y = (0.58f + oy + Mathf.Sin(t * 2.2f + i) * 0.012f - 0.5f) * H;
                    float s = H * 0.035f;
                    At(fish[i], x, y, s * 9f / 5f, s, 1f, Mathf.Sin(t * 6f + i) * 6f);   // 물고기 그림은 기본이 왼쪽을 본다(꼬리가 오른쪽) → 왼쪽으로 갈 땐 뒤집지 않는다 Alpha(fish[i], 0.9f);
                }
            }
            // 장어: 26초마다 왼쪽에서 오른쪽으로 꿈틀대며 가로지른다 (8초 시점부터)
            {
                float c = (t - 8f) % 26f; bool on = t > 8f && c < 9f;
                if (!on) Alpha(eel, 0);
                else
                {
                    float k = c / 9f; float x = (Mathf.Lerp(-0.18f, 1.18f, k) - 0.5f) * W, y = (0.46f - 0.5f) * H + Mathf.Sin(t * 3f) * H * 0.02f;
                    At(eel, x, y, H * 0.2f, H * 0.2f, -1f, Mathf.Sin(t * 4.5f) * 7f); Alpha(eel, 1f);
                }
            }
            // 게: 오른쪽 바닥을 옆걸음으로 왔다 갔다 한다
            {
                float cx = 330f + 42f * Mathf.Sin(t * 0.33f), vel = Mathf.Cos(t * 0.33f);
                float hop = Mathf.Abs(Mathf.Sin(t * 7f)) * 1.8f;
                At(crab, PX(cx), PY(158f) + hop * sc, H * 0.075f, H * 0.075f, vel > 0 ? 1f : -1f, Mathf.Sin(t * 7f) * 3f);
                At(urchinA, PX(62f), PY(166f), H * 0.05f, H * 0.05f, 1f, Mathf.Sin(t * 0.8f) * 3f);
                At(urchinB, PX(398f), PY(170f), H * 0.045f, H * 0.045f, 1f, Mathf.Sin(t * 0.7f + 2f) * 3f);
            }
        }

        // 16초 반복 개그: 유유히 헤엄 → 아귀 등장, "!" 깜짝 점프 → 두 배 속도로 줄행랑 ("!!") → 아귀는 지쳐서 "…" 하고 돌아간다
        void Gag(float t, int fontSize)
        {
            const float L = 16f;
            float c = t % L, y0 = -H * 0.17f, dsz = H * 0.11f, asz = H * 0.13f;
            float dx, dy = 0, ax, dir; int frame = 2 + (int)(t * 9f) % 6; string text = ""; float mx = 0, my = 0, adir = 1f;
            if (c < 6f) { dx = Mathf.Lerp(-0.58f, 0.05f, c / 6f); dir = 1; ax = 0.8f; frame = 2 + (int)(t * 6f) % 6; }
            else if (c < 7.2f)
            {
                float k = (c - 6f) / 1.2f; dx = 0.05f; dir = 1; ax = Mathf.Lerp(0.65f, 0.4f, S(k));
                float hop = c - 6f; dy = hop < 0.5f ? Mathf.Abs(Mathf.Sin(hop / 0.5f * Mathf.PI)) * H * 0.07f : 0f;
                frame = hop < 0.5f ? 0 : 1; text = "!"; mx = dx * W; my = y0 + dsz * 0.9f + dy;
            }
            else if (c < 13f)
            {
                float k = S((c - 7.2f) / 5.8f); dx = Mathf.Lerp(0.05f, -0.72f, k); dir = -1; ax = Mathf.Lerp(0.4f, -0.32f, k);
                frame = 2 + (int)(t * 16f) % 6; if (c < 9f) { text = "!!"; mx = dx * W; my = y0 + dsz * 0.9f + Mathf.Sin(t * 40f) * 3f; }
            }
            else if (c < 14f) { dx = -0.8f; dir = -1; ax = -0.32f; text = "…"; mx = ax * W; my = y0 + asz * 0.9f; }
            else { dx = -0.8f; dir = -1; ax = Mathf.Lerp(-0.32f, 0.7f, S((c - 14f) / 2f)); adir = -1f; }

            if (sheet != null) diver.sprite = sheet[2, frame];
            At(diver, dx * W, y0 + dy + Mathf.Sin(t * 2f) * H * 0.004f, dsz, dsz, dir);
            At(angler, ax * W, y0 + dsz * 0.1f + Mathf.Sin(t * 3f) * H * 0.01f, asz, asz, adir);
            mark.text = text; mark.fontSize = fontSize + 6; mark.color = new Color(1f, 0.88f, 0.54f, text.Length > 0 ? 1f : 0f);
            UiKit.Place(mark.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, mx, my, 120, fontSize + 14);
        }
    }
}
