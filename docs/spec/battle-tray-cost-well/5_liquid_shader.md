# 5 — 물통 액체 셰이더

## 목적

물통이 "스프라이트를 늘린 것"처럼 보이는 인상을 근본에서 없앤다. 단색 사각을 잘라내는 방식으로는 액체로 읽히지 않는다.

## 왜 `Image.Type.Filled` 로는 안 되는가

**Filled 는 지오메트리를 잘라내므로 셰이더가 수면 위치를 모른다.** 그래서 표면 파형·깊이 음영·수면 하이라이트를 프래그먼트에서 만들 수 없고, 스프라이트가 rect 에 맞춰 늘어나는 문제도 남는다.

`Type.Simple`(풀 rect)로 두고 **`_Fill` uniform 을 프래그먼트에서 잘라내면** 전부 해결된다. 부수 효과로 별도 `WellSurface` Image 와 `ApplySurface()` 가 통째로 사라진다.

## 변경 대상

- `Assets/_Project/Shaders/CostWell_UI.shader` (신규)
- `Assets/_Project/Data/Materials/CostWellLiquid.mat` (신규)
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs` — `wellLiquidMaterial`
- `Assets/_Project/Scripts/UI/CostDisplay.cs`

## 구현

### 셰이더

`UICordShine.shader` 를 골격 템플릿으로 삼는다(스텐실 + `UNITY_UI_CLIP_RECT` 스캐폴드가 이미 완성돼 있다).

프래그먼트가 만드는 것 4가지:

| | 방법 |
|---|---|
| 수면 파형 | 어긋난 사인 2개 합. 한 개면 기계적으로 보인다 |
| 깊이 음영 | 바닥으로 갈수록 `_LiquidBottom` 으로 lerp — 평평한 단색을 깨는 핵심 |
| 수면 하이라이트 | 표면 근처 `smoothstep` 밴드. 별도 Image 불필요 |
| 유리 반사 | 좌측 세로 스트라이프 2줄 |

**빈 통/가득 찬 통에서는 파형 진폭을 0 으로 죽인다**(`waveMask`). 안 그러면 경계에서 파형이 바닥이나 천장을 넘어 삐져나온다.

### 함정 2개 (프로젝트 고유)

- **`Mask` 는 스텐실이다.** `Well` 이 `Mask` 를 쓰므로 `_Stencil` 스캐폴드가 없으면 액체가 둥근 모서리 밖으로 삐져나온다. `UI_Additive.shader` 처럼 스텐실 없는 셰이더를 복사하면 안 된다.
- **`Shader.Find` 금지.** 빌드에 포함된 셰이더만 찾으며, 이 프로젝트는 그 방식으로 한 번 출시 사고를 냈다(`DeployCutscenePlayer.cs:216-224`, 2026-07-15). 머티리얼을 **에셋 참조**로 넘긴다.

### 배선 위치 — 씬이 아니라 config

머티리얼은 `BattleHudTrayConfig.wellLiquidMaterial` 로 넘긴다. 씬의 `SerializeField` 로 하면 배선에 씬 저장이 필요한데, **`SaveScene` 은 사용자의 미저장 in-memory WIP 까지 함께 베이크**한다. SO 는 그 위험이 없다.

미할당이면 예전 방식(`Type.Filled` 단색 채움)으로 폴백한다 — 출렁임만 없고 동작은 같다.

### 머티리얼 인스턴스 수명

프로젝트 관례를 그대로 따른다(`DepthParallaxView` / `DraftCardVfxDriver`): `new Material(src) { hideFlags = HideAndDontSave }` → `Image.material` 할당 → `OnDestroy` 에서 `Destroy`(에디터는 `DestroyImmediate`).

색은 생성 시 1회 push(`_LiquidBottom`/`_LiquidTop`/`_SurfaceColor` ← config), 매 프레임은 `_Fill` 하나만.

## 튜닝 값 (실측 확정)

| 프로퍼티 | 값 | 근거 |
|---|---|---|
| `_WaveAmp` | 0.055 | 초기값 0.018 은 118px 물통에서 진폭 2px 라 육안으로 직선이었다 |
| `_WaveFreq` | 7 | 물통 폭에 파동 1~2개 |
| `_WaveSpeed` | 1.9 | |
| `_DepthShade` | 0.85 | 0.55 는 그라데이션이 거의 안 보였다 |
| `_SurfaceThickness` | 0.07 | |
| `_GlassStrength` | 0.2 | |
| `wellLiquidColor`(= `_LiquidBottom`) | (0.74, 0.34, 0.04) | 밝은 금색이면 위아래 대비가 안 생긴다 |

## 완료 기준

- [x] 셰이더 컴파일 통과 (`isSupported = true`)
- [x] `WellLiquid.type == Simple`, 머티리얼 인스턴스가 `Wassup/UI/CostWell` 를 쓴다
- [x] `Well` 자식이 `WellLiquid` 하나 — `WellSurface` 가 사라졌다
- [x] `_Fill` 이 `CostRuntime` 소수부를 따라간다 (실측 0.550)
- [x] **수면이 직선이 아니라 물결친다** (크롭 확대로 확인)
- [x] 깊이 음영·유리 반사가 보인다
- [ ] Mask 경계 밖으로 액체가 삐져나오지 않는다 (사용자 Play 확인)
- [ ] 실기 성능 — 프래그먼트에 `sin` 2회, 물통 하나뿐이라 무시 가능 수준이나 실기 확인

---

**완료 확인 2026-07-21** — 셰이더 신규 작성·머티리얼 생성·config 배선·Play 실측. 파형/그라데이션/유리반사 모두 동작. 커밋: (아래 참조)
