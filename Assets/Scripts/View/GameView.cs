using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UTile = UnityEngine.Tilemaps.Tile;

namespace Abyss
{
    /// <summary>Sim 의 상태를 화면에 그린다 (타일맵, 엔티티, 파티클, 카메라, 조명, 미니맵 텍스처). 규칙은 전혀 모른다.</summary>
    public sealed class GameView : MonoBehaviour
    {
        public const float OrthoSize = 90f / 16f;           // 논리 높이 180px, 1유닛 = 16px

        public Camera Cam { get; private set; }
        public int VW { get; private set; } = 320;           // 논리 가로(px), 화면 비율에 맞춰 288~428
        public Vector2 CamPx { get; private set; }           // 카메라 왼쪽 위 월드 px (정수)
        public Texture2D MiniTexture { get; private set; }

        Sim sim;
        Tilemap ground, decoMap, crackMap;
        readonly Dictionary<long, TileBase> tileCache = new Dictionary<long, TileBase>();
        LightMask light;
        SpriteRenderer lightSr, playerSr, beaconSr, ringSr, altarSr, cursorSr, bossSr;
        PixelCanvas bossCanvas; Texture2D bossTex; Sprite bossSprite;
        Sprite bossBody1, bossBody2, bossIris1, bossIris2, bossLidA, bossLidB; SpriteRenderer bossBodySr, bossIrisSr, bossFlashSr, bossLidSr; bool bossArt;
        Sprite altarOn, altarOff;
        Sprite[,] brackets = new Sprite[2, 2];
        Sprite jellyA, jellyB, anglerSp, urchinSp, crabSp, eelSp, wraithSp, crabKingSp, eelKingSp, lureSp, bulletSp, pixelSp, flashSp, windSp;

        sealed class EnemyView { public SpriteRenderer body, flash, lure, wind; }
        readonly Dictionary<Enemy, EnemyView> enemyViews = new Dictionary<Enemy, EnemyView>();
        readonly List<SpriteRenderer> bulletPool = new List<SpriteRenderer>(), particlePool = new List<SpriteRenderer>(), swingDots = new List<SpriteRenderer>();
        Color32[] miniPixels; bool miniDirty;
        int lastW, lastH;

        static Vector3 Wp(float px, float py) { return new Vector3(px / 16f, -py / 16f, 0); }

        public void Init(Sim s)
        {
            sim = s;
            BuildCameras();
            BuildSprites();
            BuildWorld();
            BuildEntities();
            BuildMinimap();
            light = new LightMask();
            sim.TileChanged += OnTileChanged;
        }

        void OnDestroy() { if (sim != null) sim.TileChanged -= OnTileChanged; }

        // ---------- 카메라 ----------
        void BuildCameras()
        {
            var bg = new GameObject("BackgroundCam").AddComponent<Camera>(); bg.transform.SetParent(transform, false);
            bg.clearFlags = CameraClearFlags.SolidColor; bg.backgroundColor = Color.black; bg.cullingMask = 0; bg.depth = -100;
            var cgo = new GameObject("Main Camera"); cgo.tag = "MainCamera";
            Cam = cgo.AddComponent<Camera>();
            Cam.orthographic = true; Cam.orthographicSize = OrthoSize; Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color32(5, 10, 20, 255); Cam.depth = 0; Cam.nearClipPlane = 0.1f; Cam.farClipPlane = 50f;
            cgo.transform.position = new Vector3(0, 0, -10);
            cgo.AddComponent<AudioListener>();
        }

        void UpdateViewport()
        {
            if (Screen.width == lastW && Screen.height == lastH && light.Texture != null) return;
            lastW = Screen.width; lastH = Screen.height;
            float aspect = (float)Screen.width / Screen.height;
            float tgt = Mathf.Clamp(aspect, 1.6f, 428f / 180f);
            VW = Mathf.RoundToInt(180f * tgt / 2f) * 2;
            float ta = VW / 180f;
            Cam.rect = aspect > ta ? new Rect((1 - ta / aspect) / 2f, 0, ta / aspect, 1) : new Rect(0, (1 - aspect / ta) / 2f, 1, aspect / ta);
            light.Resize(VW);
            lightSr.sprite = light.Sprite;
        }

        // ---------- 스프라이트 ----------
        void BuildSprites()
        {
            var j = PixelArt.Jelly(); jellyA = j[0]; jellyB = j[1];
            anglerSp = PixelArt.Angler(); urchinSp = PixelArt.Urchin(); crabSp = PixelArt.Crab(); eelSp = PixelArt.Eel(); wraithSp = PixelArt.Wraith(); crabKingSp = PixelArt.CrabKing(); eelKingSp = PixelArt.EelKing();
            frameB[EnemyType.Angler] = PixelArt.EnemyFrameB("angler", 0.5f, 3f / 8f); frameB[EnemyType.Urchin] = PixelArt.EnemyFrameB("urchin"); frameB[EnemyType.Crab] = PixelArt.EnemyFrameB("crab");
            frameB[EnemyType.Eel] = PixelArt.EnemyFrameB("eel"); frameB[EnemyType.Wraith] = PixelArt.EnemyFrameB("wraith"); frameB[EnemyType.CrabKing] = PixelArt.EnemyFrameB("crab_king"); frameB[EnemyType.EelKing] = PixelArt.EnemyFrameB("eel_king");
            lureSp = PixelArt.Lure(); bulletSp = PixelArt.Bullet(); pixelSp = PixelArt.WhitePixel();
            flashSp = PixelArt.Rect(10, 10, new Color32(255, 255, 255, 255)); windSp = PixelArt.Rect(16, 6, new Color32(255, 255, 100, 255));
            altarOn = PixelArt.Altar(true); altarOff = PixelArt.Altar(false);
            for (int r = 0; r < 2; r++) for (int o = 0; o < 2; o++) brackets[r, o] = PixelArt.Brackets(r == 1, o);
        }

