using System;
using UnityEngine;
using UnityEngine.UI;

namespace Abyss
{
    /// <summary>
    /// 새 게임 인트로 컷신 (약 27초, 아무 키/터치로 건너뛰기). 코드로 연출하는 모션 그래픽.
    /// 박동하는 비콘 → 도시 붕괴(쇼크웨이브·붉은 섬광) → 빛을 먹는 '눈'이 깨어남 → 비콘이 꺼져 감 →
    /// 잠수부 급강하와 정체불명의 목소리 → 비콘 폭발적 점화와 타이틀 슬램.
    /// 끝나면 onDone 을 한 번 부른다.
    /// </summary>
    public sealed class IntroSequence : MonoBehaviour
    {
        struct Line { public float a, b; public string who, text; public bool red; }

        // 게임 첫 대사("…연결됐어요. 살아 있는 등대지기가 있었군요.")로 이어지도록 아라는 여기서 '???' 로만 등장한다.
        static readonly Line[] Lines =
        {
            new Line { a = 0.7f,  b = 3.4f,  text = "그날 밤," },
            new Line { a = 3.8f,  b = 7.8f,  text = "도시는 소리 없이 가라앉았다." },
            new Line { a = 8.4f,  b = 12.0f, text = "빛을 먹는 ‘눈’이 깨어났고,", red = true },
            new Line { a = 12.4f, b = 16.0f, text = "마지막 불빛, 항구의 비콘만이 남았다." },
            new Line { a = 16.8f, b = 20.4f, who = "???", text = "…신호가… 잡혔어요. 누군가, 내려오고 있어요." },
            new Line { a = 20.8f, b = 23.7f, who = "???", text = "출력 12%… 저는 아직, 꺼지지 않았어요." },
        };
        const float Total = 27.5f, FadeOut = 0.8f, TitleT = 24.0f;

        // 충격(카메라 펀치·흔들림) 시각과 세기
        static readonly float[] HitT = { 0.7f, 5.5f, 8.5f, 19.0f, TitleT };
        static readonly float[] HitW = { 0.35f, 1.0f, 0.8f, 0.45f, 1.2f };
        // 효과음 (시각, 종류)
        static readonly float[] SfxT = { 0.7f, 5.5f, 5.55f, 8.5f, 19.0f, 21.0f, TitleT, TitleT + 0.05f };
        static readonly Sfx[] SfxK = { Sfx.Chest, Sfx.Boss, Sfx.Break, Sfx.Boss, Sfx.Chest, Sfx.Craft, Sfx.Boss, Sfx.Craft };

        Action onDone; Action<Sfx> sfx; float t; bool ending; float endT; int sfxIdx;
        RectTransform world, barT, barB; CanvasGroup cg;
        Image bg, flash, diver, lamp, beacon, bGlow, eyeBody, eyeIris, eyeGlow;
        Image[] bubbles, towers, windows, rings, bursts, streams;
        float[] bx, by, bs, tx, th, wx, wy, wph;
        Text lineTxt, whoTxt, titleTxt, subTxt, skipTxt;
        Sprite[,] sheet;
        int fs;
        static readonly float[] RingT = { 5.5f, 19.0f, TitleT };

        public static IntroSequence Play(Transform canvas, Action<Sfx> sfx, Action done)
        {
            var go = new GameObject("Intro", typeof(RectTransform)); go.transform.SetParent(canvas, false);
            var c = go.AddComponent<IntroSequence>(); c.onDone = done; c.sfx = sfx; c.cg = go.AddComponent<CanvasGroup>();
            c.Build((RectTransform)go.transform);
            go.transform.SetAsLastSibling();
            return c;
        }

        internal static Sprite MakeGlow()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f; float a = Mathf.Clamp01(1f - d); a *= a;
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }

