using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyss
{
    /// <summary>
    /// 게임 진입점. 빈 씬에 이 컴포넌트 하나만 있으면 월드/카메라/UI/오디오를 전부 코드로 만든다.
    /// 명령줄 인자: -seed N (같은 맵 재현, 저장 무시) / -touch (터치 UI 강제) / -nosave (저장하지 않음)
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static int NextSeed;            // 테스트에서 시드를 지정할 때 (0 이면 저장 → 랜덤)
        public static bool NoSave;

        public Sim Sim { get; private set; }
        public GameView View { get; private set; }
        public HudController Hud { get; private set; }
        public SfxPlayer Sfx { get; private set; }

        float saveT = 10f;

        void Awake()
        {
            ParseArgs();
            Application.targetFrameRate = 60;
            ForceLandscape();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            bool fromSave = false; Sim sim = null;
            if (NextSeed == 0 && !NoSave) { sim = SaveSystem.Load(); fromSave = sim != null; }
            if (sim == null) sim = new Sim(NextSeed != 0 ? NextSeed : UnityEngine.Random.Range(1, 1000000000));
            sim.Populate();
            Sim = sim;

            Sfx = new GameObject("Sfx").AddComponent<SfxPlayer>();
            View = new GameObject("GameView").AddComponent<GameView>(); View.Init(sim);
            Hud = new GameObject("HudController").AddComponent<HudController>(); Hud.SaveLoaded = fromSave; Hud.Init(sim, View, Sfx);

            sim.SfxEvent += Sfx.Play;
            sim.SaveRequested += SaveNow;
            Hud.NewGameRequested += NewGame;
        }

        /// <summary>가로 전용으로 고정한다. 기기에서는 세로로 들고 있어도 즉시 가로로 돌리고(좌/우 가로만 허용), 에디터 Game 뷰는 회전시킬 수 없으니 HUD 가 세로 안내로 덮는다.</summary>
        static void ForceLandscape()
        {
            Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
            if (!Application.isMobilePlatform) return;
            Screen.orientation = Screen.height > Screen.width ? ScreenOrientation.LandscapeLeft : ScreenOrientation.AutoRotation;   // 세로로 시작했으면 먼저 가로로 돌린다
        }

        void LateUpdate()
        {
            // 한 번 가로로 돌린 뒤에는 좌/우 가로 자동 회전으로 풀어 준다(기기를 뒤집어도 가로 유지)
            if (Application.isMobilePlatform && Screen.orientation == ScreenOrientation.LandscapeLeft && Screen.width > Screen.height) Screen.orientation = ScreenOrientation.AutoRotation;
        }

        static void ParseArgs()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == "-seed" && i + 1 < a.Length) { int s; if (int.TryParse(a[i + 1], out s)) NextSeed = s; }
                else if (a[i] == "-touch") HudController.ForceTouch = true;
                else if (a[i] == "-nosave") NoSave = true;
            }
            if (NextSeed != 0) NoSave = true;      // 시드를 지정한 실행은 샌드박스: 저장을 덮어쓰지 않는다
        }

        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            SimInput inp = Hud.ReadInput();
            if (!Hud.Paused) Sim.Step(dt, inp);
            Hud.Tick(dt);
            View.Render();
            saveT -= dt;
            if (saveT <= 0) { saveT = 10f; SaveNow(); }
        }

        void SaveNow() { if (!NoSave && Sim != null && Sim.started) SaveSystem.Save(Sim); }

        void OnApplicationPause(bool paused) { if (paused) { SaveNow(); if (Hud != null) Hud.OpenMenu(); } }   // 앱 전환: 저장하고 일시정지 메뉴
        void OnApplicationQuit() { SaveNow(); }

        // 만든 오브젝트를 함께 정리한다(테스트에서 부트스트랩을 지웠을 때 옛 카메라·UI 가 남지 않도록)
        void OnDestroy()
        {
            if (Sfx != null) Destroy(Sfx.gameObject);
            if (View != null) { if (View.Cam != null) Destroy(View.Cam.gameObject); Destroy(View.gameObject); }
            if (Hud != null) Destroy(Hud.gameObject);
        }

        void NewGame()
        {
            SaveSystem.Delete();
            NextSeed = 0;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
