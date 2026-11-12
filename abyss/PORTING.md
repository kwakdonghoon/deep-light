# Abyss Keeper → Unity 이식 메모

## 파일 대응
| 웹 프로토타입 | Unity |
|---|---|
| `data.js` `TILE_DEF` | `TileDef` ScriptableObject (hp, tier, drop, glow, 스프라이트) |
| `data.js` `UP` / `CRAFTS` | `UpgradeTrack` / `Recipe` ScriptableObject |
| `data.js` `ENEMY_DEF`, `BOSS_DEF` | `EnemyDef` ScriptableObject + 프리팹 |
| `data.js` `TUNE` | 단일 `GameTuning` ScriptableObject |
| `game.js` `generate()` | `WorldGenerator` (시드 기반, CA 4회 + 광맥 군집). 로직 그대로 이식 가능 |
| `tiles/dmg/deco` 배열 | `Tilemap`(타일) + `Dictionary<Vector3Int,float>`(파괴 진행도) |
| `renderLight()` | URP 2D `Light2D` (Point, 4단 밴드는 램프 텍스처로 대체) + Global Light 어둡게 |
| 320×180 캔버스 | Pixel Perfect Camera (Ref. Resolution 320×180, Assets PPU 16) |
| `moveBox/collides` | `Rigidbody2D` + `TilemapCollider2D` (또는 현재 방식 그대로 수동 AABB) |
| DOM HUD | uGUI / UI Toolkit |

## 이식 순서 (권장)
1. 타일맵 + 월드 생성 + 카메라(Pixel Perfect)
2. 플레이어 이동 / 채굴 / 인벤토리
3. Light2D 어둠 + 산소(비콘·환기구) 시스템
4. 제작 UI, 업그레이드 트랙
5. 적 3종 → 보스 (상태머신: Drift / Charge / RingFire)
6. 미니맵(RenderTexture 또는 탐험 마스크 텍스처)

## 주의
- 스프라이트는 현재 코드의 문자열 픽셀맵이다. Unity에서는 Aseprite 등으로 16px 타일셋 / 스프라이트시트로 새로 그린다.
- 충돌은 타일 단위 AABB라 단순하다. `Rigidbody2D`로 바꾸면 물에 뜬 듯한 관성 값(`dt*7` 보간)을 다시 튜닝해야 한다.
- 월드 크기 160×160은 한 번에 Tilemap에 올려도 문제없다. 키울 때는 청크 로딩이 필요하다.
