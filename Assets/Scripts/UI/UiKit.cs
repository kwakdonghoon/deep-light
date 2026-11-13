using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abyss
{
    /// <summary>UI 오브젝트에서 포인터 이벤트(멀티터치 포함)를 콜백으로 받는다. 터치/마우스/펜 모두 같은 코드.</summary>
    public sealed class PointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerClickHandler
    {
        public Action<PointerEventData> Down, Up, Drag, Click;
        public void OnPointerDown(PointerEventData e) { if (Down != null) Down(e); }
        public void OnPointerUp(PointerEventData e) { if (Up != null) Up(e); }
        public void OnDrag(PointerEventData e) { if (Drag != null) Drag(e); }
        public void OnPointerClick(PointerEventData e) { if (Click != null) Click(e); }
    }

    public static class Haptics
    {
        public static void Buzz(int ms)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vib = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    if (vib != null) vib.Call("vibrate", (long)ms);
            }
            catch (Exception) { /* 진동 미지원/권한 없음 */ }
#endif
        }
    }

    /// <summary>uGUI 를 코드로 만들기 위한 작은 도우미들. 크기는 전부 물리 픽셀(Canvas 스케일 1)로 다룬다.</summary>
    public static class UiKit
    {
        public static Sprite Frame(string name) { return Resources.Load<Sprite>("Art/Frames/" + name); }
        public static Sprite Icon(string name) { return Resources.Load<Sprite>("Art/Icons/" + name); }

        // ---------- 콘솔 패드 모양 조이스틱 ----------
        static Sprite padBase, padThumb;

        static Sprite MakePadSprite(int n, System.Func<float, float, int, int, Color32> shade)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++) px[y * n + x] = shade(x - (n - 1) / 2f, y - (n - 1) / 2f, x, y);
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>패드 바닥판: 어두운 원판 + 밝은 테두리 + 안쪽 홈 + 상하좌우 삼각 화살표 (콘솔 패드의 아날로그 스틱 자리).</summary>
        public static Sprite PadBase()
        {
            if (padBase != null) return padBase;
            padBase = MakePadSprite(64, (dx, dy, x, y) =>
            {
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 31.5f) return new Color32(0, 0, 0, 0);
                if (r > 30f) return new Color32(6, 14, 26, 235);                       // 바깥 윤곽
                if (r > 27f) return (dx - dy) < 0 ? new Color32(120, 168, 214, 235) : new Color32(52, 86, 128, 235);   // 테두리: 왼쪽 위가 밝다
                // 상하좌우 화살표 (바깥쪽을 향하는 삼각형)
                float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                float along = Mathf.Max(ax, ay), across = Mathf.Min(ax, ay);          // 화살표 축 방향 거리 / 수직 거리
                if (along > 15f && along < 23f && across < (23f - along) * 0.8f + 0.5f) return new Color32(127, 227, 255, 235);
                if (r > 8.5f && r < 10f) return new Color32(30, 56, 92, 235);          // 안쪽 홈
                return new Color32(12, 28, 50, 215);                                   // 판 바닥
            });
            return padBase;
        }

        /// <summary>패드 스틱 캡: 입체감 있는 둥근 버튼 (왼쪽 위가 밝고 오른쪽 아래가 어둡다), 가운데 오목한 홈과 반짝임.</summary>
        public static Sprite PadThumb()
        {
            if (padThumb != null) return padThumb;
            padThumb = MakePadSprite(40, (dx, dy, x, y) =>
            {
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 19.5f) return new Color32(0, 0, 0, 0);
                if (r > 18f) return new Color32(6, 14, 26, 255);
                float lit = Mathf.Clamp01(0.5f + (-dx * 0.55f + dy * 0.75f) / 22f);   // 조명 방향: 왼쪽 위 (y 는 위로 증가)
                Color32 dark = new Color32(34, 64, 104, 255), light = new Color32(150, 196, 238, 255);
                Color32 body = Color32.Lerp(dark, light, lit);
                if (r > 8f && r < 10f) body = Color32.Lerp(body, new Color32(20, 40, 72, 255), 0.55f);          // 가운데 오목한 홈
                if (r < 8f) body = Color32.Lerp(new Color32(22, 44, 78, 255), new Color32(96, 140, 190, 255), lit);   // 홈 안쪽
                if (dx < -6f && dx > -10f && dy > 6f && dy < 9f && r < 15f) body = new Color32(235, 247, 255, 255);   // 반짝이는 하이라이트
                return body;
            });
            return padThumb;
        }

        public static Font Font14, Font11;

        public static void LoadFonts()
        {
            if (Font14 != null) return;
            Font14 = Resources.Load<Font>("Art/Fonts/Galmuri14");
            Font11 = Resources.Load<Font>("Art/Fonts/Galmuri11");
            if (Font14 == null) Font14 = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Font11 == null) Font11 = Font14;
            Font.textureRebuilt += OnFontRebuilt;   // 픽셀 폰트는 점 샘플링해야 또렷하다
            OnFontRebuilt(Font14); OnFontRebuilt(Font11);
        }

        static void OnFontRebuilt(Font f)
        {
            if (f != null && f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Point;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>앵커(한 점) 기준 배치. pos 는 앵커에서의 픽셀 오프셋.</summary>
        public static void Place(RectTransform rt, float ax, float ay, float px_, float py_, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.pivot = new Vector2(px_, py_);
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        }

        public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Img(Transform parent, Sprite sp, Color c, bool ray = false, string name = "img")
        {
            var rt = Rect(name, parent); var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sp; im.color = c; im.raycastTarget = ray; im.preserveAspect = false; return im;
        }

        public static Text Txt(Transform parent, string s, Font font, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter, string name = "txt")
        {
            var rt = Rect(name, parent); var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = c; t.alignment = anchor; t.text = s; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.supportRichText = true;
            return t;
        }

        public static void Outline(Text t, float dist)
        {
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color32(5, 10, 20, 255); o.effectDistance = new Vector2(dist, -dist);
        }

        public static PointerRelay Relay(Component c) { return c.gameObject.AddComponent<PointerRelay>(); }

        /// <summary>코드로 만든 속이 빈 사각 테두리(조이스틱 링용). border 가 9-slice 경계.</summary>
        public static Sprite BoxSprite(int size, int border, Color32 fill, Color32 line)
        {
            var c = new PixelCanvas(size, size);
            c.Fill(0, 0, size, size, line); c.Fill(border, border, size - border * 2, size - border * 2, fill);
            var tex = c.ToTexture();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
