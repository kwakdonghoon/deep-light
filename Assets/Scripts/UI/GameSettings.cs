using UnityEngine;

namespace Abyss
{
    /// <summary>
    /// 접근성·편의 설정. 기기에 저장되어 다음 실행에도 유지된다.
    /// 밝기: 어두운 구역(심연)이 힘든 사람을 위해 어둠의 짙기를 줄인다. 화면 효과 줄이기: 카메라 흔들림·번쩍임 같은 자극을 약하게 한다.
    /// </summary>
    public static class GameSettings
    {
        const string KeyBright = "deeplight_brightness", KeyReduce = "deeplight_reduce_fx";
        static bool loaded; static int brightLevel; static bool reduceFx;

        static void Ensure()
        {
            if (loaded) return; loaded = true;
            try { brightLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyBright, 0), 0, 2); reduceFx = PlayerPrefs.GetInt(KeyReduce, 0) == 1; } catch (System.Exception) { }
        }
        static void Save() { try { PlayerPrefs.SetInt(KeyBright, brightLevel); PlayerPrefs.SetInt(KeyReduce, reduceFx ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { } }

        /// <summary>0 보통 / 1 조금 밝게 / 2 밝게</summary>
        public static int BrightLevel { get { Ensure(); return brightLevel; } set { Ensure(); brightLevel = Mathf.Clamp(value, 0, 2); Save(); } }
        public static string BrightLabel { get { return BrightLevel == 0 ? "보통" : BrightLevel == 1 ? "조금 밝게" : "밝게"; } }
        /// <summary>어둠의 짙기에 곱하는 값 (보통 1, 밝게 0.7).</summary>
        public static float DarkScale { get { return 1f - 0.15f * BrightLevel; } }
        public static bool ReduceFx { get { Ensure(); return reduceFx; } set { Ensure(); reduceFx = value; Save(); } }
        /// <summary>효과 세기 배율 (보통 1, 줄이기 켜면 0.3).</summary>
        public static float FxScale { get { return ReduceFx ? 0.3f : 1f; } }
    }
}