        SpriteRenderer MakeSr(string name, Sprite sp, int order, Transform parent = null)
        {
            var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = order; return sr;
        }

        // ---------- 월드(타일맵) ----------
        Tilemap MakeMap(Transform parent, string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>(); var tr = go.AddComponent<TilemapRenderer>(); tr.sortingOrder = order; return tm;
        }

        static int Variant(int x, int y) { unchecked { return (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663)) & 3u); } }

        TileBase CachedTile(long key, System.Func<Sprite> make)
        {
            TileBase t;
            if (!tileCache.TryGetValue(key, out t))
            {
                var ut = ScriptableObject.CreateInstance<UTile>(); ut.sprite = make(); ut.colliderType = UTile.ColliderType.None;
                tileCache[key] = t = ut;
            }
            return t;
        }

        /// <summary>두 프레임이 번갈아 도는 장식 타일(해초 일렁임, 램프 깜박임, 환기구 거품, 버섯 맥동). 두 번째 프레임이 없으면 정지 타일.</summary>
        sealed class AnimDecoTile : TileBase
        {
            public Sprite[] frames; public float speed = 1f;
            public override void GetTileData(Vector3Int p, ITilemap tm, ref TileData d) { d.sprite = frames[0]; d.colliderType = UTile.ColliderType.None; }
            public override bool GetTileAnimationData(Vector3Int p, ITilemap tm, ref TileAnimationData d)
            {
                d.animatedSprites = frames; d.animationSpeed = speed; d.animationStartTime = ((p.x * 7 + p.y * 13) & 15) / 16f * frames.Length;   // 칸마다 위상을 달리해 같이 흔들리지 않게
                return true;
            }
        }

        TileBase CachedAnim(long key, System.Func<Sprite> a, string bName, float speed)
        {
            TileBase t;
            if (tileCache.TryGetValue(key, out t)) return t;
            var sa = a(); var sb = PixelArt.DecoProp(bName);
            if (sb == null || sa == null) return CachedTile(key, a);
            var at = ScriptableObject.CreateInstance<AnimDecoTile>(); at.frames = new[] { sa, sb }; at.speed = speed;
            tileCache[key] = at; return at;
        }

        TileBase GroundTile(int x, int y)
        {
            var t = sim.world.Get(x, y); var def = Data.Tiles[(int)t];
            int v = Variant(x, y);
            if (def.solid) { bool edge = !sim.world.IsSolid(x, y - 1); return CachedTile(((int)t * 4 + v) * 2 + (edge ? 1 : 0), () => PixelArt.TileSprite(t, v, edge)); }
            bool shade = sim.world.IsSolid(x, y - 1);
            return CachedTile((0 * 4 + v) * 2 + (shade ? 1 : 0), () => PixelArt.TileSprite(Tile.Floor, v, shade));
        }

        TileBase DecoTile(int x, int y)
        {
            var t = sim.world.Get(x, y); int v = Variant(x, y);
            switch (t)
            {
                case Tile.Lamp: return CachedAnim(9000, PixelArt.Lamp, "deco_lamp_b", 5f);
                case Tile.Vent: return CachedAnim(9001, PixelArt.Vent, "deco_vent_b", 2.2f);
                case Tile.Chest: return CachedTile(9002, PixelArt.Chest);
            }
            if (t != Tile.Floor) return null;
            switch (sim.world.deco[World.Idx(x, y)])
            {
                case 1: return CachedAnim(9100 + v, () => PixelArt.Seaweed(v), "deco_seaweed_" + (v % 3) + "_b", 1.4f);
                case 2: return CachedTile(9200, PixelArt.Shell);
                case 3: return CachedTile(9300, PixelArt.Clam);
                case World.DecoPillar: return CachedTile(9501, () => PixelArt.DecoProp("deco_pillar"));
                case World.DecoSign: return CachedTile(9502, () => PixelArt.DecoProp("deco_sign"));
                case World.DecoBones: return CachedTile(9503, () => PixelArt.DecoProp("deco_bones"));
                case World.DecoFungus: return CachedAnim(9504, () => PixelArt.DecoProp("deco_fungus"), "deco_fungus_b", 1.1f);
            }
            return null;
        }

        TileBase CrackTile(int x, int y)
        {
            int st = sim.crackShown[World.Idx(x, y)];
            if (st <= 0 || !sim.world.IsSolid(x, y)) return null;
            return CachedTile(9400 + st, () => PixelArt.Crack(st));
        }

        static Vector3Int Cell(int x, int y) { return new Vector3Int(x, -y - 1, 0); }

        void RefreshTile(int x, int y)
        {
            if (!World.InBounds(x, y)) return;
            ground.SetTile(Cell(x, y), GroundTile(x, y));
            decoMap.SetTile(Cell(x, y), DecoTile(x, y));
            crackMap.SetTile(Cell(x, y), CrackTile(x, y));
        }

        void BuildWorld()
        {
            var grid = new GameObject("World").AddComponent<Grid>(); grid.cellSize = Vector3.one;
            ground = MakeMap(grid.transform, "Ground", 0); decoMap = MakeMap(grid.transform, "Deco", 1); crackMap = MakeMap(grid.transform, "Cracks", 2);
            int n = World.W * World.H;
            var pos = new Vector3Int[n]; var g = new TileBase[n]; var d = new TileBase[n]; var c = new TileBase[n];
            for (int y = 0; y < World.H; y++)
                for (int x = 0; x < World.W; x++)
                {
                    int i = y * World.W + x; pos[i] = Cell(x, y);
                    g[i] = GroundTile(x, y); d[i] = DecoTile(x, y); c[i] = CrackTile(x, y);
                }
            ground.SetTiles(pos, g); decoMap.SetTiles(pos, d); crackMap.SetTiles(pos, c);
        }

        void OnTileChanged(int x, int y)
        {
            RefreshTile(x, y); RefreshTile(x, y - 1); RefreshTile(x, y + 1);
            if (sim.world.explored[World.Idx(x, y)] != 0) MiniPixel(x, y);
        }

        // ---------- 엔티티 ----------
        void BuildEntities()
        {
            beaconSr = MakeSr("Beacon", PixelArt.Beacon(), 3); beaconSr.transform.position = Wp(World.BX, World.BY);
            ringSr = MakeSr("BeaconRing", PixelArt.BeaconRing(), 2); ringSr.transform.position = Wp(World.BX, World.BY);
            altarSr = MakeSr("Altar", altarOn, 3); altarSr.transform.position = Wp(World.AX, World.AY);
            diverSheet = PixelArt.DiverSheet();
            playerSr = MakeSr("Player", diverSheet != null ? diverSheet[0, 0] : PixelArt.Player(), 8);
            cursorSr = MakeSr("Cursor", brackets[1, 0], 9);
            bossCanvas = new PixelCanvas(64, 64);
            bossTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            bossSprite = Sprite.Create(bossTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
            bossSr = MakeSr("Boss", bossSprite, 6); bossSr.enabled = false;
            bossBody1 = PixelArt.LoadBoss("boss_body"); bossBody2 = PixelArt.LoadBoss("boss_body2");
            bossIris1 = PixelArt.LoadBoss("boss_iris"); bossIris2 = PixelArt.LoadBoss("boss_iris2"); var bossFlashSp = PixelArt.LoadBoss("boss_flash");
            bossArt = bossBody1 != null && bossBody2 != null && bossIris1 != null && bossIris2 != null && bossFlashSp != null;
            if (bossArt)
            {
                bossBodySr = MakeSr("BossBody", bossBody1, 7, bossSr.transform);
                bossIrisSr = MakeSr("BossIris", bossIris1, 8, bossSr.transform);
                bossLidA = PixelArt.LoadBoss("boss_lid_a"); bossLidB = PixelArt.LoadBoss("boss_lid_b");
                bossLidSr = MakeSr("BossLid", bossLidA, 9, bossSr.transform); bossLidSr.enabled = false;
                bossFlashSr = MakeSr("BossFlash", bossFlashSp, 10, bossSr.transform); bossFlashSr.color = new Color(1, 1, 1, 0);
            }
            var caus = PixelArt.Caustics();
            if (caus != null)
            {
                // 물빛 카우스틱: 화면을 덮는 반복 무늬(월드에 정렬)를 아주 옅게 깔고 천천히 흘린다. 어둠 마스크 아래라 빛이 닿는 곳에서만 비친다.
                causticSr = MakeSr("Caustics", caus, 3); causticSr.drawMode = SpriteDrawMode.Tiled; causticSr.size = new Vector2(40, 24);
                causticSr.color = new Color(0.62f, 0.9f, 1f, 0.11f);
            }
            lightSr = MakeSr("Darkness", null, 100);
            for (int i = 0; i < 11; i++) { var d = MakeSr("SwingDot", pixelSp, 8); d.transform.localScale = new Vector3(2, 2, 1); d.enabled = false; swingDots.Add(d); }
        }

        readonly Dictionary<EnemyType, Sprite> frameB = new Dictionary<EnemyType, Sprite>();   // 적의 두 번째 애니메이션 프레임

        Sprite SpriteFor(Enemy e)
        {
            Sprite alt;
            if (frameB.TryGetValue(e.type, out alt) && alt != null && Mathf.FloorToInt(e.t * (e.type == EnemyType.Urchin ? 1.6f : e.type == EnemyType.CrabKing ? 2.2f : 3f)) % 2 == 1) return alt;
            return SpriteForA(e);
        }

        Sprite SpriteForA(Enemy e)
        {
            switch (e.type)
            {
                case EnemyType.Jelly: return (Mathf.FloorToInt(e.t * 3) % 2 == 0) ? jellyA : jellyB;
                case EnemyType.Angler: return anglerSp;
                case EnemyType.Urchin: return urchinSp;
                case EnemyType.Wraith: return wraithSp;
                case EnemyType.CrabKing: return crabKingSp;
                case EnemyType.EelKing: return eelKingSp;
                case EnemyType.Crab: return crabSp;
                default: return eelSp;
            }
        }

        EnemyView CreateEnemyView(Enemy e)
        {
            var v = new EnemyView { body = MakeSr("Enemy_" + e.type, SpriteFor(e), 5) };
            v.flash = MakeSr("Flash", flashSp, 5, v.body.transform); v.flash.color = new Color(1, 1, 1, 0.7f); v.flash.sortingOrder = 6; v.flash.enabled = false;
            v.wind = MakeSr("Wind", windSp, 5, v.body.transform); v.wind.color = new Color(1, 1, 0.4f, 0.6f); v.wind.sortingOrder = 6; v.wind.enabled = false;
            if (e.type == EnemyType.Angler) { v.lure = MakeSr("Lure", lureSp, 6, v.body.transform); }
            if (e.elite) { v.body.color = new Color(1f, 0.82f, 0.4f, 1f); v.body.transform.localScale = new Vector3(1.35f, 1.35f, 1f); }   // 정예: 금빛으로 더 크다
            return v;
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int i, Sprite sp, int order)
        {
            while (pool.Count <= i) { var sr = MakeSr("pooled", sp, order); sr.enabled = false; pool.Add(sr); }
            return pool[i];
        }

        SpriteRenderer causticSr;
        int ringR = 80;
        Sprite[,] diverSheet;
        int diverDir = 0;     // 0 아래 1 위 2 옆
        float diverPhase;     // 수영 발차기 위상
        float diverRot;       // 이동 방향으로 도는 몸의 각도(도)
        bool diverFlip;       // 왼쪽을 보는 중인지

        /// <summary>방향(아래/위/옆)과 프레임(서기/걷기/공격)을 골라 잠수부 스프라이트를 바꾼다. 시뮬의 y 는 아래로 증가한다.</summary>
        void UpdateDiverFrame(Player P)
        {
            bool moving = Mathf.Abs(P.vx) + Mathf.Abs(P.vy) > 4f;
            bool atk = P.swing > 0;
            float ax = 0, ay = 0; bool have = false;
            if (atk) { ax = Mathf.Cos(P.swingAng); ay = Mathf.Sin(P.swingAng); have = true; }
            else if (moving) { ax = P.vx; ay = P.vy; have = true; }
            // 수영 자세는 항상 옆모습(머리 오른쪽, 오리발이 뒤로 길게)을 쓰고, 이동 방향으로 몸을 돌린다.
            // 위/아래로 갈 때도 똑바로 선 채 걷는 것이 아니라 머리를 앞으로 한 채 헤엄치게 된다. (시뮬의 y 는 아래로 증가 → 화면 위쪽은 -y)
            diverDir = 2;
            bool wantLeft = (have && Mathf.Abs(ax) > 0.05f) ? ax < 0 : P.face < 0;
            if (wantLeft != diverFlip) { diverRot = -diverRot; diverFlip = wantLeft; }   // 좌우를 뒤집을 때 각도도 같이 뒤집어 끊김이 없게
            playerSr.flipX = diverFlip;
            float target = 0f;
            if (have) target = Mathf.Clamp(Mathf.Atan2(-ay, Mathf.Abs(ax)) * Mathf.Rad2Deg, -90f, 90f) * (diverFlip ? -1f : 1f);
            diverRot = Mathf.MoveTowardsAngle(diverRot, target, 480f * Time.deltaTime);   // 방향이 바뀌면 부드럽게 돌아간다 (멈추면 수평으로 눕는다)
            playerSr.transform.rotation = Quaternion.Euler(0, 0, diverRot);
            int f;
            if (atk) f = P.swing > 0.09f ? 9 : 8;                                             // 찌르기 → 거두기
            else if (moving)
            {
                float spdK = Mathf.Clamp(Mathf.Sqrt(P.vx * P.vx + P.vy * P.vy) / 55f, 0.7f, 1.8f);   // 빠를수록 발차기도 빠르게
                diverPhase += Time.deltaTime * 11f * spdK;
                f = 2 + Mathf.FloorToInt(diverPhase) % 6;                                     // 수영 6프레임(플러터 킥)
            }
            else f = Mathf.FloorToInt(Time.time * 1.6f) % 2;                                  // 대기: 느린 숨쉬기 2프레임
            playerSr.sprite = diverSheet[diverDir, f];
        }

        // ---- 비콘 길잡이: 비콘이 화면 밖일 때, 먼 등대 불빛처럼 화면 가장자리에서 그쪽 방향으로 은은하게 빛난다. 산소가 줄면 붉은빛으로 빠르게 깜박인다. ----
        readonly List<SpriteRenderer> compassPool = new List<SpriteRenderer>();
        Sprite compassGlowSp, compassArrowSp;
        void UpdateBeaconCompass(Player P)
        {
            bool show = sim.started;
            Vector3 vp = Cam.WorldToViewportPoint(Wp(World.BX, World.BY));
            if (vp.z <= 0f || (vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f)) show = false;     // 화면 안에 있으면 필요 없다
            if (!show) { for (int i = 0; i < compassPool.Count; i++) compassPool[i].enabled = false; return; }
            if (compassGlowSp == null) compassGlowSp = IntroSequence.MakeGlow();
            float dx = vp.x - 0.5f, dy = vp.y - 0.5f, m = 0.07f;
            float t = Mathf.Min(dx != 0f ? (0.5f - m) / Mathf.Abs(dx) : float.MaxValue, dy != 0f ? (0.5f - m) / Mathf.Abs(dy) : float.MaxValue);
            Vector3 w = Cam.ViewportToWorldPoint(new Vector3(0.5f + dx * t, 0.5f + dy * t, 10f)); w.z = 0f;
            float o2 = sim.MaxO2 > 0 ? P.o2 / sim.MaxO2 : 1f;
            bool low = o2 < 0.4f;
            float pulse = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 0.7f + 0.3f * Mathf.Sin(Time.time * 2f);
            Color c = low ? new Color(1f, 0.6f, 0.4f, 1f) : new Color(0.6f, 0.92f, 1f, 1f);
            var g = Pooled(compassPool, 0, compassGlowSp, 102); g.enabled = true;
            g.transform.position = w; g.transform.localScale = Vector3.one * (2.4f + (low ? 0.5f : 0f) * pulse); c.a = 0.55f * pulse; g.color = c;
            // 빛 안쪽(화면 중심 쪽)에 비콘 방향을 가리키는 작은 화살표
            if (compassArrowSp == null) compassArrowSp = PixelArt.FromRows(new[] { "..x..", ".xxx.", "xxxxx", ".xxx." }, new Dictionary<char, Color32> { { 'x', new Color32(255, 255, 255, 255) } }).ToSprite();
            Vector2 dv = new Vector2(dx, dy).normalized;
            Pooled(compassPool, 1, pixelSp, 103);   // 풀은 번호 순서대로 만들어지므로 중심점(1번)을 먼저 확보한다
            var ar = Pooled(compassPool, 2, compassArrowSp, 103); ar.enabled = true;
            ar.transform.position = w - new Vector3(dv.x, dv.y, 0f) * 0.9f; ar.transform.localScale = Vector3.one * 2.2f;
            ar.transform.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg - 90f);
            var arc = c; arc.a = 0.9f * pulse; ar.color = arc;
            var core = Pooled(compassPool, 1, pixelSp, 103); core.enabled = true;
            core.transform.position = w; core.transform.localScale = new Vector3(4f, 4f, 1f); c.a = 0.95f * pulse; c = Color.Lerp(c, Color.white, 0.5f); c.a = 0.95f * pulse; core.color = c;
        }

        // ---- 목표 반짝임: 초반 목표(광맥·해파리·상자·소굴)가 어둠 속에서 가까워지면 희미하게 반짝여 '저기구나' 하고 알게 한다 (화면 화살표 없이 세계 안에서 유도) ----
        readonly List<SpriteRenderer> glintPool = new List<SpriteRenderer>();
        void UpdateGoalGlint(Player P)
        {
            float gx = 0, gy = 0; bool on = sim.started && sim.GoalTarget(out gx, out gy);   // out 변수를 먼저 0 으로 초기화해 한 번만 호출한다
            float dx = gx - P.x, dy = gy - P.y, dist = Mathf.Sqrt(dx * dx + dy * dy);
            on = on && dist > 12f && dist < 240f && !(Mathf.Abs(gx - World.BX) < 1f && Mathf.Abs(gy - World.BY) < 1f);   // 비콘 목표는 이미 빛나고, 너무 멀면 보이지 않는다
            if (!on) { for (int i = 0; i < glintPool.Count; i++) glintPool[i].enabled = false; return; }
            float near = 1f - Mathf.Clamp01((dist - 40f) / 200f);                                  // 가까울수록 또렷하게
            float tw = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 2.6f));                    // 반짝
            float a = Mathf.Clamp01(0.25f + 0.75f * near) * tw;
            float[] ox = { 0, -3, 3, 0, 0 }, oy = { 0, 0, 0, -3, 3 };
            for (int i = 0; i < 5; i++)
            {
                var sr = Pooled(glintPool, i, pixelSp, 101); sr.enabled = true;
                float s = i == 0 ? 2f : 1f;
                sr.transform.position = Wp(Mathf.Round(gx + ox[i]), Mathf.Round(gy + oy[i])); sr.transform.localScale = new Vector3(s, s, 1);
                sr.color = new Color(1f, 0.9f, 0.55f, a * (i == 0 ? 1f : 0.7f));
            }
            for (int i = 5; i < glintPool.Count; i++) glintPool[i].enabled = false;
        }

        // ---- 물고기 떼 장식: 플레이어 주변 바닥 위를 약하게 떠다니는 작은 물고기 무리 (시뮬과 무관한 순수 연출, 벽은 피해 다닌다) ----
        sealed class FishSchool { public int n = 5; public float x, y, ang, spd, turn, tt; public float[] ox = new float[5], oy = new float[5], ph = new float[5]; }
        readonly List<FishSchool> schools = new List<FishSchool>();
        readonly List<SpriteRenderer> fishPool = new List<SpriteRenderer>();
        Sprite fishSp;
        const int SchoolCount = 2;                 // 동시에 떠다니는 무리 수 (전엔 4 → 너무 자주 보여서 절반으로)
        float fishNextSpawn;                       // 새 무리가 나타날 수 있는 가장 이른 시각 (한 무리가 사라진 뒤 한참 있다가 다시 나온다)

        bool FishFloor(float px, float py)
        {
            int tx = Mathf.FloorToInt(px / World.TS), ty = Mathf.FloorToInt(py / World.TS);
            return World.InBounds(tx, ty) && sim.world.Get(tx, ty) == Tile.Floor;
        }

        void UpdateFishSchools(Player P)
        {
            if (fishSp == null) fishSp = TitleScene.MakeFish();
            float dt = Time.deltaTime;
            for (int i = schools.Count - 1; i >= 0; i--)   // 너무 멀어진 무리는 거둔다 (아래에서 가까운 곳에 새로 생긴다)
            {
                var s = schools[i]; float dx = s.x - P.x, dy = s.y - P.y;
                if (dx * dx + dy * dy > 300f * 300f) schools.RemoveAt(i);
            }
            for (int tries = 0; schools.Count < SchoolCount && tries < 12 && Time.time >= fishNextSpawn; tries++)
            {
                float a = Random.value * 6.2832f, d = 130f + Random.value * 110f, sx = P.x + Mathf.Cos(a) * d, sy = P.y + Mathf.Sin(a) * d;   // 화면 가장자리 근처에서 나타난다
                if (!FishFloor(sx, sy)) continue;
                var s = new FishSchool { n = Random.Range(3, 6), x = sx, y = sy, ang = Random.value * 6.2832f, spd = 10f + Random.value * 8f, tt = 1f + Random.value * 2f };
                for (int k = 0; k < 5; k++) { s.ox[k] = -k * 4f + (Random.value - 0.5f) * 3f; s.oy[k] = (Random.value - 0.5f) * 12f; s.ph[k] = Random.value * 6.28f; }
                schools.Add(s);
                fishNextSpawn = Time.time + 12f + Random.value * 18f;   // 다음 무리는 12~30초 뒤에야 나타난다 (사라지자마자 다시 나오지 않게)
            }
            int n = 0;
            foreach (var s in schools)
            {
                s.tt -= dt; if (s.tt <= 0f) { s.turn = (Random.value - 0.5f) * 1.6f; s.tt = 1f + Random.value * 2f; }
                s.ang += s.turn * dt;
                float cx = Mathf.Cos(s.ang), cy = Mathf.Sin(s.ang);
                float nx = s.x + cx * s.spd * dt, ny = s.y + cy * s.spd * dt;
                if (FishFloor(nx + cx * 10f, ny + cy * 10f) && FishFloor(nx, ny)) { s.x = nx; s.y = ny; }
                else { s.ang += 2.0f + Random.value * 1.4f; s.tt = 0.5f; }   // 벽이 나오면 방향을 크게 튼다
                for (int k = 0; k < s.n; k++)
                {
                    float wob = Mathf.Sin(Time.time * 3f + s.ph[k]) * 1.2f;
                    float fx = s.x + cx * s.ox[k] - cy * (s.oy[k] + wob), fy = s.y + cy * s.ox[k] + cx * (s.oy[k] + wob);
                    var sr = Pooled(fishPool, n++, fishSp, 4); sr.enabled = true;
                    sr.transform.position = Wp(Mathf.Round(fx), Mathf.Round(fy));
                    sr.flipX = cx > 0f;   // 물고기 그림은 기본이 왼쪽을 본다
                    sr.color = new Color(0.62f, 0.86f, 1f, 0.4f);   // 더 옅게
                }
            }
            for (int i = n; i < fishPool.Count; i++) fishPool[i].enabled = false;
        }

        // ---- 발쪽 거품: 헤엄칠 때 발(오리발) 뒤에서 작은 거품이 뽀글뽀글 올라온다 (시뮬과 무관한 순수 연출) ----
        struct FootBubble { public float x, y, vx, vy, life, max, size; }
        readonly List<FootBubble> footBubbles = new List<FootBubble>();
        readonly List<SpriteRenderer> footPool = new List<SpriteRenderer>();
        float footT;

        void UpdateFootBubbles(Player P)
        {
            float dt = Time.deltaTime;
            float sp = Mathf.Sqrt(P.vx * P.vx + P.vy * P.vy);
            if (sim.started && sp > 4f && dt > 0f)
            {
                footT -= dt;
                while (footT <= 0f)
                {
                    footT += Mathf.Lerp(0.16f, 0.07f, Mathf.Clamp01(sp / 70f)) * (0.7f + 0.6f * Random.value);   // 빠를수록 더 자주
                    float nx = P.vx / sp, ny = P.vy / sp, jit = (Random.value - 0.5f) * 5f;
                    float big = Random.value < 0.3f ? 2f : 1f;
                    footBubbles.Add(new FootBubble { x = P.x - nx * 6f - ny * jit, y = P.y + 4f - ny * 6f + nx * jit, vx = (Random.value - 0.5f) * 8f - nx * 6f, vy = -(12f + Random.value * 12f), life = 0.7f + Random.value * 0.5f, max = 1.2f, size = big });
                }
            }
            for (int i = footBubbles.Count - 1; i >= 0; i--)
            {
                var b = footBubbles[i]; b.life -= dt; if (b.life <= 0f) { footBubbles.RemoveAt(i); continue; }
                b.x += b.vx * dt + Mathf.Sin((b.life + i) * 9f) * 3f * dt; b.y += b.vy * dt; b.vx *= 0.97f; footBubbles[i] = b;
            }
            int n = footBubbles.Count;
            for (int i = 0; i < footPool.Count; i++) if (i >= n) footPool[i].enabled = false;
            for (int i = 0; i < n; i++)
            {
                var b = footBubbles[i]; var sr = Pooled(footPool, i, pixelSp, 7); sr.enabled = true;
                sr.transform.position = Wp(Mathf.Round(b.x), Mathf.Round(b.y)); sr.transform.localScale = new Vector3(b.size, b.size, 1);
                sr.color = new Color(0.78f, 0.94f, 1f, Mathf.Clamp01(b.life / 0.45f) * 0.85f);
            }
        }

        void UpdateEntities()
        {
            var P = sim.P;
            // 비콘 확장으로 안전지대가 커지면 점선 원도 다시 그린다
            int wantR = Mathf.RoundToInt(sim.BeaconR);
            if (wantR != ringR) { ringR = wantR; ringSr.sprite = PixelArt.BeaconRing(ringR); }
            // 플레이어
            float bobY = Mathf.Round(Mathf.Sin(P.bob));
            playerSr.transform.position = Wp(P.x, P.y + bobY); playerSr.flipX = P.face < 0;
            if (diverSheet != null) UpdateDiverFrame(P);
            UpdateFootBubbles(P);
            UpdateFishSchools(P);
            UpdateGoalGlint(P);
            UpdateBeaconCompass(P);
            // 비콘 깜빡임: 미세하게 떨리고, 몇 초마다 '깜빡-깜빡' 점멸 (색만 바뀌며 조명·규칙에는 영향 없음)
            { float ph = Time.time % 4.5f, dip = (ph < 0.10f || (ph > 0.26f && ph < 0.36f)) ? 0.3f : 1f, f = (0.9f + 0.1f * Mathf.Sin(Time.time * 17f) * Mathf.Sin(Time.time * 7.3f)) * dip; beaconSr.color = new Color(f, f, f, 1f); }
            playerSr.color = (P.invuln > 0 && Mathf.FloorToInt(P.invuln * 20) % 2 == 0) ? new Color(1, 1, 1, 0.4f) : Color.white;

            // 휘두르기
            for (int i = 0; i < swingDots.Count; i++) swingDots[i].enabled = false;
            if (P.swing > 0)
            {
                float k = P.swing / 0.16f; int idx = 0;
                for (int s = 8; s <= 28; s += 2)
                {
                    float aa = P.swingAng + (k - 0.5f) * 0.8f;
                    var d = swingDots[idx++]; d.enabled = true;
                    d.transform.position = Wp(P.x + Mathf.Cos(aa) * s, P.y + Mathf.Sin(aa) * s); d.color = new Color(1, 1, 1, 0.5f + k * 0.4f);
                }
            }

            // 적
            var dead = new List<Enemy>();
            foreach (var kv in enemyViews) if (!sim.enemies.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var e in dead) { Destroy(enemyViews[e].body.gameObject); enemyViews.Remove(e); }
            foreach (var e in sim.enemies)
            {
                EnemyView v; if (!enemyViews.TryGetValue(e, out v)) { v = CreateEnemyView(e); enemyViews[e] = v; }
                float by = e.type == EnemyType.Urchin ? 0 : Mathf.Round(Mathf.Sin(e.t * 3));
                v.body.transform.position = Wp(e.x, e.y + by); v.body.sprite = SpriteFor(e);
                if (e.type == EnemyType.Angler || e.type == EnemyType.Eel || e.type == EnemyType.EelKing) v.body.flipX = e.faceX > 0;
                if (e.type == EnemyType.Wraith) v.body.color = new Color(1, 1, 1, 0.6f + 0.3f * Mathf.Sin(e.t * 3f));   // 반투명하게 일렁인다
                v.flash.enabled = e.flash > 0;
                if (v.wind != null) v.wind.enabled = e.wind > 0 && Mathf.FloorToInt(e.t * 16) % 2 == 1;
                if (v.lure != null) v.lure.transform.localPosition = new Vector3((e.faceX > 0 ? 8f : -8f) / 16f, 9f / 16f, 0);
            }

            // 보스 / 제단
            var b = sim.boss;
            bossSr.enabled = b.active;
            if (bossArt) { bossBodySr.enabled = b.active; bossIrisSr.enabled = b.active; bossFlashSr.enabled = b.active; if (!b.active) bossLidSr.enabled = false; }
            if (b.active)
            {
                Vector2 look = new Vector2(P.x - b.x, P.y - b.y);
                bool angry = b.hp < Data.BossHp * 0.5f;
                if (bossArt)
                {
                    PixelArt.DrawBossArms(bossCanvas, b.t, angry); bossCanvas.Upload(bossTex);
                    bossBodySr.sprite = angry ? bossBody2 : bossBody1;
                    bossIrisSr.sprite = angry ? bossIris2 : bossIris1;
                    float dl = look.magnitude; if (dl < 0.0001f) { look = Vector2.right; dl = 1; }
                    // 눈동자는 플레이어 쪽으로 최대 4px, 픽셀 격자에 맞춘다(시뮬 y 는 아래로 증가)
                    bossIrisSr.transform.localPosition = new Vector3(Mathf.Round(look.x / dl * 4f) / 16f, -Mathf.Round(look.y / dl * 4f) / 16f, 0);
                    // 피격이면 하얗게, 돌진 예고(windup)면 몸이 깜빡이며 떨린다
                    float flashA = b.flash > 0 ? 0.85f : (b.windup > 0 ? (Mathf.FloorToInt(b.windup * 16f) % 2 == 0 ? 0.55f : 0.1f) : 0f);
                    bossFlashSr.color = new Color(1, 1, 1, flashA);
                    if (b.windup > 0) bossSr.transform.position += new Vector3(((Mathf.FloorToInt(b.windup * 30f) % 2) * 2 - 1) / 16f, 0, 0);
                    // 눈 깜빡임: 4.2초마다 반쯤 감김 → 완전히 감김 → 반쯤 감김
                    float bt = b.t % 4.2f; Sprite lid = null;
                    if (bossLidA != null && bossLidB != null && bt < 0.36f) lid = (bt < 0.1f || bt >= 0.26f) ? bossLidA : bossLidB;
                    bossLidSr.enabled = lid != null; if (lid != null) bossLidSr.sprite = lid;
                }
                else { PixelArt.DrawBoss(bossCanvas, b.t, look, b.flash > 0); bossCanvas.Upload(bossTex); }
                bossSr.transform.position = Wp(b.x, b.y);
            }
            altarSr.enabled = !b.dead && !b.active;
            if (altarSr.enabled) altarSr.sprite = Mathf.Sin(sim.time * 3f) > 0 ? altarOn : altarOff;

            // 탄
            for (int i = 0; i < bulletPool.Count; i++) bulletPool[i].enabled = false;
            for (int i = 0; i < sim.bullets.Count; i++)
            {
                var sr = Pooled(bulletPool, i, bulletSp, 7); sr.enabled = true; sr.transform.position = Wp(sim.bullets[i].x, sim.bullets[i].y);
            }

            // 파티클
            int pn = sim.particles.Count;
            for (int i = 0; i < particlePool.Count; i++) if (i >= pn) particlePool[i].enabled = false;
            for (int i = 0; i < pn; i++)
            {
                var p = sim.particles[i]; var sr = Pooled(particlePool, i, pixelSp, 4); sr.enabled = true;
                sr.transform.position = Wp(Mathf.Round(p.x), Mathf.Round(p.y)); sr.transform.localScale = new Vector3(p.size, p.size, 1);
                var c = (Color)p.color; c.a = Mathf.Min(1f, p.life / p.max * 1.5f); sr.color = c;
            }

            // 커서
            bool vis = sim.started && World.InBounds(sim.cursorTx, sim.cursorTy);
            cursorSr.enabled = vis;
            if (vis)
            {
                int o = sim.cursorReach ? (Mathf.FloorToInt(sim.time * 2f) % 2) : 0;
                cursorSr.sprite = brackets[sim.cursorReach ? 1 : 0, o];
                cursorSr.transform.position = Wp(sim.cursorTx * 16 + 8, sim.cursorTy * 16 + 8);
            }
        }

        // ---------- 미니맵 ----------
        void BuildMinimap()
        {
            MiniTexture = new Texture2D(World.W, World.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            miniPixels = new Color32[World.W * World.H];
            for (int i = 0; i < miniPixels.Length; i++) miniPixels[i] = new Color32(5, 8, 15, 255);
            for (int y = 0; y < World.H; y++) for (int x = 0; x < World.W; x++) if (sim.world.explored[World.Idx(x, y)] != 0) MiniPixelRaw(x, y);
            MiniTexture.SetPixels32(miniPixels); MiniTexture.Apply(false);
        }

        void MiniPixelRaw(int x, int y) { miniPixels[(World.H - 1 - y) * World.W + x] = Data.Tiles[sim.world.tiles[World.Idx(x, y)]].mini; }
        void MiniPixel(int x, int y) { MiniPixelRaw(x, y); miniDirty = true; }

        void UpdateMinimap()
        {
            foreach (int i in sim.exploredDirty) MiniPixelRaw(i % World.W, i / World.W);
            if (sim.exploredDirty.Count > 0) { miniDirty = true; sim.exploredDirty.Clear(); }
            if (miniDirty) { MiniTexture.SetPixels32(miniPixels); MiniTexture.Apply(false); miniDirty = false; }
        }

        // ---------- 매 프레임 ----------
        public void Render()
        {
            UpdateViewport();
            var P = sim.P;
            int cx = Mathf.RoundToInt(Mathf.Clamp(P.x - VW / 2f, 0, World.W * 16 - VW));
            int cy = Mathf.RoundToInt(Mathf.Clamp(P.y - 90f, 0, World.H * 16 - 180));
            if (sim.camShake > 0 && !GameSettings.ReduceFx) { cx += Random.Range(-2, 3); cy += Random.Range(-2, 3); }   // '화면 효과 줄이기'를 켜면 흔들지 않는다
            CamPx = new Vector2(cx, cy);
            Cam.transform.position = new Vector3((cx + VW / 2f) / 16f, -(cy + 90f) / 16f, -10f);
            lightSr.transform.position = new Vector3((cx + VW / 2f) / 16f, -(cy + 90f) / 16f, 0);
            if (causticSr != null)
            {
                // 무늬 한 칸이 64px(4유닛)라 위치를 4유닛 격자에 맞추면 월드에 고정된다. 거기에 천천히 흐르는 오프셋을 더한다.
                float ox = (Time.time * 5f % 64f) / 16f, oy = -(Time.time * 2.5f % 64f) / 16f;
                causticSr.transform.position = new Vector3(Mathf.Round((cx + VW / 2f) / 16f / 4f) * 4f + ox, Mathf.Round(-(cy + 90f) / 16f / 4f) * 4f + oy, 0);
            }
            UpdateEntities();
            UpdateMinimap();
            light.Update(sim, cx, cy);
        }

        /// <summary>화면(픽셀) 좌표를 월드 px 로. 마우스 조준에 쓴다.</summary>
        public Vector2 ScreenToWorldPx(Vector2 screen)
        {
            Vector3 w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return new Vector2(w.x * 16f, -w.y * 16f);
        }

        /// <summary>월드 px 를 화면(픽셀) 좌표로. 터치 정밀 채굴/조준에 쓴다.</summary>
        public Vector2 WorldPxToScreen(Vector2 px)
        {
            Vector3 s = Cam.WorldToScreenPoint(new Vector3(px.x / 16f, -px.y / 16f, 0));
            return new Vector2(s.x, s.y);
        }
    }
}
