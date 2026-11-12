using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss
{
    /// <summary>
    /// HUD, 모달(제작/메뉴/도움말/시작), 터치 조작(플로팅 조이스틱·행동 버튼·월드 터치)과 키보드/마우스 입력을 SimInput 으로 변환한다.
    /// 웹 프로토타입의 index.html + game.js 의 UI/입력 부분 포팅. 크기는 웹과 같은 규칙(--px = 화면 높이/180)을 쓴다.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public event Action NewGameRequested;
        public static bool ForceTouch;                  // 에디터/데스크톱에서 터치 UI 를 강제로 켜기 (-touch 인자, T 키)

        Sim sim; GameView view; SfxPlayer sfx;
        public bool TouchMode { get; private set; }
        public bool Paused { get { return menuOpen || sheetOpen; } }
        public bool SaveLoaded;

        bool lefty, menuOpen, sheetOpen, helpOpen, miniBig, newArmed;
        float newArmedAt;
        int sheetSel = -1, sheetJust = -1;

        Canvas canvas; RectTransform canvasRt, safe;
        float px = 2, H = 180, W = 320; int f = 1;
        int lastW, lastH; Rect lastSafe;
        readonly List<Action> layouts = new List<Action>();
        readonly List<Image> frames = new List<Image>();

        // ----- 터치 상태 -----
        const int None = int.MinValue;
        int joyId = None, tapId = None; Vector2 joyOrigin, joyPos, tapPos; Vector2 face = Vector2.right;
        float jx, jy; bool actHeld; bool pressLamp, pressVent, pressHeal; int hintStep;

        // ----- UI 참조 -----
        Text hpTxt, o2Txt, zoneTxt;
        RectTransform hpFill, o2Fill; Image hpRing, o2Ring;
        readonly Text[] chipTxt = new Text[5];
        RectTransform bossRt, bossFill;
        RectTransform toastRt; Text toastTxt; CanvasGroup toastCg; float toastTimer, toastShown;
        RectTransform miniRt, trRt; RawImage miniRaw; Image dotPlayer, dotBeacon, dotBoss; readonly List<Image> dotRuins = new List<Image>(), dotMinis = new List<Image>(), dotGuide = new List<Image>();
        Text craftText; GameObject craftPanel;
        GameObject touchRoot, zones, cluster;
        Image actImg, healImg, ventImg, lampImg; Image actIcon; Text healBadge, ventBadge, lampBadge;
        RectTransform craftPillRt; Image craftPillImg;
        RectTransform joyRing, joyThumb, joyHint; Image joyHintA, joyHintB;
        GameObject sheetGo, menuGo, helpGo, startGo, victoryGo, rotateGo;
        Image hurtFlash, lowO2Flash;
        Text sheetMsg, menuNewTxt, menuSoundTxt, menuLeftyTxt, startInfoTxt;
        string curActIcon = "pick";
        float hudT, saveInfoT;
        int lastZone = -1;

        sealed class Card
        {
            public RectTransform rt; public Image frame, icon; public Text title, effect; public RectTransform go;
            public readonly Image[] chipIcon = new Image[4]; public readonly Text[] chipTxt = new Text[4];
        }
        readonly Card[] cards = new Card[Data.Crafts.Length];

        // =========================================================
        public void Init(Sim s, GameView v, SfxPlayer sf)
        {
            sim = s; view = v; sfx = sf;
            TouchMode = Application.isMobilePlatform || ForceTouch;
            try { lefty = PlayerPrefs.GetInt("abyss_lefty", 0) == 1; } catch (Exception) { }
            UiKit.LoadFonts();
            Build();
            sim.Toast += OnToast;
            sim.Buzz += ms => { if (TouchMode) Haptics.Buzz(ms); };
            sim.VictoryReached += () => { victoryGo.SetActive(true); victoryHideAt = Time.unscaledTime + 7f; };
            sim.StoryLine += OnStory; sim.ChapterBanner += OnChapterBanner;
            UpdateScale(true);
            SetTouchMode(TouchMode);
        }

        float victoryHideAt;
        bool attackMode, autoPausedByRotate;

        void OnDestroy() { if (sim != null) sim.Toast -= OnToast; }

        // ---------- 스케일 ----------
        void UpdateScale(bool force)
        {
            if (!force && Screen.width == lastW && Screen.height == lastH && Screen.safeArea == lastSafe) return;
            lastW = Screen.width; lastH = Screen.height; lastSafe = Screen.safeArea;
            W = Screen.width; H = Screen.height; px = Mathf.Max(2f, H / 180f);
            f = H >= 900 ? 3 : H >= 560 ? 2 : 1;
            canvas.referencePixelsPerUnit = 1f;
            var sa = Screen.safeArea;
            safe.anchorMin = new Vector2(sa.x / W, sa.y / H); safe.anchorMax = new Vector2((sa.x + sa.width) / W, (sa.y + sa.height) / H);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            foreach (var im in frames) im.pixelsPerUnitMultiplier = 1f / px;
            foreach (var a in layouts) a();
            RefreshAll();
        }

        float Vh(float v) { return H * v / 100f; }
        int Fs(int basePx) { return basePx * f; }

        // ---------- 빌드 도우미 ----------
        Image FrameImg(Transform parent, string sprite, string name = "frame")
        {
            var im = UiKit.Img(parent, UiKit.Frame(sprite), Color.white, false, name);
            im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 1f / px; frames.Add(im); return im;
        }

        Text T14(Transform p, string s, Color c, TextAnchor a = TextAnchor.MiddleCenter, string n = "txt") { return UiKit.Txt(p, s, UiKit.Font14, Fs(14), c, a, n); }
        Text T11(Transform p, string s, Color c, TextAnchor a = TextAnchor.MiddleCenter, string n = "txt") { return UiKit.Txt(p, s, UiKit.Font11, Fs(11), c, a, n); }
        void Lay(Action a) { layouts.Add(a); }

#if UNITY_EDITOR
        /// <summary>유니티 에디터에서만 보이는 키보드 단축키 표시: 버튼 오른쪽 아래 모서리에 작은 키캡을 붙인다.</summary>
        void KeyTag(Transform parent, string label)
        {
            var bg = UiKit.Img(parent, null, new Color32(10, 22, 38, 235), false, "keytag");
            var tx = T11(bg.rectTransform, label, new Color32(255, 224, 138, 255), TextAnchor.MiddleCenter, "key"); UiKit.Stretch(tx.rectTransform);
            var rt = bg.rectTransform;
            Lay(() => { tx.fontSize = Fs(11); float h = Fs(11) + px * 3, w = Fs(11) * 0.65f * label.Length + px * 5; UiKit.Place(rt, 1, 0, 1, 0, -px, px, Mathf.Max(w, h), h); });
        }
#endif

        static readonly Color Ink = new Color32(232, 246, 255, 255), Dim = new Color32(169, 192, 210, 255), Gold = new Color32(255, 224, 138, 255);
        static readonly Color Ok = new Color32(143, 230, 160, 255), No = new Color32(255, 154, 154, 255);

        void Build()
        {
            var go = new GameObject("HUD"); go.transform.SetParent(transform, false);
            canvas =go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            go.AddComponent<GraphicRaycaster>(); canvasRt = (RectTransform)go.transform;
            safe = UiKit.Rect("Safe", canvasRt);
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            BuildBars(); BuildChips(); BuildBoss(); BuildToast(); BuildTopRight(); BuildDesktopCraft();
            BuildTouch(); BuildSheet(); BuildMenu(); BuildHelp(); BuildStoryLog(); BuildOverlays(); BuildStart();
        }

        // ---------- 체력/산소 바, 구역 ----------
        RectTransform MakeBar(RectTransform parent, Color fillColor, out Image ring, out Text label)
        {
            var bar = UiKit.Rect("Bar", parent);
            ring = UiKit.Img(bar, null, new Color32(46, 70, 104, 255));
            UiKit.Stretch(ring.rectTransform);
            var inner = UiKit.Img(bar, null, new Color32(5, 10, 20, 255)); UiKit.Stretch(inner.rectTransform, 1, 1, 1, 1);
            var fill = UiKit.Img(bar, null, fillColor); fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.pivot = new Vector2(0, 0.5f); fill.rectTransform.offsetMin = new Vector2(2, 2); fill.rectTransform.offsetMax = new Vector2(-2, -2);
            label = T11(bar, "", Ink, TextAnchor.MiddleLeft, "label"); UiKit.Outline(label, 1);
            fillRects.Add(fill.rectTransform);
            return bar;
        }
        readonly List<RectTransform> fillRects = new List<RectTransform>();
        RectTransform hpBarRt, o2BarRt, barsRt;

        void BuildBars()
        {
            barsRt = UiKit.Rect("Bars", safe);
            Image r1, r2;
            hpBarRt = MakeBar(barsRt, new Color32(224, 82, 90, 255), out r1, out hpTxt); hpRing = r1; hpFill = fillRects[fillRects.Count - 1];
            o2BarRt = MakeBar(barsRt, new Color32(58, 167, 232, 255), out r2, out o2Txt); o2Ring = r2; o2Fill = fillRects[fillRects.Count - 1];
            zoneTxt = T11(barsRt, "", Dim, TextAnchor.UpperLeft, "zone"); UiKit.Outline(zoneTxt, 1);
            goalsTxt = T11(barsRt, "", new Color32(255, 226, 150, 255), TextAnchor.UpperLeft, "goals"); UiKit.Outline(goalsTxt, 1); goalsTxt.lineSpacing = 1.15f;
            Lay(() =>
            {
                float m = Vh(1.2f), bw = Vh(26), bh = Mathf.Max(18f, Vh(4.4f)), gap = px * 2;
                UiKit.Place(barsRt, 0, 1, 0, 1, m, -m, bw, bh * 2 + gap + Fs(11) + 4);
                UiKit.Place(hpBarRt, 0, 1, 0, 1, 0, 0, bw, bh);
                UiKit.Place(o2BarRt, 0, 1, 0, 1, 0, -(bh + gap), bw, bh);
                UiKit.Place(zoneTxt.rectTransform, 0, 1, 0, 1, 0, -(bh * 2 + gap * 2), bw * 2, Fs(11) + 4);
                foreach (var t in new[] { hpTxt, o2Txt }) { UiKit.Stretch(t.rectTransform, Vh(1), 0, 0, 0); t.fontSize = Fs(11); }
                zoneTxt.fontSize = Fs(11);
                UiKit.Place(goalsTxt.rectTransform, 0, 1, 0, 1, 0, -(bh * 2 + gap * 2 + Fs(11) + px * 2), bw * 1.5f, Fs(11) * 1.4f + 4); goalsTxt.fontSize = Fs(11); goalsTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            });
        }
        Text goalsTxt;

        // ---------- 자원 칩 ----------
        RectTransform chipsRt; Image[] chipIcons = new Image[5];
        void BuildChips()
        {
            chipsRt = UiKit.Rect("Chips", safe);
            var fr = FrameImg(chipsRt, "chip"); UiKit.Stretch(fr.rectTransform);
            var order = new[] { Item.Scrap, Item.Brass, Item.Crystal, Item.Pearl, Item.Gel };
            for (int i = 0; i < 5; i++)
            {
                chipIcons[i] = UiKit.Img(chipsRt, UiKit.Icon(Data.ItemIcon[(int)order[i]]), Color.white, false, "chipIcon");
                chipTxt[i] = T14(chipsRt, "0", Gold, TextAnchor.MiddleLeft, "chipNum"); UiKit.Outline(chipTxt[i], 1);
            }
            Lay(() =>
            {
                float cw = px * 24, pad = px * 6, gap = px * 3, total = pad * 2 + cw * 5 + gap * 4, h = px * 18, m = Vh(1.2f);
                UiKit.Place(chipsRt, 0.5f, 1, 0.5f, 1, 0, -m, total, h);
                for (int i = 0; i < 5; i++)
                {
                    float x = pad + i * (cw + gap);
                    UiKit.Place(chipIcons[i].rectTransform, 0, 0.5f, 0, 0.5f, x, 0, px * 11, px * 11);
                    UiKit.Place(chipTxt[i].rectTransform, 0, 0.5f, 0, 0.5f, x + px * 13, 0, cw - px * 13, h);
                    chipTxt[i].fontSize = Fs(11);
                }
            });
        }

        // ---------- 보스 바 ----------
        Text bossLabel;
        void BuildBoss()
        {
            bossRt = UiKit.Rect("BossBar", safe);
            var ring = UiKit.Img(bossRt, null, new Color32(107, 45, 45, 255)); UiKit.Stretch(ring.rectTransform);
            var inner = UiKit.Img(bossRt, null, new Color32(5, 10, 20, 255)); UiKit.Stretch(inner.rectTransform, 1, 1, 1, 1);
            var fill = UiKit.Img(bossRt, null, new Color32(192, 57, 43, 255)); bossFill = fill.rectTransform;
            bossFill.anchorMin = Vector2.zero; bossFill.anchorMax = Vector2.one; bossFill.pivot = new Vector2(0, 0.5f);
            bossFill.offsetMin = new Vector2(2, 2); bossFill.offsetMax = new Vector2(-2, -2);
            bossLabel = T11(bossRt, "심연의 눈", new Color32(255, 154, 154, 255), TextAnchor.LowerCenter, "bossLabel"); UiKit.Outline(bossLabel, 1);
            bossRt.gameObject.SetActive(false);
            Lay(() =>
            {
                float m = Vh(1.5f), w = Vh(36), h = Mathf.Max(14f, Vh(3.6f));
                UiKit.Place(bossRt, 0.5f, 0, 0.5f, 0, 0, m + Fs(11) + px * 4, w, h);   // 화면 아래 중앙(위쪽은 자원 칩·토스트가 쓴다)
                UiKit.Place(bossLabel.rectTransform, 0.5f, 1, 0.5f, 0, 0, 2, w, Fs(11) + 2); bossLabel.fontSize = Fs(11);
            });
        }

        // ---------- 토스트 ----------
        void BuildToast()
        {
            // 안내 문구는 게임 화면을 가리지 않게: 프레임 없이 반투명한 얇은 알약 하나, 자원 칩 바로 아래, 화면 폭의 40% 이내(왼쪽 목표·오른쪽 지도와 겹치지 않는 영역).
            toastRt = UiKit.Rect("Toast", safe); toastCg = toastRt.gameObject.AddComponent<CanvasGroup>(); toastCg.alpha = 0; toastCg.blocksRaycasts = false;
            var bg = UiKit.Img(toastRt, null, new Color(0.02f, 0.05f, 0.1f, 0.6f), false, "bg"); UiKit.Stretch(bg.rectTransform);
            toastTxt = T11(toastRt, "", Ink, TextAnchor.MiddleCenter); UiKit.Stretch(toastTxt.rectTransform); UiKit.Outline(toastTxt, 1);
            toastTxt.horizontalOverflow = HorizontalWrapMode.Wrap; toastTxt.verticalOverflow = VerticalWrapMode.Overflow;
            BuildStoryLane();
            Lay(() => { toastTxt.fontSize = Fs(11); LayoutStory(); LayoutToast(); });
        }

        // ---------- 이야기 무전 / 장 제목: 자원 칩 바로 아래 한 줄(토스트는 그 밑으로 밀린다) ----------
        RectTransform storyRt; Text storyTxt; CanvasGroup storyCg; float storyTimer, storyShown;
        void BuildStoryLane()
        {
            storyRt = UiKit.Rect("Story", safe); storyCg = storyRt.gameObject.AddComponent<CanvasGroup>(); storyCg.alpha = 0; storyCg.blocksRaycasts = false;
            var bg = UiKit.Img(storyRt, null, new Color(0.02f, 0.06f, 0.12f, 0.66f), false, "bg"); UiKit.Stretch(bg.rectTransform);
            var line = UiKit.Img(storyRt, null, new Color32(159, 232, 255, 200), false, "accent"); line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(0, 1); line.rectTransform.pivot = new Vector2(0, 0.5f); line.rectTransform.sizeDelta = new Vector2(px * 1.5f, 0);   // 왼쪽 가장자리 하늘색 띠
            storyTxt = T11(storyRt, "", Ink, TextAnchor.MiddleLeft); UiKit.Stretch(storyTxt.rectTransform, px * 6, 0, px * 4, 0); UiKit.Outline(storyTxt, 1);
            storyTxt.horizontalOverflow = HorizontalWrapMode.Wrap; storyTxt.verticalOverflow = VerticalWrapMode.Overflow; storyTxt.supportRichText = true;
        }

        float storyBoxH;
        void LayoutStory()
        {
            storyTxt.fontSize = Fs(11);
            float m = Vh(1.2f), pad = px * 6, maxInner = W * 0.40f - pad * 2;      // 왼쪽 목표 글자와 겹치지 않는 폭
            float pw = Mathf.Max(1f, storyTxt.preferredWidth);
            int lines = Mathf.Max(1, Mathf.CeilToInt(pw / maxInner));
            float w = Mathf.Min(pw, maxInner) + pad * 2 + px * 4; storyBoxH = lines * Fs(11) * 1.35f + px * 5;
            UiKit.Place(storyRt, 0.5f, 1, 0.5f, 1, 0, -(m + px * 18 + px * 2), w, storyBoxH);
            UiKit.Stretch(storyTxt.rectTransform, px * 6, 0, px * 4, 0);
        }

        void OnStory(string text, float sec)
        {
            // "아라: …" 의 화자 이름만 색을 입힌다
            int c = text.IndexOf(':');
            storyTxt.text = (c > 0 && c < 8) ? "<color=#9fe8ff>" + text.Substring(0, c) + "</color>  " + text.Substring(c + 1).TrimStart() : text;
            LayoutStory(); storyTimer = sec; storyShown = 1; LayoutToast();
        }

        void OnChapterBanner(string title)
        {
            storyTxt.text = "<color=#ffe296>" + title + "</color>";
            LayoutStory(); storyTimer = 3.4f; storyShown = 1; LayoutToast();
        }

        void LayoutToast()
        {
            float m = Vh(1.2f), pad = px * 5, maxInner = W * 0.40f - pad * 2;
            float pw = Mathf.Max(1f, toastTxt.preferredWidth);
            int lines = Mathf.Max(1, Mathf.CeilToInt(pw / maxInner));
            float w = Mathf.Min(pw, maxInner) + pad * 2, h = lines * Fs(11) * 1.35f + px * 4;
            float below = (storyShown > 0.01f || (storyCg != null && storyCg.alpha > 0.02f)) ? storyBoxH + px * 2 : 0;   // 무전이 떠 있으면 그 아래로
            UiKit.Place(toastRt, 0.5f, 1, 0.5f, 1, 0, -(m + px * 18 + px * 2 + below), w, h);
        }

        void OnToast(string msg, float sec, bool quiet)
        {
            if (quiet && msg.StartsWith("+")) return;                  // 획득 알림은 칩 숫자와 반짝임으로 충분하다(화면 위에 글자를 띄우지 않는다)
            if (quiet && toastTimer > 0.8f) return;
            toastTxt.text = msg; LayoutToast(); toastTimer = Mathf.Min(sec, 3.2f); toastShown = 1;
            if (sheetOpen && sheetMsg != null) sheetMsg.text = msg;      // 시트가 토스트를 가리므로 시트 안에도 표시
        }

        // ---------- 우상단: 메뉴 버튼 + 미니맵 ----------
        Image menuBtnImg;
        void BuildTopRight()
        {
            trRt = UiKit.Rect("TopRight", safe);
            var mb = UiKit.Rect("MenuBtn", trRt); menuBtnImg = FrameImg(mb, "btn"); UiKit.Stretch(menuBtnImg.rectTransform); menuBtnImg.raycastTarget = true;
            var mt = T14(mb, "II", Ink, TextAnchor.MiddleCenter, "pause"); UiKit.Stretch(mt.rectTransform);
            var rel = UiKit.Relay(menuBtnImg); rel.Down = e => menuBtnImg.sprite = UiKit.Frame("btn_down"); rel.Up = e => menuBtnImg.sprite = UiKit.Frame("btn");
            rel.Click = e => { if (sim.started) { if (menuOpen) CloseMenu(); else OpenMenu(); } };
            menuBtnRt = mb;
#if UNITY_EDITOR
            KeyTag(mb, "Esc");
#endif

            miniRt = UiKit.Rect("Minimap", trRt);
            var fr = FrameImg(miniRt, "panel"); UiKit.Stretch(fr.rectTransform); fr.raycastTarget = true;
            UiKit.Relay(fr).Click = e => { miniBig = !miniBig; RefreshAll(); };
            miniRaw = new GameObject("MiniTex", typeof(RectTransform)).AddComponent<RawImage>(); miniRaw.transform.SetParent(miniRt, false);
            miniRaw.raycastTarget = false; UiKit.Stretch(miniRaw.rectTransform, px * 3, px * 3, px * 3, px * 3);
            dotBeacon = UiKit.Img(miniRt, null, Color.white); dotPlayer = UiKit.Img(miniRt, null, Color.white); dotBoss = UiKit.Img(miniRt, null, new Color32(192, 64, 64, 255));
            for (int i = 0; i < World.StarterVeins.Length; i++) dotGuide.Add(UiKit.Img(miniRt, null, new Color32(224, 144, 74, 255)));   // 초반 안내: 비콘 곁 광맥(주황)
            dotGuide.Add(UiKit.Img(miniRt, null, new Color32(255, 216, 107, 255)));                                                   // 초반 안내: 시작 상자(금색)
            for (int i = 0; i < World.MiniLairs.Length; i++) dotMinis.Add(UiKit.Img(miniRt, null, new Color32(255, 120, 60, 255)));   // 미니보스 소굴(처치하면 사라짐)
            for (int i = 0; i < World.RuinCenters.Length; i++) dotRuins.Add(UiKit.Img(miniRt, null, new Color32(255, 216, 107, 255)));   // 발견한 폐허 방 표시
            Lay(() =>
            {
                float m = Vh(1.2f), mb2 = Mathf.Max(40f, Vh(8)), ms = miniBig ? Vh(50) : Vh(14);
                UiKit.Place(trRt, 1, 1, 1, 1, -m, -m, mb2 + px * 3 + ms, Mathf.Max(mb2, ms));
                UiKit.Place(menuBtnRt, 0, 1, 0, 1, 0, 0, mb2, mb2);
                UiKit.Place(miniRt, 1, 1, 1, 1, 0, 0, ms, ms);
                UiKit.Stretch(miniRaw.rectTransform, px * 3, px * 3, px * 3, px * 3);
            });
        }
        RectTransform menuBtnRt;

        // ---------- 데스크톱 제작 목록 ----------
        void BuildDesktopCraft()
        {
            craftPanel = new GameObject("DesktopCraft", typeof(RectTransform)); craftPanel.transform.SetParent(safe, false);
            var rt = (RectTransform)craftPanel.transform;
            var fr = FrameImg(rt, "panel"); UiKit.Stretch(fr.rectTransform);
            craftText = T11(rt, "", Ink, TextAnchor.UpperLeft); UiKit.Stretch(craftText.rectTransform, px * 6, px * 5, px * 6, px * 5);
            craftText.lineSpacing = 1.1f;
            Lay(() => { float m = Vh(1.5f); UiKit.Place(rt, 0, 0, 0, 0, m, m, Mathf.Max(Vh(66), 300), Fs(11) * 1.35f * 6 + px * 12); craftText.fontSize = Fs(11); UiKit.Stretch(craftText.rectTransform, px * 6, px * 5, px * 6, px * 5); });
        }

        // ---------- 터치 조작 ----------
        void BuildTouch()
        {
            touchRoot = new GameObject("Touch", typeof(RectTransform)); touchRoot.transform.SetParent(canvasRt, false); UiKit.Stretch((RectTransform)touchRoot.transform);
            zones = new GameObject("Zones", typeof(RectTransform)); zones.transform.SetParent(touchRoot.transform, false); UiKit.Stretch((RectTransform)zones.transform);
            var jz = UiKit.Img(zones.transform, null, new Color(0, 0, 0, 0), true, "JoyZone"); var tz = UiKit.Img(zones.transform, null, new Color(0, 0, 0, 0), true, "TapZone");
            Lay(() =>
            {
                float lw = W * 0.42f;
                if (!lefty) { UiKit.Place(jz.rectTransform, 0, 0, 0, 0, 0, 0, lw, H); UiKit.Place(tz.rectTransform, 1, 0, 1, 0, 0, 0, W - lw, H); }
                else { UiKit.Place(jz.rectTransform, 1, 0, 1, 0, 0, 0, lw, H); UiKit.Place(tz.rectTransform, 0, 0, 0, 0, 0, 0, W - lw, H); }
            });
            var jr = UiKit.Relay(jz);
            jr.Down = e =>
            {
                if (!sim.started) { StartGame(); return; }
                if (joyId != None || Paused) return;
                joyId = e.pointerId; joyOrigin = joyPos = e.position; joyRing.gameObject.SetActive(true); joyRing.anchoredPosition = joyOrigin; joyThumb.anchoredPosition = Vector2.zero; joyHint.gameObject.SetActive(false);
            };
            jr.Drag = e =>
            {
                if (e.pointerId != joyId) return;
                joyPos = e.position; Vector2 d = joyPos - joyOrigin; float R = JoyR();
                joyThumb.anchoredPosition = d.magnitude > R ? d.normalized * R : d;
            };
            jr.Up = e => { if (e.pointerId == joyId) JoyEnd(); };
            var tr = UiKit.Relay(tz);
            tr.Down = e => { if (!sim.started) { StartGame(); return; } if (Paused || tapId != None) return; tapId = e.pointerId; tapPos = e.position; };
            tr.Drag = e => { if (e.pointerId == tapId) tapPos = e.position; };
            tr.Up = e => { if (e.pointerId == tapId) tapId = None; };

            // 힌트(조이스틱 자리 표시)와 조이스틱 시각물
            joyHint = UiKit.Rect("JoyHint", touchRoot.transform);
            joyHintA = UiKit.Img(joyHint, null, new Color(0.5f, 0.89f, 1f, 0.3f)); joyHintB = UiKit.Img(joyHint, null, new Color(0.5f, 0.89f, 1f, 0.3f));
            var ringSprite = UiKit.BoxSprite(16, 2, new Color32(8, 20, 36, 72), new Color32(127, 227, 255, 115));
            var thumbSprite = UiKit.BoxSprite(16, 2, new Color32(127, 227, 255, 153), new Color32(232, 246, 255, 255));
            var ringImg = UiKit.Img(touchRoot.transform, ringSprite, Color.white, false, "JoyRing"); ringImg.type = Image.Type.Sliced; ringImg.pixelsPerUnitMultiplier = 1f / 2f; joyRing = ringImg.rectTransform;
            var thumbImg = UiKit.Img(joyRing, thumbSprite, Color.white, false, "JoyThumb"); thumbImg.type = Image.Type.Sliced; joyThumb = thumbImg.rectTransform;
            joyRing.gameObject.SetActive(false);
            Lay(() =>
            {
                float s = Vh(20);
                UiKit.Place(joyRing, 0, 0, 0.5f, 0.5f, 0, 0, s, s); UiKit.Place(joyThumb, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, Vh(9), Vh(9));
                ringImg.pixelsPerUnitMultiplier = 1f / (px); thumbImg.pixelsPerUnitMultiplier = 1f / px;
                float m = Vh(1.5f) + Vh(6) + s / 2;
                if (!lefty) UiKit.Place(joyHint, 0, 0, 0.5f, 0.5f, m, m, s, s); else UiKit.Place(joyHint, 1, 0, 0.5f, 0.5f, -m, m, s, s);
                UiKit.Place(joyHintA.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, px * 2, s * 0.55f);
                UiKit.Place(joyHintB.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, s * 0.55f, px * 2);
            });

            // 행동 버튼 클러스터
            cluster = new GameObject("Cluster", typeof(RectTransform)); cluster.transform.SetParent(safe, false);
            var crt = (RectTransform)cluster.transform; UiKit.Stretch(crt);
            actImg = MakeBtn(crt, "act", out var actRt); actIcon = UiKit.Img(actRt, UiKit.Icon("pick"), Color.white, false, "icon");
            healImg = MakeBtn(crt, "btn", out var healRt); var healIc = UiKit.Img(healRt, UiKit.Icon("heart"), Color.white, false, "icon"); healBadge = MakeBadge(healRt);
            ventImg = MakeBtn(crt, "btn", out var ventRt); var ventIc = UiKit.Img(ventRt, UiKit.Icon("vent"), Color.white, false, "icon"); ventBadge = MakeBadge(ventRt);
            lampImg = MakeBtn(crt, "btn", out var lampRt); var lampIc = UiKit.Img(lampRt, UiKit.Icon("lamp"), Color.white, false, "icon"); lampBadge = MakeBadge(lampRt);
#if UNITY_EDITOR
            KeyTag(actRt, "Space"); KeyTag(healRt, "F"); KeyTag(ventRt, "E"); KeyTag(lampRt, "Q");
#endif
            HoldButton(actImg, "act", () => { if (!sim.started) StartGame(); else actHeld = true; }, () => actHeld = false);
            HoldButton(lampImg, "btn", () => pressLamp = true, null);
            HoldButton(ventImg, "btn", () => pressVent = true, null);
            HoldButton(healImg, "btn", () => pressHeal = true, null);
            Lay(() =>
            {
                float m = Vh(1.5f), big = Vh(26), sm = Mathf.Max(56f, Vh(15)), R = Mathf.Max(Vh(23), 80f), sgn = lefty ? -1 : 1;
                float ax = lefty ? 0 : 1;
                Action<RectTransform, float, float, float> put = (rt, fromEdgeX, fromBottomY, size) => UiKit.Place(rt, ax, 0, 0.5f, 0.5f, -sgn * (fromEdgeX + m), fromBottomY + m, size, size);
                put(actRt, big / 2, big / 2, big);
                put(healRt, big / 2 + 0.174f * R, big / 2 + 0.985f * R, sm);
                put(ventRt, big / 2 + 0.819f * R, big / 2 + 0.574f * R, sm);
                put(lampRt, big / 2 + 0.985f * R, big / 2 - 0.174f * R, sm);
                UiKit.Place(actIcon.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, px * 32, px * 32);
                foreach (var ic in new[] { healIc, ventIc, lampIc }) UiKit.Place(ic.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, px * 16, px * 16);
                foreach (var b in new[] { healBadge, ventBadge, lampBadge }) { b.fontSize = Fs(11); LayoutBadge(b); }
            });

            // 제작 알약 버튼 (비콘 근처에서만)
            craftPillRt = UiKit.Rect("CraftPill", safe); craftPillImg = FrameImg(craftPillRt, "btn"); UiKit.Stretch(craftPillImg.rectTransform); craftPillImg.raycastTarget = true;
            var pi = UiKit.Img(craftPillRt, UiKit.Icon("hammer"), Color.white, false, "icon"); var pt = T14(craftPillRt, "제작", Ink, TextAnchor.MiddleLeft, "label"); UiKit.Outline(pt, 1);
            var pr = UiKit.Relay(craftPillImg);
            pr.Down = e => craftPillImg.sprite = UiKit.Frame("btn_down"); pr.Up = e => craftPillImg.sprite = UiKit.Frame("btn"); pr.Click = e => OpenSheet();
            Lay(() =>
            {
                float m = Vh(1.5f), w = px * 52, h = Mathf.Max(48f, Vh(12));
                UiKit.Place(craftPillRt, 0.5f, 0, 0.5f, 0, 0, m, w, h);
                UiKit.Place(pi.rectTransform, 0, 0.5f, 0, 0.5f, px * 8, 0, px * 16, px * 16);
                UiKit.Place(pt.rectTransform, 0, 0.5f, 0, 0.5f, px * 28, 0, w - px * 28, h); pt.fontSize = Fs(14);
            });
            craftPillRt.gameObject.SetActive(false);
        }

        float JoyR() { return Mathf.Clamp(H * 0.12f, 36f, 64f); }

        void JoyEnd() { joyId = None; joyRing.gameObject.SetActive(false); if (TouchMode) joyHint.gameObject.SetActive(true); jx = jy = 0; }
        void TapEnd() { tapId = None; }

        Image MakeBtn(Transform parent, string sprite, out RectTransform rt)
        {
            rt = UiKit.Rect("Btn", parent); var im = FrameImg(rt, sprite); UiKit.Stretch(im.rectTransform); im.raycastTarget = true; return im;
        }

        Text MakeBadge(RectTransform parent)
        {
            var bg = UiKit.Img(parent, null, new Color32(10, 22, 38, 255), false, "badge");
            var t = T11(bg.transform, "0", Gold, TextAnchor.MiddleCenter, "badgeTxt"); UiKit.Stretch(t.rectTransform); UiKit.Outline(t, 1);
            return t;
        }

        void LayoutBadge(Text t)
        {
            var bg = (RectTransform)t.transform.parent; float w = Mathf.Max(px * 9, t.preferredWidth + px * 4);
            UiKit.Place(bg, 1, 0, 1, 0, px * 3, -px * 3, w, px * 9);
        }

        void HoldButton(Image img, string baseSprite, Action down, Action up)
        {
            var r = UiKit.Relay(img); string downSprite = baseSprite + "_down";
            r.Down = e => { img.sprite = UiKit.Frame(downSprite); if (down != null) down(); };
            r.Up = e => { img.sprite = UiKit.Frame(baseSprite); if (up != null) up(); };
        }

        // ---------- 제작 시트 ----------
        RectTransform sheetPanel, sheetHeader; Text sheetTitle, sheetMeta; Image sheetCloseImg; Text sheetCloseTxt;
        RectTransform sheetCloseRt;

        void BuildSheet()
        {
            sheetGo = Overlay("Sheet", 0.82f);
            sheetPanel = UiKit.Rect("Panel", sheetGo.transform); var pf = FrameImg(sheetPanel, "panel"); UiKit.Stretch(pf.rectTransform); pf.raycastTarget = true;
            sheetHeader = UiKit.Rect("Header", sheetPanel);
            sheetTitle = T14(sheetHeader, "제작", new Color32(159, 232, 255, 255), TextAnchor.MiddleLeft);
            sheetMeta = T11(sheetHeader, "숫자 = 보유 / 필요", Dim, TextAnchor.MiddleLeft);
            sheetMsg = T11(sheetHeader, "", Ok, TextAnchor.MiddleLeft);
            sheetCloseRt = UiKit.Rect("Close", sheetHeader); sheetCloseImg = FrameImg(sheetCloseRt, "btn"); UiKit.Stretch(sheetCloseImg.rectTransform); sheetCloseImg.raycastTarget = true;
            sheetCloseTxt = T14(sheetCloseRt, "닫기", Ink); UiKit.Stretch(sheetCloseTxt.rectTransform);
            var cr = UiKit.Relay(sheetCloseImg); cr.Down = e => sheetCloseImg.sprite = UiKit.Frame("btn_down"); cr.Up = e => sheetCloseImg.sprite = UiKit.Frame("btn"); cr.Click = e => CloseSheet();
            for (int i = 0; i < Data.Crafts.Length; i++) cards[i] = MakeCard(i);
            Lay(LayoutSheet);
            sheetGo.SetActive(false);
        }

        Card MakeCard(int i)
        {
            var c = new Card(); c.rt = UiKit.Rect("Card" + i, sheetPanel);
            c.frame = FrameImg(c.rt, "card"); UiKit.Stretch(c.frame.rectTransform); c.frame.raycastTarget = true;
            c.icon = UiKit.Img(c.rt, null, Color.white, false, "icon");
            c.title = T14(c.rt, "", Ink, TextAnchor.MiddleLeft, "title"); c.effect = T11(c.rt, "", Dim, TextAnchor.MiddleLeft, "effect");
            for (int k = 0; k < 4; k++) { c.chipIcon[k] = UiKit.Img(c.rt, null, Color.white, false, "chipIcon"); c.chipTxt[k] = T11(c.rt, "", Ok, TextAnchor.MiddleLeft, "chipTxt"); }
            var go = UiKit.Rect("Go", c.rt); var goFrame = FrameImg(go, "act"); UiKit.Stretch(goFrame.rectTransform); goFrame.raycastTarget = true;
            var gt = T14(go, "제작", Color.white); UiKit.Stretch(gt.rectTransform); UiKit.Outline(gt, 1);
            c.go = go;
            var gr = UiKit.Relay(goFrame); gr.Down = e => goFrame.sprite = UiKit.Frame("act_down"); gr.Up = e => goFrame.sprite = UiKit.Frame("act");
            int idx = i;
            gr.Click = e => OnCardGo(idx);
            UiKit.Relay(c.frame).Click = e => OnCardTap(idx);
            return c;
        }

        void LayoutSheet()
        {
            int rowsN = (Data.Crafts.Length + 1) / 2;       // 카드 2열
            float pw = Mathf.Min(W * 0.94f, H * 1.85f), pad = px * 4, gap = px * 2, hh = Mathf.Max(48f, Vh(rowsN > 4 ? 9.5f : 11)), ch = Mathf.Max(48f, Vh(rowsN > 4 ? 12.6f : 13));
            float ph = hh + ch * rowsN + gap * rowsN + pad * 2 + px * 4;
            UiKit.Place(sheetPanel, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, pw, ph);
            UiKit.Place(sheetHeader, 0.5f, 1, 0.5f, 1, 0, -(pad + px * 4), pw - pad * 2 - px * 8, hh);
            float hw = pw - pad * 2 - px * 8;
            float cbw = px * 34;
            UiKit.Place(sheetCloseRt, lefty ? 0 : 1, 0.5f, lefty ? 0 : 1, 0.5f, 0, 0, cbw, hh);
            float tx = lefty ? cbw + px * 4 : 0;
            UiKit.Place(sheetTitle.rectTransform, 0, 0.5f, 0, 0.5f, tx, 0, px * 30, hh); sheetTitle.fontSize = Fs(14);
            UiKit.Place(sheetMeta.rectTransform, 0, 0.5f, 0, 0.5f, tx + px * 30, 0, hw - cbw - px * 36, hh * 0.5f); sheetMeta.fontSize = Fs(11);
            sheetMeta.rectTransform.anchoredPosition = new Vector2(tx + px * 30, hh * 0.25f);
            UiKit.Place(sheetMsg.rectTransform, 0, 0.5f, 0, 0.5f, tx + px * 30, -hh * 0.25f, hw - cbw - px * 36, hh * 0.5f); sheetMsg.fontSize = Fs(11);
            sheetCloseTxt.fontSize = Fs(14);
            float cw = (pw - pad * 2 - px * 8 - gap) / 2f;
            for (int i = 0; i < Data.Crafts.Length; i++)
            {
                int col = i % 2, row = i / 2; var c = cards[i];
                UiKit.Place(c.rt, 0, 1, 0, 1, pad + px * 4 + col * (cw + gap), -(pad + px * 4 + hh + gap + row * (ch + gap)), cw, ch);
                UiKit.Place(c.icon.rectTransform, 0, 0.5f, 0, 0.5f, px * 7, 0, px * 16, px * 16);
                c.title.fontSize = Fs(14); c.effect.fontSize = Fs(11);
                for (int k = 0; k < 4; k++) c.chipTxt[k].fontSize = Fs(11);
                UiKit.Place(c.go, 1, 0.5f, 1, 0.5f, -px * 5, 0, px * 26, Mathf.Max(44f, px * 22));
                var gt = c.go.GetComponentInChildren<Text>(); if (gt != null) gt.fontSize = Fs(14);
            }
            RefreshSheet();
        }

        static string EffectText(Track t, Level l)
        {
            switch (t)
            {
                case Track.Pick: return "티어" + l.tier + "·속도" + (int)l.power;
                case Track.Harpoon: return "공격" + l.dmg;
                case Track.Light: return "시야" + l.r;
                case Track.Tank: return "산소" + l.max + (l.refill > 1f ? "·충전↑" : "");
                case Track.Fins: return "이동" + (int)l.speed + (l.drain > 0f && l.drain < 1f ? "·절약" : "");
                case Track.Beacon: return "반경" + l.r;
                default: return "체력" + l.hp;
            }
        }

        static string EffectArrow(Track t, Level a, Level b)
        {
            if (t == Track.Pick) return "티어" + a.tier + "→" + b.tier + "·속도" + (int)a.power + "→" + (int)b.power;   // 곡괭이만 압축 표기(말줄임 방지)
            return EffectText(t, a) + "→" + EffectText(t, b);
        }

        void RefreshSheet()
        {
            if (sheetGo == null || !sheetGo.activeSelf) return;
            for (int i = 0; i < Data.Crafts.Length; i++)
            {
                var d = Data.Crafts[i]; var c = cards[i]; string title, eff; Cost[] cost = null; bool maxed = false; string icon;
                if (d.upgrade)
                {
                    int lv = sim.P.lvl[(int)d.track]; var nx = sim.NextLevel(d.track); icon = Data.TrackIcon[(int)d.track];
                    if (nx == null) { title = Data.Up[(int)d.track][lv].name; eff = EffectText(d.track, Data.Up[(int)d.track][lv]); maxed = true; }
                    else { title = nx.name; eff = EffectArrow(d.track, Data.Up[(int)d.track][lv], nx); cost = nx.cost; }
                }
                else { title = d.label; eff = d.id == "lamp" ? "어둠을 밝힌다" : d.id == "potion" ? "산소 60% 충전" : "산소 충전 거점"; cost = d.cost; icon = d.id; }
                bool ok = !maxed && sim.CanAfford(cost);
                bool sel = sheetSel == i && d.upgrade && !maxed;
                c.frame.sprite = UiKit.Frame(maxed ? "card_max" : sel ? "card_sel" : ok ? "card_can" : "card");
                if (sheetJust == i && Mathf.FloorToInt(Time.unscaledTime * 8f) % 2 == 0) c.frame.sprite = UiKit.Frame("card_sel");
                c.icon.sprite = UiKit.Icon(icon); c.icon.color = maxed ? new Color(1, 1, 1, 0.55f) : Color.white;
                c.title.text = title; c.effect.text = eff;
                float tx = px * 28, rightLimit = c.rt.sizeDelta.x - (sel && ok ? px * 34 : px * 6);
                UiKit.Place(c.title.rectTransform, 0, 1, 0, 1, tx, -px * 3, c.title.preferredWidth + 4, Fs(14) + 4);
                UiKit.Place(c.effect.rectTransform, 0, 1, 0, 1, tx + c.title.preferredWidth + px * 4, -px * 3 - (Fs(14) - Fs(11)) * 0.5f, Mathf.Max(10, rightLimit - (tx + c.title.preferredWidth + px * 4)), Fs(11) + 4);
                c.title.color = maxed ? Dim : Ink;
                float x = tx; float cy = -(px * 3 + Fs(14) + px * 4);
                for (int k = 0; k < 4; k++)
                {
                    bool has = !maxed && cost != null && k < cost.Length;
                    c.chipIcon[k].gameObject.SetActive(has || (maxed && k == 0 && false)); c.chipTxt[k].gameObject.SetActive(has || (maxed && k == 0));
                    if (maxed && k == 0) { c.chipTxt[0].text = "최대 등급"; c.chipTxt[0].color = Ok; UiKit.Place(c.chipTxt[0].rectTransform, 0, 1, 0, 1, tx, cy, px * 60, Fs(11) + 4); continue; }
                    if (!has) continue;
                    int have = sim.inv[(int)cost[k].item], need = cost[k].amount;
                    c.chipIcon[k].sprite = UiKit.Icon(Data.ItemIcon[(int)cost[k].item]);
                    c.chipTxt[k].text = have + "/" + need; c.chipTxt[k].color = have >= need ? Ok : No;
                    UiKit.Place(c.chipIcon[k].rectTransform, 0, 1, 0, 1, x, cy - px * 0.5f, px * 12, px * 12);
                    UiKit.Place(c.chipTxt[k].rectTransform, 0, 1, 0, 1, x + px * 13, cy, c.chipTxt[k].preferredWidth + 4, Fs(11) + 4);
                    x += px * 13 + c.chipTxt[k].preferredWidth + px * 5;
                }
                c.go.gameObject.SetActive(sel && ok);
            }
            if (sheetJust >= 0 && Time.unscaledTime - sheetJustAt > 0.4f) sheetJust = -1;     // 제작 직후 카드 깜빡임은 0.4초만
        }
        float sheetJustAt;

        void OnCardTap(int i)
        {
            var d = Data.Crafts[i];
            var nx = d.upgrade ? sim.NextLevel(d.track) : null;
            if (d.upgrade && nx == null) return;                       // 최대 등급
            if (!d.upgrade) { sheetJust = i; sheetJustAt = Time.unscaledTime; sim.TryCraft(i); RefreshSheet(); return; }   // 소모품은 한 번에
            if (sheetSel != i) { sheetSel = i; RefreshSheet(); return; }
            if (!sim.CanAfford(nx.cost)) OnToast("재료가 부족하다", 1.4f, false);
        }

        void OnCardGo(int i)
        {
            var d = Data.Crafts[i]; if (!d.upgrade) return;
            var nx = sim.NextLevel(d.track); if (nx == null || !sim.CanAfford(nx.cost)) return;
            sheetJust = i; sheetJustAt = Time.unscaledTime; sheetSel = -1; sim.TryCraft(i); RefreshSheet();
        }

        // ---------- 메뉴 / 도움말 ----------
        Image MenuButton(Transform parent, string label, Action onClick, out Text txt)
        {
            var rt = UiKit.Rect("MenuBtn", parent); var im = FrameImg(rt, "btn"); UiKit.Stretch(im.rectTransform); im.raycastTarget = true;
            txt = T14(rt, label, Ink); UiKit.Stretch(txt.rectTransform);
            var r = UiKit.Relay(im); r.Down = e => im.sprite = UiKit.Frame("btn_down"); r.Up = e => im.sprite = UiKit.Frame("btn"); r.Click = e => onClick();
            return im;
        }

        RectTransform menuPanel; readonly List<Image> menuBtns = new List<Image>(); readonly List<Text> menuTxts = new List<Text>();
        void BuildMenu()
        {
            menuGo = Overlay("Menu", 0.82f);
            menuPanel = UiKit.Rect("Panel", menuGo.transform); var pf = FrameImg(menuPanel, "panel"); UiKit.Stretch(pf.rectTransform); pf.raycastTarget = true;
            var title = T14(menuPanel, "일시정지", new Color32(159, 232, 255, 255), TextAnchor.MiddleLeft, "title"); menuTxts.Add(title);
            Text t;
            menuBtns.Add(MenuButton(menuPanel, "계속하기", CloseMenu, out t)); menuTxts.Add(t);
#if UNITY_EDITOR
            KeyTag(menuBtns[menuBtns.Count - 1].transform, "Esc");
#endif
            menuBtns.Add(MenuButton(menuPanel, "이야기 기록", OpenLog, out t)); menuTxts.Add(t);
            menuBtns.Add(MenuButton(menuPanel, "조작법 보기", () => { helpOpen = true; helpGo.SetActive(true); }, out t)); menuTxts.Add(t);
            menuBtns.Add(MenuButton(menuPanel, "소리: 켜짐", () => { sfx.Muted = !sfx.Muted; RefreshMenu(); }, out menuSoundTxt)); menuTxts.Add(menuSoundTxt);
#if UNITY_EDITOR
            KeyTag(menuBtns[menuBtns.Count - 1].transform, "M");
#endif
            menuBtns.Add(MenuButton(menuPanel, "전체화면", () => { Screen.fullScreen = !Screen.fullScreen; }, out t)); menuTxts.Add(t);
            menuBtns.Add(MenuButton(menuPanel, "왼손 모드: 꺼짐", ToggleLefty, out menuLeftyTxt)); menuTxts.Add(menuLeftyTxt);
            menuBtns.Add(MenuButton(menuPanel, "새 게임", OnNewGameTap, out menuNewTxt)); menuTxts.Add(menuNewTxt);
            Lay(() =>
            {
                float bh = Mathf.Max(38f, Vh(9.4f)), gap = px * 1.6f, pad = px * 5, pw = Mathf.Min(W * 0.7f, H * 1.0f + 200), th = Fs(14) + px * 4;
                UiKit.Place(menuPanel, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, pw, pad * 2 + px * 4 + th + gap + menuBtns.Count * (bh + gap));
                UiKit.Place(title.rectTransform, 0, 1, 0, 1, pad + px * 4, -(pad + px * 2), pw - pad * 2, th);
                for (int i = 0; i < menuBtns.Count; i++) UiKit.Place(menuBtns[i].transform.parent.GetComponent<RectTransform>(), 0.5f, 1, 0.5f, 1, 0, -(pad + px * 4 + th + gap + i * (bh + gap)), pw - pad * 2 - px * 8, bh);
                foreach (var tx in menuTxts) tx.fontSize = Fs(14);
            });
            menuGo.SetActive(false);
        }

        // ---------- 이야기 기록: 지금까지 들은 무전을 다시 본다 ----------
        GameObject logGo; Text logTxt; bool logOpen;
        void BuildStoryLog()
        {
            logGo = Overlay("StoryLog", 0.92f);
            var panel = UiKit.Rect("Panel", logGo.transform); var pf = FrameImg(panel, "panel"); UiKit.Stretch(pf.rectTransform); pf.raycastTarget = true;
            var title = T14(panel, "이야기 기록", new Color32(159, 232, 255, 255), TextAnchor.MiddleLeft);
            logTxt = T11(panel, "", Ink, TextAnchor.UpperLeft); logTxt.lineSpacing = 1.2f; logTxt.horizontalOverflow = HorizontalWrapMode.Wrap; logTxt.verticalOverflow = VerticalWrapMode.Truncate; logTxt.supportRichText = true;
            var closeRt = UiKit.Rect("Close", panel); var ci = FrameImg(closeRt, "btn"); UiKit.Stretch(ci.rectTransform); ci.raycastTarget = true;
            var ct = T14(closeRt, "닫기", Ink); UiKit.Stretch(ct.rectTransform);
            var rl = UiKit.Relay(ci); rl.Down = e => ci.sprite = UiKit.Frame("btn_down"); rl.Up = e => ci.sprite = UiKit.Frame("btn"); rl.Click = e => { logOpen = false; logGo.SetActive(false); };
            Lay(() =>
            {
                float pw = Mathf.Min(W * 0.88f, H * 1.7f), ph = H * 0.86f, pad = px * 6, bh = Mathf.Max(40f, Vh(9.5f)), th = Fs(14) + px * 4;
                UiKit.Place(panel, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, pw, ph);
                UiKit.Place(title.rectTransform, 0, 1, 0, 1, pad, -pad, pw, th); title.fontSize = Fs(14);
                UiKit.Place(logTxt.rectTransform, 0, 1, 0, 1, pad, -(pad + th + px * 2), pw - pad * 2, ph - pad * 2 - th - bh - px * 6); logTxt.fontSize = Fs(11);
                UiKit.Place(closeRt, 0.5f, 0, 0.5f, 0, 0, pad, px * 50, bh); ct.fontSize = Fs(14);
            });
            logGo.SetActive(false);
        }

        public void OpenLog() { logOpen = true; logGo.SetActive(true); RefreshLog(); }

        void RefreshLog()
        {
            var sb = new StringBuilder(); var log = sim.storyLog; int from = Mathf.Max(0, log.Count - 11);
            if (log.Count == 0) sb.Append("<color=#a9c0d2>아직 들은 무전이 없다.</color>");
            for (int i = from; i < log.Count; i++)
            {
                string l = log[i];
                if (l.StartsWith("—")) { sb.Append("<color=#ffe296>").Append(l).Append("</color>\n"); continue; }
                int c = l.IndexOf(':');
                sb.Append(c > 0 && c < 8 ? "<color=#9fe8ff>" + l.Substring(0, c) + "</color>  " + l.Substring(c + 1).TrimStart() : l).Append('\n');
            }
            logTxt.text = sb.ToString();
        }

        void BuildHelp()
        {
            helpGo = Overlay("Help", 0.9f);
            var panel = UiKit.Rect("Panel", helpGo.transform); var pf = FrameImg(panel, "panel"); UiKit.Stretch(pf.rectTransform); pf.raycastTarget = true;
            var title = T14(panel, "조작법", new Color32(159, 232, 255, 255), TextAnchor.MiddleLeft);
            var rows = new[] {
                new[] { "move", "왼쪽 화면을 끌어서 이동", "처음 누른 곳에 조이스틱이 생긴다" },
                new[] { "harpoon", "오른쪽 아래 큰 버튼을 꾹 누르기", "적이 가까우면 자동 공격, 아니면 앞쪽 채굴·상자·조개" },
                new[] { "pick", "화면을 직접 터치", "누른 타일을 정밀하게 채굴 (램프·환기구 회수도 여기서)" },
                new[] { "hammer", "비콘 근처에서 제작 버튼", "산소는 비콘·환기구 근처에서만 충전된다" } };
            var ics = new List<Image>(); var t1 = new List<Text>(); var t2 = new List<Text>();
            foreach (var r in rows)
            {
                ics.Add(UiKit.Img(panel, UiKit.Icon(r[0]), Color.white, false, "icon")); t1.Add(T14(panel, r[1], Ink, TextAnchor.MiddleLeft)); t2.Add(T11(panel, r[2], Dim, TextAnchor.MiddleLeft));
            }
            var closeRt = UiKit.Rect("Close", panel); var ci = FrameImg(closeRt, "btn"); UiKit.Stretch(ci.rectTransform); ci.raycastTarget = true;
            var ct = T14(closeRt, "닫기", Ink); UiKit.Stretch(ct.rectTransform);
            var rl = UiKit.Relay(ci); rl.Down = e => ci.sprite = UiKit.Frame("btn_down"); rl.Up = e => ci.sprite = UiKit.Frame("btn"); rl.Click = e => { helpOpen = false; helpGo.SetActive(false); };
            Lay(() =>
            {
                float pw = Mathf.Min(W * 0.88f, H * 1.7f), pad = px * 6, rowH = Mathf.Max(Fs(14) + Fs(11) + px * 4, px * 22), bh = Mathf.Max(48f, Vh(11));
                UiKit.Place(panel, 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, pw, pad * 2 + Fs(14) + px * 4 + rows.Length * (rowH + px * 2) + bh + px * 4);
                UiKit.Place(title.rectTransform, 0, 1, 0, 1, pad, -pad, pw, Fs(14) + 4); title.fontSize = Fs(14);
                for (int i = 0; i < rows.Length; i++)
                {
                    float y = -(pad + Fs(14) + px * 4 + i * (rowH + px * 2));
                    UiKit.Place(ics[i].rectTransform, 0, 1, 0, 1, pad, y - (rowH - px * 16) / 2, px * 16, px * 16);
                    UiKit.Place(t1[i].rectTransform, 0, 1, 0, 1, pad + px * 24, y, pw, Fs(14) + 2); t1[i].fontSize = Fs(14);
                    UiKit.Place(t2[i].rectTransform, 0, 1, 0, 1, pad + px * 24, y - Fs(14) - 2, pw, Fs(11) + 2); t2[i].fontSize = Fs(11);
                }
                UiKit.Place(closeRt, 0.5f, 0, 0.5f, 0, 0, pad, px * 50, bh); ct.fontSize = Fs(14);
            });
            helpGo.SetActive(false);
        }

        GameObject Overlay(string name, float alpha)
        {
            var rt = UiKit.Rect(name, canvasRt); UiKit.Stretch(rt);
            var im = UiKit.Img(rt, null, new Color(0.008f, 0.024f, 0.055f, alpha), true, "dim");
            UiKit.Stretch(im.rectTransform);
            return rt.gameObject;
        }

        // ---------- 시작 화면, 오버레이 ----------
        void BuildOverlays()
        {
            var hurtRt = UiKit.Rect("HurtFlash", canvasRt); UiKit.Stretch(hurtRt); hurtFlash = hurtRt.gameObject.AddComponent<Image>(); hurtFlash.color = new Color(1, 0.16f, 0.16f, 0); hurtFlash.raycastTarget = false;
            var lowRt = UiKit.Rect("LowO2", canvasRt); UiKit.Stretch(lowRt); lowO2Flash = lowRt.gameObject.AddComponent<Image>(); lowO2Flash.color = new Color(0, 0.12f, 0.24f, 0); lowO2Flash.raycastTarget = false;

            victoryGo = Overlay("Victory", 0.7f); victoryGo.GetComponentInChildren<Image>().raycastTarget = false;
            var vt = T14(victoryGo.transform, "비콘이 되살아났다!", new Color32(255, 216, 107, 255)); var vs = T11(victoryGo.transform, "심연의 눈이 감겼다… 하지만 더 깊은 곳에서 다른 불빛이 깜박인다.", Ink);
            Lay(() => { UiKit.Place(vt.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, px * 10, W, Fs(14) * 2); vt.fontSize = Fs(14) * 2; UiKit.Place(vs.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, 0, -px * 10, W, Fs(11) + 6); vs.fontSize = Fs(11); });
            victoryGo.SetActive(false);

            rotateGo = Overlay("Rotate", 1f);
            var rt1 = T14(rotateGo.transform, "화면을 가로로 돌려주세요", Ink); UiKit.Stretch(rt1.rectTransform);
            Lay(() => { rt1.fontSize = Fs(14) * 2; });
            rotateGo.SetActive(false);
        }

        Text startSub, startPrompt;
        RectTransform startWorld; CanvasGroup startCg; TitleScene titleScene = new TitleScene(); float startAt = -1f;
        void BuildStart()
        {
            startGo = Overlay("Start", 1f); startCg = startGo.AddComponent<CanvasGroup>();
            // 배경 장면(레이어·동물·빛)은 한 컨테이너에 두고 같이 천천히 확대/이동시킨다 (여백을 둬서 가장자리가 보이지 않게)
            startWorld = UiKit.Rect("World", startGo.transform); UiKit.Stretch(startWorld, -40, -40, -40, -40);
            titleScene.Build(startWorld);
            startSub = T14(startGo.transform, "숨이 다하기 전에, 빛을 되살려라", Ink); UiKit.Outline(startSub, 2);
            startPrompt = T14(startGo.transform, "화면을 터치(클릭)해서 시작", Gold); UiKit.Outline(startPrompt, 2);
            startInfoTxt = T11(startGo.transform, "", Dim); UiKit.Outline(startInfoTxt, 1);
            UiKit.Relay(startGo.GetComponentInChildren<Image>()).Down = e => StartGame();
            Lay(() =>
            {
                startSub.fontSize = Fs(14); startPrompt.fontSize = Fs(14); startInfoTxt.fontSize = Fs(11);
                UiKit.Place(startSub.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, H * 0.20f, W, Fs(14) + 8);
                UiKit.Place(startPrompt.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, H * 0.11f, W, Fs(14) + 8);
                UiKit.Place(startInfoTxt.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, H * 0.04f, W, Fs(11) + 6);
                RefreshStartText();
            });
        }

        /// <summary>타이틀 모션: 검정에서 페이드인, 천천히 밀고 들어가는 카메라, 레이어별 움직임(TitleScene), 부제와 안내 문구 떠오름.</summary>
        void AnimateStart()
        {
            if (startAt < 0) startAt = Time.unscaledTime;
            float t = Time.unscaledTime - startAt;
            startCg.alpha = Mathf.SmoothStep(0, 1, t / 0.9f);
            float zoom = 1.03f + 0.025f * Mathf.Sin(t * 0.35f) + 0.04f * (1f - Mathf.Exp(-t * 0.6f));   // 처음에 살짝 밀고 들어온 뒤 숨 쉬듯 일렁임
            startWorld.localScale = new Vector3(zoom, zoom, 1);
            startWorld.anchoredPosition = new Vector2(Mathf.Sin(t * 0.27f) * W * 0.008f, Mathf.Cos(t * 0.21f) * H * 0.008f);
            titleScene.Tick(t, W, H, Fs(14));

            // 부제와 안내 문구가 차례로 아래에서 떠오른다
            float k1 = Mathf.SmoothStep(0, 1, (t - 0.6f) / 0.9f), k2 = Mathf.SmoothStep(0, 1, (t - 1.1f) / 0.9f);
            UiKit.Place(startSub.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, H * 0.20f - (1 - k1) * Fs(14) * 1.5f, W, Fs(14) + 8);
            var c1 = startSub.color; c1.a = k1; startSub.color = c1;
            UiKit.Place(startPrompt.rectTransform, 0.5f, 0f, 0.5f, 0f, 0, H * 0.11f - (1 - k2) * Fs(14) * 1.5f, W, Fs(14) + 8);
            var pc = startPrompt.color; pc.a = k2 * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f)); startPrompt.color = pc;
        }

        void RefreshStartText()
        {
            if (startInfoTxt == null) return;
            startPrompt.text = SaveLoaded ? "화면을 터치(클릭)해서 이어하기" : "화면을 터치(클릭)해서 시작";
#if UNITY_EDITOR
            // 키보드 단축키 안내는 에디터에서만 보인다 (빌드에는 표시하지 않음)
            startInfoTxt.text = TouchMode ? "" : (SaveLoaded ? "N 키: 새 게임   ·   I 키: 인트로 다시 보기" : "I 키: 인트로 다시 보기");
#else
            startInfoTxt.text = "";
#endif
        }

        public void BeginPlay() { skipIntro = true; StartGame(); }

        bool skipIntro, introPlaying;
        void StartGame()
        {
            if (sim.started || introPlaying) return;
            // 새 게임(저장 없음)이고 아직 한 번도 안 봤으면 인트로 컷신을 먼저 보여 준다
            // (저장이 있어도 인트로를 한 번도 못 봤으면 한 번 보여 준다. 타이틀에서 I 키로 다시 볼 수 있다)
            bool seen = false; try { seen = PlayerPrefs.GetInt("deeplight_intro_seen", 0) == 1; } catch (Exception) { }
            if (!skipIntro && (!SaveLoaded || !seen))
            {
                introPlaying = true; startGo.SetActive(false);
                IntroSequence.Play(canvasRt, sfx.Play, () => { introPlaying = false; skipIntro = true; try { PlayerPrefs.SetInt("deeplight_intro_seen", 1); PlayerPrefs.Save(); } catch (Exception) { } StartGame(); });
                return;
            }
            sim.started = true; startGo.SetActive(false);
            OnToast(TouchMode ? "왼쪽 화면을 끌어서 이동하세요" : "지도의 주황 점(광맥)에서 고철을 캐보자", 4f, false);
        }

        // ---------- 메뉴/시트 열기 닫기 ----------
        void SyncPause()
        {
            if (Paused) { JoyEnd(); TapEnd(); actHeld = false; }
        }

        public void OpenSheet() { if (!sim.started || menuOpen) return; sheetOpen = true; sheetSel = -1; sheetMsg.text = ""; sheetGo.SetActive(true); SyncPause(); RefreshSheet(); }
        void CloseSheet() { sheetOpen = false; sheetGo.SetActive(false); SyncPause(); }
        public void OpenMenu() { if (!sim.started) return; if (sheetOpen) CloseSheet(); menuOpen = true; menuGo.SetActive(true); SyncPause(); RefreshMenu(); }
        void CloseMenu() { menuOpen = false; menuGo.SetActive(false); helpOpen = false; helpGo.SetActive(false); logOpen = false; logGo.SetActive(false); SyncPause(); }

        void RefreshMenu()
        {
            menuSoundTxt.text = "소리: " + (sfx.Muted ? "꺼짐" : "켜짐");
            menuLeftyTxt.text = "왼손 모드: " + (lefty ? "켜짐" : "꺼짐");
            menuNewTxt.text = newArmed ? "정말 삭제하고 새로 시작? (한 번 더)" : "새 게임";
        }

        void ToggleLefty()
        {
            lefty = !lefty; try { PlayerPrefs.SetInt("abyss_lefty", lefty ? 1 : 0); } catch (Exception) { }
            foreach (var a in layouts) a(); RefreshMenu();
        }

        void OnNewGameTap()
        {
            if (!newArmed) { newArmed = true; newArmedAt = Time.unscaledTime; RefreshMenu(); return; }
            if (NewGameRequested != null) NewGameRequested();
        }

        void SetTouchMode(bool on)
        {
            TouchMode = on;
            touchRoot.SetActive(on); cluster.SetActive(on); craftPanel.SetActive(!on);
            if (!on) craftPillRt.gameObject.SetActive(false);
            joyHint.gameObject.SetActive(on);
            RefreshStartText();
        }

        // =========================================================
        // 입력 -> SimInput
        // =========================================================
        static bool Key(KeyCode k) { return Input.GetKey(k); }
        static bool KeyDown(KeyCode k) { return Input.GetKeyDown(k); }

        void HotKeys()
        {
            if (KeyDown(KeyCode.Escape) && sim.started) { if (menuOpen) CloseMenu(); else OpenMenu(); }
            if (KeyDown(KeyCode.M)) { sfx.Muted = !sfx.Muted; OnToast(sfx.Muted ? "소리 끔" : "소리 켬", 1f, false); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (KeyDown(KeyCode.T)) { ForceTouch = !TouchMode; SetTouchMode(!TouchMode); }
#endif
            if (!sim.started)
            {
                if (startWorld != null && startGo.activeSelf) AnimateStart();
                if (KeyDown(KeyCode.I) && !introPlaying) { skipIntro = false; try { PlayerPrefs.SetInt("deeplight_intro_seen", 0); } catch (Exception) { } StartGame(); return; }
                if (KeyDown(KeyCode.N) && SaveLoaded && NewGameRequested != null) { NewGameRequested(); return; }
                if (Input.anyKeyDown && !helpOpen && !introPlaying && !KeyDown(KeyCode.I) && !KeyDown(KeyCode.N) && !KeyDown(KeyCode.T) && !Input.GetMouseButtonDown(0)) StartGame();
            }
        }

        public SimInput ReadInput()
        {
            UpdateScale(false);
            HotKeys();
            if (Paused || !sim.started) { if (Input.GetMouseButtonDown(0) && !sim.started && !TouchMode) { /* 시작 오버레이 클릭은 Relay 가 처리 */ } return default(SimInput); }
            return TouchMode ? TouchInput() : DesktopInput();
        }

        SimInput DesktopInput()
        {
            var inp = new SimInput();
            float mx = (Key(KeyCode.D) || Key(KeyCode.RightArrow) ? 1 : 0) - (Key(KeyCode.A) || Key(KeyCode.LeftArrow) ? 1 : 0);
            float my = (Key(KeyCode.S) || Key(KeyCode.DownArrow) ? 1 : 0) - (Key(KeyCode.W) || Key(KeyCode.UpArrow) ? 1 : 0);
            inp.move = new Vector2(mx, my); if (inp.move.sqrMagnitude > 1) inp.move.Normalize();
            inp.aim = view.ScreenToWorldPx(Input.mousePosition);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            inp.mine = Input.GetMouseButton(0) && !overUi;
            inp.attack = (Input.GetMouseButton(1) && !overUi) || Key(KeyCode.Space);
            inp.lamp = KeyDown(KeyCode.Q); inp.vent = KeyDown(KeyCode.E); inp.heal = KeyDown(KeyCode.F);
            for (int i = 0; i < Mathf.Min(9, Data.Crafts.Length); i++) if (KeyDown(KeyCode.Alpha1 + i)) sim.TryCraft(i);
            if (Data.Crafts.Length >= 10 && KeyDown(KeyCode.Alpha0)) sim.TryCraft(9);      // 10번째(산소 포션)는 0 키
            return inp;
        }

        void SetActIcon(string name)
        {
            if (name == curActIcon) return;
            curActIcon = name; actIcon.sprite = UiKit.Icon(name);
        }

        SimInput TouchInput()
        {
            var P = sim.P; var inp = new SimInput { touch = true };
            if (joyId != None)
            {
                Vector2 d = joyPos - joyOrigin; float len = Mathf.Max(d.magnitude, 0.0001f), mag = Mathf.Min(1f, len / JoyR());
                if (mag > 0.18f) { jx = d.x / len * mag; jy = -d.y / len * mag; face = new Vector2(d.x / len, -d.y / len); }   // 화면 y(위)를 월드 y(아래)로 뒤집음
                else { jx = jy = 0; }
            }
            else { jx = jy = 0; }
            inp.move = new Vector2(jx, jy);

            Vector2 tpos;
            bool nearEnemy = sim.NearestTarget(out tpos, attackMode ? 40f : 30f);   // 히스테리시스: 들어올 땐 30, 벗어날 땐 40 (넉백으로 아이콘이 깜빡이지 않게)
            attackMode = nearEnemy;
            SetActIcon(nearEnemy ? "harpoon" : "pick");
            if (hintStep == 0 && (jx != 0 || jy != 0)) { hintStep = 1; OnToast("오른쪽 아래 큰 버튼을 꾹 눌러 채굴하세요", 4f, false); }
            else if (hintStep == 1 && actHeld) hintStep = 2;

            Vector2 aim = new Vector2(P.x + face.x * World.TS * 1.2f, P.y + face.y * World.TS * 1.2f);
            if (tapId != None) { aim = view.ScreenToWorldPx(tapPos); inp.mine = true; }
            else if (actHeld)
            {
                if (nearEnemy)
                {
                    aim = tpos; inp.attack = true; Vector2 dd = tpos - new Vector2(P.x, P.y); if (dd.sqrMagnitude > 0.0001f) face = dd.normalized;
                }
                else { int tx, ty; if (sim.FrontTile(face, out tx, out ty)) { aim = new Vector2(tx * World.TS + 8, ty * World.TS + 8); inp.mine = true; } }
            }
            if (pressLamp || pressVent)       // 설치: 앞 칸이 바닥이면 앞, 아니면 내 칸
            {
                int tx = Mathf.FloorToInt((P.x + face.x * World.TS) / World.TS), ty = Mathf.FloorToInt((P.y + face.y * World.TS) / World.TS);
                if (sim.world.Get(tx, ty) != Tile.Floor) { tx = Mathf.FloorToInt(P.x / World.TS); ty = Mathf.FloorToInt(P.y / World.TS); }
                aim = new Vector2(tx * World.TS + 8, ty * World.TS + 8); inp.mine = false; inp.lamp = pressLamp; inp.vent = pressVent;
            }
            inp.heal = pressHeal; pressLamp = pressVent = pressHeal = false;
            inp.aim = aim; inp.faceDir = face;
            return inp;
        }

        // =========================================================
        // 매 프레임 UI 갱신
        // =========================================================
        public void Tick(float dt)
        {
            // 토스트 페이드
            if (toastTimer > 0) { toastTimer -= dt; if (toastTimer <= 0) toastShown = 0; }
            toastCg.alpha = Mathf.MoveTowards(toastCg.alpha, toastShown, dt * 4f);
            if (storyTimer > 0) { storyTimer -= dt; if (storyTimer <= 0) { storyShown = 0; LayoutToast(); } }
            storyCg.alpha = Mathf.MoveTowards(storyCg.alpha, storyShown, dt * 3f);
            if (newArmed && Time.unscaledTime - newArmedAt > 3f) { newArmed = false; RefreshMenu(); }
            if (victoryGo.activeSelf && Time.unscaledTime > victoryHideAt) victoryGo.SetActive(false);

            // 세로 화면 안내 + 자동 일시정지
            // 가로 전용: 터치/데스크톱 모두 세로 화면이면 안내를 꽉 채워 덮고(UI 가 깨져 보이지 않게) 일시정지한다. 가로로 돌아오면 자동으로 이어 간다.
            bool portrait = Screen.height > Screen.width;
            if (rotateGo.activeSelf != portrait) rotateGo.SetActive(portrait);
            if (portrait && sim.started && !menuOpen) { OpenMenu(); autoPausedByRotate = true; }
            else if (!portrait && autoPausedByRotate) { autoPausedByRotate = false; if (menuOpen) CloseMenu(); }

            hudT -= dt;
            RefreshBars();
            if (hudT <= 0) { hudT = 0.15f; RefreshChips(); RefreshAll(); }

            // 피격/저산소 화면 효과
            float hurt = sim.P.invuln > 0.6f ? 0.18f : 0f; hurtFlash.color = new Color(1, 0.16f, 0.16f, hurt);
            bool inBeacon = Mathf.Sqrt((sim.P.x - World.BX) * (sim.P.x - World.BX) + (sim.P.y - World.BY) * (sim.P.y - World.BY)) < sim.BeaconR;
            lowO2Flash.color = (sim.P.o2 < 10 && !inBeacon && sim.started) ? new Color(0, 0.12f, 0.24f, 0.25f + Mathf.Sin(Time.unscaledTime * 8) * 0.1f) : new Color(0, 0.12f, 0.24f, 0);

            // 미니맵 점
            UpdateMiniDots();
            miniRaw.texture = view.MiniTexture;
            if (sheetOpen) RefreshSheet();
        }

        void SetFill(RectTransform fill, float frac) { fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1); fill.offsetMax = new Vector2(-2, -2); }

        void RefreshBars()
        {
            var P = sim.P;
            SetFill(hpFill, P.hp / sim.MaxHp); SetFill(o2Fill, P.o2 / sim.MaxO2);
            bool lowHp = P.hp <= 3, lowO2 = P.o2 < 10;
            hpTxt.text = "체력 " + Mathf.CeilToInt(P.hp) + "/" + sim.MaxHp + (lowHp ? " !" : "");
            o2Txt.text = "산소 " + Mathf.CeilToInt(P.o2) + "/" + sim.MaxO2 + (lowO2 ? " !" : "");
            bool blink = Mathf.FloorToInt(Time.unscaledTime * 4) % 2 == 0;
            Color ringOk = new Color32(46, 70, 104, 255), ringLow = new Color32(255, 107, 107, 255);
            hpRing.color = lowHp && blink ? ringLow : ringOk; o2Ring.color = lowO2 && blink ? ringLow : ringOk;
            o2Fill.GetComponent<Image>().color = lowO2 ? new Color32(255, 107, 107, 255) : new Color32(58, 167, 232, 255);
            var mini = sim.boss.active ? null : sim.ActiveMini;                     // 심연의 눈이 우선, 아니면 가까운 미니보스
            bossRt.gameObject.SetActive(sim.boss.active || mini != null);
            if (sim.boss.active) { bossLabel.text = "심연의 눈"; bossFill.anchorMax = new Vector2(Mathf.Clamp01(sim.boss.hp / Data.BossHp), 1); bossFill.offsetMax = new Vector2(-2, -2); }
            else if (mini != null) { bossLabel.text = Data.Enemies[(int)mini.type].name; bossFill.anchorMax = new Vector2(Mathf.Clamp01(mini.hp / mini.maxHp), 1); bossFill.offsetMax = new Vector2(-2, -2); }
        }

        void RefreshChips()
        {
            var order = new[] { Item.Scrap, Item.Brass, Item.Crystal, Item.Pearl, Item.Gel };
            for (int i = 0; i < 5; i++) chipTxt[i].text = sim.inv[(int)order[i]].ToString();
        }

        void RefreshAll()
        {
            goalsTxt.text = sim.Chapter + "장 · " + sim.ActiveGoals()[0].TrimStart('·', ' ');       // 집중을 위해 지금 할 일 한 줄만(몇 장인지와 함께)
            int zn = sim.Zone; zoneTxt.text = "구역: " + Data.ZoneName[zn];
            if (zn != lastZone) { if (lastZone != -1 && sim.started) OnToast("구역: " + Data.ZoneName[zn] + " — " + Data.ZoneLore[zn], 3.2f, false); lastZone = zn; }
            // 미니맵 크기 변경 반영
            var ms = miniBig ? Vh(50) : Vh(18);
            if (Mathf.Abs(miniRt.sizeDelta.x - ms) > 0.5f) foreach (var a in layouts) a();

            // 터치 버튼 상태
            healBadge.text = "x" + (sim.inv[(int)Item.Gel] / 2) + (sim.inv[(int)Item.Potion] > 0 ? "·P" + sim.inv[(int)Item.Potion] : "");   // 젤 회복 횟수 · 산소 포션 수 ventBadge.text = sim.inv[(int)Item.Vent].ToString(); lampBadge.text = sim.inv[(int)Item.Lamp].ToString();
            foreach (var b in new[] { healBadge, ventBadge, lampBadge }) LayoutBadge(b);
            SetDim(lampImg, sim.inv[(int)Item.Lamp] <= 0); SetDim(ventImg, sim.inv[(int)Item.Vent] <= 0); SetDim(healImg, sim.inv[(int)Item.Gel] < 2 || sim.P.hp >= sim.MaxHp);

            bool near = sim.NearBeacon && sim.started && !sheetOpen && !menuOpen;
            if (TouchMode && craftPillRt.gameObject.activeSelf != near) craftPillRt.gameObject.SetActive(near);
            if (near)
            {
                bool can = false;
                foreach (var d in Data.Crafts)
                {
                    var cost = d.upgrade ? (sim.NextLevel(d.track) != null ? sim.NextLevel(d.track).cost : null) : d.cost;
                    if (cost != null && sim.CanAfford(cost)) { can = true; break; }
                }
                craftPillImg.color = can && Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0 ? new Color(1.35f, 1.35f, 1.35f, 1) : Color.white;
            }

            // 데스크톱 제작 목록: 비콘 근처에서만 보인다(평소에는 화면을 가리지 않는다)
            bool showCraft = !TouchMode && sim.started && sim.NearBeacon && !sheetOpen && !menuOpen;
            if (craftPanel.activeSelf != showCraft) craftPanel.SetActive(showCraft);
            if (showCraft) RefreshDesktopCraft();
        }

        static void SetDim(Image im, bool dim) { im.color = dim ? new Color(1, 1, 1, 0.45f) : Color.white; }

        void RefreshDesktopCraft()
        {
            // 화면을 덜 가리도록: 지금 살 수 있는 것을 먼저, 최대 4줄만 보여 준다(전체는 숫자 키 1~0). 최대 등급은 숨긴다.
            var ok = new List<string>(); var no = new List<string>();
            for (int i = 0; i < Data.Crafts.Length; i++)
            {
                var d = Data.Crafts[i]; Cost[] cost; string label;
                if (d.upgrade) { var nx = sim.NextLevel(d.track); if (nx == null) continue; cost = nx.cost; label = nx.name; }
                else { cost = d.cost; label = d.label; }
                var sb1 = new StringBuilder("[" + (i + 1 == 10 ? 0 : i + 1) + "] " + label + " ");
                foreach (var c in cost) { int have = sim.inv[(int)c.item]; sb1.Append(have >= c.amount ? "<color=#8fe6a0>" : "<color=#ff9a9a>").Append(Data.ItemName[(int)c.item]).Append(' ').Append(have).Append('/').Append(c.amount).Append("</color> "); }
                (sim.CanAfford(cost) ? ok : no).Add(sb1.ToString());
            }
            var all = new List<string>(ok); all.AddRange(no);
            var sb = new StringBuilder("<color=#9fe8ff>제작</color>  <color=#a9c0d2>숫자 키로 선택</color>\n");
            for (int i = 0; i < Mathf.Min(4, all.Count); i++) sb.Append(all[i]).Append('\n');
            if (all.Count > 4) sb.Append("<color=#a9c0d2>… 외 ").Append(all.Count - 4).Append("종</color>\n");
            craftText.text = sb.ToString();
        }

        void UpdateMiniDots()
        {
            float size = miniRt.rect.width - px * 6; if (size <= 0) return;
            float cell = size / World.W, dot = Mathf.Max(3f, cell * 3f), off = px * 3;
            Action<Image, float, float, bool> put = (im, wx, wy, show) =>
            {
                im.gameObject.SetActive(show);
                UiKit.Place(im.rectTransform, 0, 0, 0.5f, 0.5f, off + (wx / World.TS + 0.5f) * cell, off + (World.H - (wy / World.TS + 0.5f)) * cell, dot, dot);
            };
            put(dotBeacon, World.BX, World.BY, true);
            put(dotPlayer, sim.P.x, sim.P.y, true);
            dotPlayer.color = Mathf.FloorToInt(Time.unscaledTime * 3) % 2 == 1 ? Color.white : new Color32(255, 80, 80, 255);
            put(dotBoss, World.AX, World.AY, !sim.boss.dead);
            for (int i = 0; i < World.StarterVeins.Length; i++)
                put(dotGuide[i], World.StarterVeins[i][0] * World.TS + 8, World.StarterVeins[i][1] * World.TS + 8, sim.ShowEarlyGuide);
            put(dotGuide[World.StarterVeins.Length], World.StarterChest[0] * World.TS + 8, World.StarterChest[1] * World.TS + 8, sim.world.Get(World.StarterChest[0], World.StarterChest[1]) == Tile.Chest);
            for (int i = 0; i < dotMinis.Count; i++)
            {
                var l = World.MiniLairs[i];
                put(dotMinis[i], l[0] * World.TS + 8, l[1] * World.TS + 8, !sim.miniDead[i]);
            }
            for (int i = 0; i < dotRuins.Count; i++)
            {
                var rc = World.RuinCenters[i];
                bool seen = sim.world.explored[World.Idx(rc[0], rc[1])] != 0 || sim.world.explored[World.Idx(rc[0], rc[1] - 3)] != 0;   // 방 가까이 가 봤으면 표시
                bool opened = sim.world.Get(rc[0], rc[1]) != Tile.Chest;
                put(dotRuins[i], rc[0] * World.TS + 8, rc[1] * World.TS + 8, seen && !opened);   // 상자를 열면 사라진다
            }
        }
    }
}