        void Build(RectTransform r)
        {
            UiKit.Stretch(r);
            fs = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 20f));
            var white = PixelArt.WhitePixel(); var glow = MakeGlow();
            // 카메라 흔들림에 가장자리가 보이지 않게 월드 컨테이너에 여백을 둔다
            world = UiKit.Rect("world", r); { float m = -Screen.height * 0.06f; UiKit.Stretch(world, m, m, m, m); }
            bg = UiKit.Img(world, white, Color.black, true, "bg"); UiKit.Stretch(bg.rectTransform);

            var rng = new System.Random(7);
            int nT = 14; towers = new Image[nT]; tx = new float[nT]; th = new float[nT];
            for (int i = 0; i < nT; i++)
            {
                tx[i] = (i + 0.5f) / nT; th[i] = 0.12f + (float)rng.NextDouble() * 0.30f;
                towers[i] = UiKit.Img(world, white, new Color(0.05f, 0.11f, 0.2f, 0f), false, "tower");
            }
            int nW = 52; windows = new Image[nW]; wx = new float[nW]; wy = new float[nW]; wph = new float[nW];
            for (int i = 0; i < nW; i++)
            {
                int k = rng.Next(nT); wx[i] = tx[k] + ((float)rng.NextDouble() - 0.5f) * 0.03f; wy[i] = 0.02f + (float)rng.NextDouble() * th[k] * 0.9f; wph[i] = (float)rng.NextDouble() * 20f;
                windows[i] = UiKit.Img(world, white, new Color(1f, 0.85f, 0.45f, 0f), false, "window");
            }
            int nB = 34; bubbles = new Image[nB]; bx = new float[nB]; by = new float[nB]; bs = new float[nB];
            for (int i = 0; i < nB; i++)
            {
                bx[i] = (float)rng.NextDouble(); by[i] = (float)rng.NextDouble(); bs[i] = 0.02f + (float)rng.NextDouble() * 0.05f;
                bubbles[i] = UiKit.Img(world, white, new Color(0.7f, 0.9f, 1f, 0f), false, "bubble");
            }

            // 눈 (보스 스프라이트): 붉은 광채 + 안구 + 홍채
            eyeGlow = UiKit.Img(world, glow, new Color(1f, 0.15f, 0.1f, 0f), false, "eyeGlow");
            eyeBody = UiKit.Img(world, PixelArt.LoadBoss("boss_body"), new Color(1, 1, 1, 0f), false, "eye"); eyeBody.preserveAspect = true;
            eyeIris = UiKit.Img(world, PixelArt.LoadBoss("boss_iris"), new Color(1, 1, 1, 0f), false, "iris"); eyeIris.preserveAspect = true;
            streams = new Image[16];
            for (int i = 0; i < streams.Length; i++) streams[i] = UiKit.Img(world, white, new Color(0.7f, 0.95f, 1f, 0f), false, "stream");

            // 비콘 + 광채, 소나/쇼크웨이브 링
            bGlow = UiKit.Img(world, glow, new Color(0.55f, 0.9f, 1f, 0f), false, "beaconGlow");
            beacon = UiKit.Img(world, PixelArt.Beacon(), new Color(1, 1, 1, 0f), false, "beacon"); beacon.preserveAspect = true;
            var ringSp = UiKit.BoxSprite(32, 2, new Color32(0, 0, 0, 0), new Color32(160, 235, 255, 255));
            rings = new Image[RingT.Length];
            for (int i = 0; i < rings.Length; i++) { rings[i] = UiKit.Img(world, ringSp, new Color(1, 1, 1, 0f), false, "ring"); rings[i].type = Image.Type.Sliced; }

            // 잠수부 + 램프 광채
            sheet = PixelArt.DiverSheet();
            lamp = UiKit.Img(world, glow, new Color(1f, 0.92f, 0.55f, 0f), false, "lamp");
            diver = UiKit.Img(world, sheet != null ? sheet[0, 2] : PixelArt.Player(), new Color(1, 1, 1, 0f), false, "diver"); diver.preserveAspect = true;

            // 타이틀 순간 사방으로 퍼지는 빛줄기
            bursts = new Image[12];
            for (int i = 0; i < bursts.Length; i++) bursts[i] = UiKit.Img(world, white, new Color(0.85f, 0.96f, 1f, 0f), false, "burst");

            barT = UiKit.Img(r, white, Color.black, false, "barT").rectTransform; barB = UiKit.Img(r, white, Color.black, false, "barB").rectTransform;

            whoTxt = UiKit.Txt(r, "", UiKit.Font14, fs, new Color32(159, 232, 255, 255), TextAnchor.MiddleCenter, "who"); UiKit.Outline(whoTxt, 2);
            lineTxt = UiKit.Txt(r, "", UiKit.Font14, fs + 6, new Color32(232, 246, 255, 255), TextAnchor.MiddleCenter, "line"); UiKit.Outline(lineTxt, 2);
            titleTxt = UiKit.Txt(r, "DEEP LIGHT", UiKit.Font14, fs * 3 + 8, new Color32(159, 232, 255, 255), TextAnchor.MiddleCenter, "title"); UiKit.Outline(titleTxt, 3);
            subTxt = UiKit.Txt(r, "딥 라이트  ·  숨이 다하기 전에, 빛을 되살려라", UiKit.Font14, fs, new Color32(255, 224, 138, 255), TextAnchor.MiddleCenter, "sub"); UiKit.Outline(subTxt, 2);
            UiKit.Place(whoTxt.rectTransform, 0.5f, 0.17f, 0.5f, 0.5f, 0, fs * 1.5f, Screen.width, fs + 8);
            UiKit.Place(lineTxt.rectTransform, 0.5f, 0.17f, 0.5f, 0.5f, 0, 0, Screen.width, fs + 14);
            UiKit.Place(titleTxt.rectTransform, 0.5f, 0.56f, 0.5f, 0.5f, 0, 0, Screen.width, fs * 3 + 20);
            UiKit.Place(subTxt.rectTransform, 0.5f, 0.56f, 0.5f, 0.5f, 0, -fs * 2.6f, Screen.width, fs + 8);
            titleTxt.color = Clear(titleTxt.color); subTxt.color = Clear(subTxt.color);

            flash = UiKit.Img(r, white, new Color(1, 1, 1, 0), false, "flash"); UiKit.Stretch(flash.rectTransform);
            skipTxt = UiKit.Txt(r, "건너뛰기: 아무 키", UiKit.Font11, Mathf.Max(11, fs - 6), new Color32(169, 192, 210, 200), TextAnchor.LowerRight, "skip");
            UiKit.Place(skipTxt.rectTransform, 1, 0, 1, 0, -fs, fs * 0.6f, Screen.width * 0.5f, fs + 4);
        }

        static Color Clear(Color c) { c.a = 0; return c; }
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        static float Win(float t, float a, float b, float fade = 0.6f) { return Smooth((t - a) / fade) * (1 - Smooth((t - (b - fade)) / fade)); }
        static float Pw(float t, float[] ts, float[] vs)
        {
            if (t <= ts[0]) return vs[0];
            for (int i = 1; i < ts.Length; i++) if (t <= ts[i]) return Mathf.Lerp(vs[i - 1], vs[i], (t - ts[i - 1]) / (ts[i] - ts[i - 1]));
            return vs[vs.Length - 1];
        }
        void Put(Image im, float fx, float fy, float w, float h, float ox = 0, float oy = 0) { UiKit.Place(im.rectTransform, fx, fy, 0.5f, 0.5f, ox, oy, w, h); }

        void Update()
        {
            // 연출 코드에서 예외가 나도 게임이 인트로에 갇히지 않게 한다
            try { Tick(); } catch (Exception e) { Debug.LogException(e); Done(); }
        }

        void Tick()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            t += dt;
            if (!ending && t > 0.4f && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) Finish();
            while (!ending && sfxIdx < SfxT.Length && t >= SfxT[sfxIdx]) { if (sfx != null) sfx(SfxK[sfxIdx]); sfxIdx++; }
            if (ending) { endT += dt; Draw(); if (endT >= FadeOut) Done(); return; }
            if (t >= Total) { Finish(); return; }
            Draw();
        }

        void Finish() { ending = true; endT = 0; }

        void Done()
        {
            var cb = onDone; onDone = null; Destroy(gameObject); if (cb != null) cb();
        }

        void Draw()
        {
            float W = Screen.width, H = Screen.height;
            cg.alpha = ending ? 1f - Smooth(endT / FadeOut) : 1f;

            // ---- 충격량: 각 타격 직후 급격히 커졌다가 사라진다 ----
            float impact = 0;
            for (int i = 0; i < HitT.Length; i++) if (t >= HitT[i]) impact += HitW[i] * Mathf.Exp(-(t - HitT[i]) * 4.5f);

            // ---- 배경 밝기 ----
            float bgLv = Pw(t, new[] { 0f, 3f, 5.4f, 8f, 16f, 24f, 26f }, new[] { 0f, 0.55f, 0.6f, 0.12f, 0.3f, 0.6f, 0.35f });
            bg.color = Color.Lerp(new Color(0, 0, 0.01f), new Color(0.02f, 0.09f, 0.16f), bgLv);

            // ---- 카메라 ----
            float zoom = 1f + 0.16f * Smooth(t / 24f) + 0.07f * impact;
            float sh = impact * H * 0.018f;
            world.localScale = new Vector3(zoom, zoom, 1);
            world.anchoredPosition = new Vector2(Mathf.Sin(t * 0.45f) * W * 0.006f + Mathf.Sin(t * 61f) * sh, Mathf.Cos(t * 0.38f) * H * 0.005f + Mathf.Cos(t * 53f) * sh);

            // ---- 비콘 밝기 곡선 (심장박동 → 환함 → 빨려 나감 → 꺼져 감 → 응답 → 폭발) ----
            float B = Pw(t, new[] { 0f, 0.7f, 1.4f, 3.5f, 8.2f, 13.5f, 16.5f, 18.6f, 21.5f, 23.6f, TitleT, 26f },
                            new[] { 0f, 0.0f, 0.6f, 0.9f, 0.9f, 0.2f, 0.07f, 0.45f, 0.6f, 1.2f, 1.7f, 1.0f });
            if (t < 3.5f) B *= 1f + 0.35f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 5.2f)), 6f);
            if (t > 12.5f && t < 18.2f) B *= (Mathf.Sin(t * 23f) * Mathf.Sin(t * 9.7f) > 0.15f ? 1f : 0.3f);   // 꺼져 가며 깜박
            float bfy = 0.27f, bh = H * 0.22f;
            Put(beacon, 0.5f, bfy, bh * 16f / 28f, bh); beacon.color = new Color(1, 1, 1, Mathf.Clamp01(B * 1.2f));
            beacon.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 6f) * Mathf.Clamp01(B));
            Put(bGlow, 0.5f, bfy, H * (0.45f + 0.9f * B), H * (0.45f + 0.9f * B)); bGlow.color = new Color(0.55f, 0.9f, 1f, Mathf.Clamp01(0.5f * B));

            // ---- 도시: 3초에 나타나 반짝이다가 5.5초에 붕괴 ----
            float cityA = Smooth((t - 2.6f) / 1.2f);
            float sink = H * 0.5f * Smooth((t - 5.5f) / 2.4f);
            for (int i = 0; i < towers.Length; i++)
            {
                float w = W / towers.Length * 0.92f, h = th[i] * H;
                float jit = (t > 5.5f && t < 6.8f) ? Mathf.Sin(t * 70f + i) * W * 0.004f : 0f;
                UiKit.Place(towers[i].rectTransform, 0, 0, 0.5f, 0, tx[i] * W + jit, -sink * (0.7f + 0.3f * ((i * 37) % 10) / 10f), w, h);
                towers[i].color = new Color(0.05f, 0.11f, 0.2f, cityA);
            }
            for (int i = 0; i < windows.Length; i++)
            {
                // 붕괴 후 창문이 왼쪽에서 오른쪽으로 파도처럼 꺼진다
                float off = 5.5f + wx[i] * 1.4f + wph[i] * 0.01f; float on = 1f - Smooth((t - off) / 0.25f);
                float tw = 0.75f + 0.25f * Mathf.Sin(t * 6f + wph[i]);
                UiKit.Place(windows[i].rectTransform, 0, 0, 0.5f, 0.5f, wx[i] * W, wy[i] * H - sink, Mathf.Max(3, W * 0.006f), Mathf.Max(3, W * 0.006f));
                windows[i].color = new Color(1f, 0.85f, 0.45f, cityA * on * tw);
            }

            // ---- 물방울 ----
            float bubA = Smooth((t - 1f) / 2f) * (1f + impact);
            for (int i = 0; i < bubbles.Length; i++)
            {
                float y = Mathf.Repeat(by[i] + t * bs[i] * (1f + impact), 1.1f) - 0.05f; float x = bx[i] + Mathf.Sin(t * 0.8f + i) * 0.01f;
                float s = Mathf.Max(3, W * 0.005f) * (1 + (i % 3));
                UiKit.Place(bubbles[i].rectTransform, 0, 0, 0.5f, 0.5f, x * W, y * H, s, s);
                bubbles[i].color = new Color(0.7f, 0.9f, 1f, Mathf.Clamp01(0.35f * bubA));
            }

            // ---- 눈: 8.5초에 눈꺼풀이 열리며 깨어나 비콘을 응시하고, 14초에 감김 ----
            float eyeOpen = Smooth((t - 8.5f) / 1.8f) * (1f - Smooth((t - 13.6f) / 0.7f));
            float eyeA = Smooth((t - 8.4f) / 0.6f) * (1f - Smooth((t - 14f) / 0.8f));
            float E = H * 0.62f, efy = 0.66f;
            Put(eyeGlow, 0.5f, efy, E * 2.4f, E * 2.4f * Mathf.Max(0.15f, eyeOpen)); eyeGlow.color = new Color(1f, 0.15f, 0.1f, 0.5f * eyeA * (0.7f + 0.3f * Mathf.Sin(t * 3f)));
            Put(eyeBody, 0.5f, efy, E, E * Mathf.Max(0.02f, eyeOpen)); eyeBody.color = new Color(1, 1, 1, eyeA);
            Put(eyeIris, 0.5f, efy, E * 0.42f, E * 0.42f * Mathf.Max(0.02f, eyeOpen), Mathf.Sin(t * 0.9f) * E * 0.03f, -E * 0.13f * eyeOpen);
            eyeIris.color = new Color(1, 1, 1, eyeA);

            // 비콘의 빛이 눈으로 빨려 올라가는 입자
            float stA = Win(t, 9f, 13.8f, 1f);
            for (int i = 0; i < streams.Length; i++)
            {
                float f = Mathf.Repeat(t * 0.38f + i / (float)streams.Length, 1f); float e = f * f;
                float sx = (0.5f + Mathf.Sin(i * 2.1f + t) * 0.06f * (1 - f)) * W, sy = Mathf.Lerp(bfy + 0.05f, efy - 0.1f, e) * H;
                float s = Mathf.Max(3, W * 0.005f) * (1 + (i % 2));
                UiKit.Place(streams[i].rectTransform, 0, 0, 0.5f, 0.5f, sx, sy, s, s);
                streams[i].color = new Color(0.7f, 0.95f, 1f, Mathf.Sin(f * Mathf.PI) * stA);
            }

            // ---- 링: 붕괴 / 비콘 응답 / 타이틀 때 퍼져 나가는 파동 ----
            for (int i = 0; i < rings.Length; i++)
            {
                float k = (t - RingT[i]) / 1.3f;
                if (k < 0 || k > 1) { rings[i].color = new Color(1, 1, 1, 0); continue; }
                float sz = Mathf.Lerp(bh * 0.4f, H * 1.7f, 1f - (1f - k) * (1f - k));
                Put(rings[i], 0.5f, bfy, sz, sz * 0.55f);
                rings[i].color = new Color(i == 0 ? 1f : 0.7f, i == 0 ? 0.5f : 0.95f, i == 0 ? 0.45f : 1f, 0.8f * (1 - k) * (1 - k));
            }

            // ---- 잠수부: 16.6초에 위에서 급강하, 램프 불빛 ----
            float dIn = Smooth((t - 16.6f) / 2.0f);
            float dy = Mathf.Lerp(1.2f, 0.56f, dIn);
            float dsz = H * 0.2f;
            int frame = 2 + (int)(t * 8f) % 6;
            if (sheet != null) diver.sprite = sheet[0, dIn < 1f ? frame : (int)(t * 1.5f) % 2];
            float dAlpha = Smooth((t - 16.5f) / 0.3f);
            Put(diver, 0.5f, dy, dsz, dsz, Mathf.Sin(t * 1.2f) * W * 0.01f, Mathf.Sin(t * 2.2f) * H * 0.008f);
            diver.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 1.6f) * 5f);
            diver.color = new Color(1, 1, 1, dAlpha);
            Put(lamp, 0.5f, dy, dsz * 3.2f, dsz * 3.2f, 0, -dsz * 0.6f);
            lamp.color = new Color(1f, 0.92f, 0.55f, 0.55f * dAlpha * (0.85f + 0.15f * Mathf.Sin(t * 9f)));

            // ---- 타이틀 순간: 사방으로 퍼지는 빛줄기 ----
            float bk = (t - TitleT) / 1.5f;
            for (int i = 0; i < bursts.Length; i++)
            {
                if (bk < 0 || bk > 1) { bursts[i].color = new Color(1, 1, 1, 0); continue; }
                float len = H * 1.3f * (1f - (1f - bk) * (1f - bk));
                UiKit.Place(bursts[i].rectTransform, 0.5f, bfy, 0.5f, 0f, 0, 0, Mathf.Max(3, W * 0.004f), len);
                bursts[i].rectTransform.localEulerAngles = new Vector3(0, 0, i * 30f + 7f);
                bursts[i].color = new Color(0.85f, 0.96f, 1f, 0.7f * (1 - bk));
            }

            // ---- 섬광: 붕괴는 붉게, 타이틀은 하얗게 ----
            float fk;
            if (t >= TitleT) { fk = Mathf.Clamp01(1f - (t - TitleT) / 0.6f); flash.color = new Color(1, 1, 1, 0.95f * fk * fk); }
            else if (t >= 5.5f && t < 6.2f) { fk = 1f - (t - 5.5f) / 0.7f; flash.color = new Color(1f, 0.15f, 0.1f, 0.5f * fk); }
            else flash.color = new Color(1, 1, 1, 0);

            // ---- 레터박스 ----
            float barH = H * 0.09f * Smooth(t / 1.0f) * (1f - Smooth((t - (TitleT + 1.8f)) / 1.0f));
            UiKit.Place(barT, 0.5f, 1f, 0.5f, 1f, 0, 0, W + 4, barH); UiKit.Place(barB, 0.5f, 0f, 0.5f, 0f, 0, 0, W + 4, barH);

            // ---- 자막: 쿵 하고 커졌다가 자리 잡고, 빠르게 타자 ----
            string text = ""; string who = ""; float la = 0, st = 0; bool red = false;
            foreach (var l in Lines) { float a = Win(t, l.a, l.b, 0.4f); if (a > la) { la = a; text = l.text; who = l.who ?? ""; st = t - l.a; red = l.red; } }
            int shown = Mathf.Clamp((int)(st * 28f), 0, text.Length);
            lineTxt.text = text.Substring(0, shown);
            lineTxt.color = red ? new Color(1f, 0.72f, 0.7f, la) : new Color(0.91f, 0.96f, 1f, la);
            float slam = 1f + 0.45f * (1f - Smooth(st / 0.22f));
            lineTxt.rectTransform.localScale = Vector3.one * slam;
            whoTxt.text = who; whoTxt.color = new Color(0.62f, 0.91f, 1f, who.Length > 0 ? la : 0);

            // ---- 타이틀: 화면을 때리듯 커다랗게 내려앉음 ----
            float tk = (t - TitleT) / 0.35f;
            float tsc = Mathf.Lerp(3.2f, 1f, Mathf.Pow(Smooth(tk), 0.5f));
            titleTxt.rectTransform.localScale = Vector3.one * (tk < 0 ? 3.2f : tsc);
            titleTxt.color = new Color(0.62f, 0.91f, 1f, Mathf.Clamp01(tk * 3f));
            subTxt.color = new Color(1f, 0.88f, 0.54f, Smooth((t - TitleT - 0.7f) / 0.8f));
            skipTxt.color = new Color(0.66f, 0.75f, 0.82f, 0.8f * Smooth((t - 1f) / 1f));
        }
    }
}
