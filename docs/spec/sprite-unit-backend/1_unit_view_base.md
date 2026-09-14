# 1 · `UnitView` 추상 베이스 + 풀 전환 (라이브 무변)

## 목적

`SpineUnitView` 의 seam 표면을 **선언만 있는 추상 베이스** `UnitView : MonoBehaviour` 로 올리고,
풀과 타입 명시 지점을 그 베이스로 바꾼다. 순수 리팩터 — 이 커밋 뒤 게임은 픽셀 하나 안 바뀐다.
그것이 완료 기준이다.

**인터페이스가 아니라 추상 클래스인 이유** (critic C-1): 정적 타입이 인터페이스면 Unity 의
`operator ==` 오버로드가 선택되지 않아 `!= null` 이 참조 비교로 떨어진다. 풀 10곳 · 소비처 13곳의
생존 판정이 파괴된 뷰를 통과시키고, `DespawnMissing` 의 탐지기가 영영 안 터진다. 컴파일러가 못 잡는다.
`MonoBehaviour` 파생 베이스면 이 문제가 애초에 없고 `transform`/`gameObject`/`GetComponent` 도 그대로다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/UnitView.cs`
- 신규 `Assets/_Project/Scripts/Presentation/UnitFacing.cs` + `Tests/EditMode/UnitFacingTests.cs`
- 수정 `Presentation/SpineUnitView.cs` — `: UnitView` + `override` 키워드 + `SetFacingByViewDelta` 가 `UnitFacing` 호출
- 수정 `Presentation/SpineUnitPool.cs` — 딕셔너리 값 · `TrySpawn`/`TryGet`/`Detach` out 타입 → `UnitView`
- 수정 `Bridge/BattleBridge.cs` — `TryGetUnitView(out UnitView)` · 소환사 sync 호출부 `is SpineUnitView` 캐스트
- 수정 `UI/DefenderRetireFlight.cs` — `SpineUnitView` 9곳 → `UnitView` (본문 무변경)

## 구현

`UnitView` 추상 멤버 = `SpineUnitView` public 표면에서 **`Spawn` · `SetLoopOverride` · `ClearLoopOverride` 를 뺀 전부**:
`Entity` · `CurrentAnimationName` · `SetAnimationTimeScale` · `UpdatePosition` · `SetFlightHeight` · `PlayKnockupHop` ·
`UpdateSortingOrder` · `TryGetScreenRect` · `ApproxWorldHeight` · `SetFlightView` · `SetHoverHighlight` · `SetHealthTint` ·
`PlayPunch` · `PlayLandingSquash` · `FlashWhite` · `SetDimmed` · `PlayAttack` · `PlayDeploy` · `Kill` · `Dispose` ·
`FaceToward` · `ResolveCastAnchor` · `ResolveProjectileLaunchAnchor`. 기본 인자값은 베이스에 둔다(호출부는 베이스 타입으로 부른다).
구현은 0 줄 — 필드도 헬퍼도 없다.

`SpineUnitPool.TrySpawn` 은 여전히 `AddComponent<SpineUnitView>()` 만 한다(분기는 unit 2a). `view` 지역변수 타입만 바뀐다.

`BattleBridge.cs:3922` — `if (spineView is SpineUnitView sv) SyncSummonerAnimationState(entity, kv.Value.data, sv);`
메서드 시그니처는 그대로(파라미터 `SpineUnitView`). 해석 모호성 제거: **호출부에서** 가른다.

`UnitFacing` 순수 함수 — `SpineUnitView.SetFacingByViewDelta` 의 판정부를 그대로 옮긴다:
```
public const float MoveEpsilon = 0.001f, FlipAccum = 0.05f;
// dx: view-space 가로 델타. facingRight: 지금 오른쪽(+x)을 보는가. 반환: 지금 뒤집어야 하는가.
// 뒤집는 방향은 dx 의 부호 = 호출측이 `dx >= 0` 으로 안다.
public static bool ShouldFlip(float dx, bool facingRight, bool immediate, ref float pendingAccum)
```
규칙: `|dx| <= MoveEpsilon` → false · 이미 그 방향 → accum=0, false · `!immediate` 면 accum += |dx|, `< FlipAccum` 이면 false · 그 외 accum=0, true.
Spine 은 `facingRight = ScaleX < 0` 으로 번역해 호출하고 `ScaleX = currentAbs * (dx >= 0 ? -1 : 1)` 을 쓴다 — 동작 동일.

## 완료 기준

- **라이브 동작 무변**: EditMode 전체 초록(신규 `UnitFacingTests` 4건 내외 제외하면 총계 동일) · PlayMode
  `PatrolDefenderPlayTest`·`DefenderRetireTest` 수정 0 으로 초록(`CurrentAnimationName`·`gameObject` 가 베이스/MonoBehaviour 에 있다).
- `grep -rn "out SpineUnitView"` 0건 · `SpineUnitView` 타입 명시는 풀의 `AddComponent` · 소환사 sync 시그니처+캐스트 · 주석뿐.
- 컴파일 0 에러(에디터 + 격리 리그).

---

2026-09-15 구현 · `05b630ef` — EditMode 2697 초록(신규 4) · PlayMode 테스트 수정 0 · PlayMode lane 미실행.
