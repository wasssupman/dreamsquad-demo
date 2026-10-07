# 4 — `Resources` 머티리얼 → SO 참조 · `Shader.Find` 체인 제거

## 목적

런타임 머티리얼 4 는 `Resources.Load<Material>("RuntimeMaterials/…")` 경로 로드다(GUID 참조 0). 「값은 SO 참조로」 원칙 밖이고, somnia 는 `Resources` 폴더명을 어디서든 거절한다(이식 플랜 §3-8). 같은 결의 `Shader.Find` 폴백 체인도 뺀다 — CLAUDE.md 「Unity 함정」.

## 지금

| 호출 | 로드 | 소비처 |
|---|---|---|
| `Rendering/RuntimeMaterialFactory.cs:101` | `RuntimeMaterials/CardCrumpleUI.mat` | 손패 카드 구김 |
| `RuntimeMaterialFactory.cs:119` | `RuntimeMaterials/SolidOpaque.mat` · `SolidTransparent.mat` | 쿼드 유닛 뷰 · 픽업 · 사직서 · 퇴근 비행 폴백 |
| `BattleCoreUnity/View/CoreOverlayMaterial.cs:36` | `RuntimeMaterials/BoardOverlay.mat` | 보드 오버레이 |
| `RuntimeMaterialFactory.cs:20-24, 50-53, 127-130` | `Shader.Find` 체인(Wassup/Tile_Unlit → URP/Unlit → …) | 위 로드가 실패했을 때의 폴백 |
| `Data/Authoring/BlockingHazardPresenter.cs:266-269` | `Shader.Find` 체인 4 | 길막 플레이스홀더(백로그 BCR/8c) |
| `UI/Dreamcatcher/UiCardFaceMesh.cs:67` | `Shader.Find("Wassup/UI/CardCrumple")` | 카드 면 메시 |

## 변경 대상

1. **`Scripts/Data/RuntimeMaterialSet.cs`** — SO. 필드 `boardOverlay` · `cardCrumpleUi` · `solidOpaque` · `solidTransparent` · `blockingHazardPlaceholder`(길막) · `cardFace`(UI 카드 면). 에셋 `Data/Materials/RuntimeMaterialSet.asset` — 머티리얼 4 는 `Assets/Resources/RuntimeMaterials/` → `Data/Materials/Runtime/` 로 `git mv`(GUID 유지).
2. **`RuntimeMaterialFactory`** — static 캐시는 두되 원천은 `Configure(RuntimeMaterialSet)` 로 주입. 주입처는 `BattleDriver.Awake`(단위 0 의 `BattleContent.runtimeMaterials` 로 실어도 된다). `Shader.Find` 체인 전부 삭제 — 주입 전 호출은 `Debug.LogError` + null(조용한 폴백 금지 — 백로그 M2 의 결).
3. `CoreOverlayMaterial` · `BlockingHazardPresenter` · `UiCardFaceMesh` 는 팩토리에서 받는다.
4. `Assets/Resources/` 폴더 삭제. Always Included Shaders(`GraphicsSettings.asset`)의 `CardCrumple_UI` · `Tile_Unlit` 두 줄은 **그대로 둔다** — 머티리얼이 에셋 참조가 되어 없어도 빌드에 들어가지만, 남겨서 해로울 것도 없다(설정 churn 을 안 만든다).
5. 테스트: `RuntimeMaterialSet.asset` 의 슬롯 6 이 null 이 아니고 셰이더가 `Wassup/*` 또는 URP 인 것(EditMode.Assets).

## 완료 기준

- [x] `rg "Resources\.Load|Shader\.Find" Assets/_Project/Scripts` → 0 · `Assets/Resources` 없음
- [ ] Play: 보드 오버레이 · 카드 구김 · 쿼드 유닛 · 길막 플레이스홀더 · 카드 면이 전과 같다
- [x] 커밋 1

## 구현 결과 (2026-10-07) — `5a8c327c9`(타입·에셋은 `c1289bc00`)

- `RuntimeMaterialSet` 슬롯 6 — 머티리얼 4 는 `Data/Materials/Runtime/` 로 이동(GUID 유지), `TexturedOpaque.mat`(Tile_Unlit — 쿼드 유닛 뷰) · `HazardParticle.mat`(URP Particles/Unlit) 신규. `Assets/Resources` 소멸.
- 팩토리는 복제만 하고 `Shader.Find` 사슬을 전부 버렸다(묶음/슬롯이 비면 null + 에러 1회). `BattleDriver` 는 `DefaultExecutionOrder(-100)` + `Awake` 주입. `CreateTransparentTexture` 는 호출처 0 이라 삭제. Always Included Shaders 는 그대로.
- `rg "Resources\.Load|Shader\.Find" Scripts` → 0.
