# 1 — 상세 패널 뷰 (SquadUnitDetailView)

## 목적

좌 1/3 상세 패널의 **아트-백드롭 + 통합 카드**를 그리는 뷰 컴포넌트. `Show(DefenderUnitData, bool inSquad)` 한 방으로 (a) 라이브 Spine을 SkeletonGraphic에 바인딩하고 (b) 이름·등급·클래스·코스트·스탯·설명문·[출전] 카드를 채운다. 씬 배선(SkeletonGraphic 저작)은 unit 5.

**핵심 전제**: `SceneTransition.cs`가 증명한 런타임 SkeletonGraphic 패턴을 미러링 — SkeletonGraphic은 **씬에 미리 저작**(머티리얼/Canvas 채널 셋업 완료)되고, 코드는 `skeletonDataAsset` 교체 + `Initialize(true)` + `SpineCombinedSkinCache.Apply(Skeleton, data)` + idle 재생만. UGUI 런타임 머티리얼/채널 함정 회피.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` (`Wassup.UI`)
- 신규 `Assets/_Project/Scripts/Data/UnitLabels.cs` (`Wassup.Data`) — `ClassLabel`(unit 0에서 이관) + `RarityLabel`. 2회째 사용이라 추출.
- 수정 `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 내부 ClassLabel → `UnitLabels.ClassLabel` 호출(동작 불변, unit 0 테스트 그대로 통과).

## 구현

`SquadUnitDetailView : MonoBehaviour`
- SerializeField: `SkeletonGraphic spineView`(씬 저작), `Image portraitFallback`, `Image rarityFrame`(패널 등급 글로우), `RectTransform cardRoot`, `TMP_FontAsset font`, `string idleAnimation = "idle"`.
- `event Action DeployClicked;`
- `Show(DefenderUnitData u, bool inSquad)`: `BindSpine(u)` + `FillCard(u)` + `SetDeployState(inSquad)`. `u == null` → 전부 비활성/클리어.
- **BindSpine**: `u.SpineSkeletonDataAsset != null` → spine 활성, 필요 시 skeletonDataAsset 교체+Initialize, `SpineCombinedSkinCache.Apply`, idle 루프(`u.SpineIdleAnimation` → "idle"/"Idle" 폴백), portraitFallback off. 없으면 spine off + `portraitFallback = u.portrait`. rarityFrame 색 = 등급색.
- **FillCard**(cardRoot에 1회 절차적 생성 후 갱신): 이름(`displayName`) · 등급 배지(색+라벨) · 클래스 배지 · 코스트 배지 · 스탯 5행(데미지=`AttackOutputStats.TryGetUniqueMagnitude(outputs, Damage)`, 체력, 사거리, 공격주기초, 각성보상) · 설명문(`UnitKitSummary.Build`) · [출전] 버튼.
- **SetDeployState(bool inSquad)**: 버튼 라벨 `출전 ⊕` ↔ `편성 해제 ⊖` + 색 전환.
- 편성/스톤 브라우징 로직은 unit 4가 `DeployClicked` 구독 + `SetDeployState` 호출로 붙인다. 뷰는 데이터 바인딩만.

**중복 제거 결정**: 기믹 특성(방향/어그로/on-place/해저드)은 **설명문 문장**이 서술하므로 별도 "특성 칩"은 두지 않는다(중복 회피). 배지는 정체성(등급·클래스·코스트)만.

## 완료 기준

- [x] 컴파일 클린(신규 .cs 2개 → scope=all refresh, 에러 0). `UnitKitSummaryTests` 여전히 10/10(ClassLabel 이관 무해).
- [x] `Show(u, inSquad)` 바인딩 코드 완성 — spine(있으면)/포트레이트 폴백(없으면) + 카드 전 필드 + 버튼 상태. null-safe.
- [x] 시각 검증 — Play 오버레이 프리뷰(런타임 in-memory, 씬 무저장)로 라이브 Spine 풀바디 렌더 + 카드 가독성 확인(사용자 요청, 2026-07-18). SkeletonGraphic 머티리얼 = `SkeletonGraphicDefault-Straight.mat`, Canvas `additionalShaderChannels`(TexCoord1/2·Normal·Tangent) 필요.
- **검증된 튜닝값(unit 5 씬 저작 시 적용)**: spine `localScale ≈ 2.2`, feet 앵커 `y ≈ 0.42`(pivot bottom), cardRoot 앵커 `y 0~0.40`. 버튼 라벨 심볼(⊕/⊖) 제거 — Jua 폰트 미지원(□ 깨짐).

> 구현 2026-07-18 · 커밋 `82df4182` + 버튼 글리프 fix(미커밋). Play 프리뷰 시각 검증 통과.
