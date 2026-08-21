# unit 0 — 붕괴가 프랍에 닿는다

## 목적

본능이 무너져도 프랍은 아무 일 없었다는 듯 서 있다. **붕괴 사건과 프랍 사이의 배선**을 놓고,
그 위에 최소 읽힘 하나를 얹는다 — 그을리고 주저앉고, 포신은 조준을 멈춘다.

신호는 **이미 도착해 있다.** `BattleBridge.SyncGoalStability` 의 else 분기가 붕괴한 거점의
셀·진영을 알고 게이지를 숨기고 돌파편 VFX 를 터뜨린다. 받는 사람만 없다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/StructureWreckView.cs` (신규)
- `Assets/_Project/Prefabs/Structures/Instinct_{Ally,Enemy}.prefab` — 컴포넌트 부착 + 포신 참조
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `_structureWrecksByCell` 사전 (터렛 사전 바로 옆, 같은 규칙)
  - `SpawnStructureViews` — 등록 · `ClearStructureViews` — 비움
  - `SyncGoalStability` 붕괴 분기 — `Collapse()` 호출

## 구현

### 프리젠터 (뷰 소유)

```
StructureWreckView : MonoBehaviour
    [SerializeField] StructureTurretView turret;   // 붕괴 시 끈다(계약 5). 없어도 무해
    [SerializeField] Color scorchTint = (0.30, 0.26, 0.24, 1);
    [SerializeField] float settleScale   = 0.72f;  // 주저앉음 비율
    [SerializeField] float settleSeconds = 0.25f;
    public GameObject Collapse()                    // 멱등. unit 1 이 debris 컨테이너를 반환하게 만든다
```

- **그을림** = MPB `_BaseColor` + `_Color`. 렌더러 목록과 MPB 를 `Awake` 에 1회 캐시한다.
  `TilemapMapView.ApplyPropTint` 를 공유하지 않는 이유: 그쪽은 「단계가 바뀔 때마다 프랍을
  새로 훑는」 골 전용 헬퍼(스프라이트 프랍까지 다룬다)고, 여기는 「자기 렌더러를 알고 한 번만
  쓰는」 컴포넌트다. 공유하면 캐시를 버려야 하고, 얻는 것은 열 줄이다.
  공유 머티리얼은 건드리지 않는다(MPB 만 쓴다).
- **주저앉음**은 즉시 스냅이 아니라 `settleSeconds` 짧은 ease-out. 「무너지는 동작」이 있어야
  붕괴가 **사건**으로 보인다(골 붕괴가 즉시 스냅인 것과 다른 판단 — 골은 그 프레임에 유출
  전환이 같이 터져 시선이 이미 그쪽에 있다).
- **멱등**: `_collapsed` 플래그로 두 번째 호출을 무시한다. 안 그러면 스케일이 누적 곱해진다.
- 시간은 `TimeManager.Instance.DeltaTime(TimeDomain.Battle)`(계약 6).

### 브리지 배선

- `SpawnStructureViews` 가 프랍을 세울 때 `GetComponentInChildren<StructureWreckView>()` 를
  셀 키로 등록한다 — 포신 사전과 **같은 자리·같은 규칙**(계약 3). 미발견은 경고하지 않는다(계약 9).
- `ClearStructureViews` 가 포신 사전과 함께 비운다. 이게 원복 지점이다(계약 2) —
  잔해 상태를 브리지에 따로 들고 있지 않는다.
- 붕괴 분기(현 `SyncGoalStability` 의 else)에서 셀로 찾아 `Collapse()`. 기존 게이지 숨김·
  돌파편 VFX·로그는 그대로 둔다.
- ⚠ **매치 종료 프레임의 구멍은 수용한다.** `SyncGoalStability` 는 `_resultShown` 이면 조기
  리턴한다(`BattleBridge.cs:6136`). 즉 `EndMatch` 와 같은 프레임에 부서진 본능은 잔해 연출을
  못 받는다. 다음 프레임엔 결과 화면이 보드를 덮으므로 보이지 않는 차이고, 이걸 고치려면
  종료 판정과 붕괴 관측의 순서를 건드려야 한다 — 뷰 하나 때문에 그 순서를 바꾸지 않는다.

### 테스트를 새로 쓰지 않는 이유 (제약 10 판정)

순수 계산이 없다 — 색 lerp 한 줄과 스케일 lerp 한 줄이고 둘 다 호출처가 하나다.
(a) 비자명 · (b) 호출처 2+ · (c) sim-critical 회귀 가치 어디에도 안 걸린다
(`instinct-turret-readout` unit 1 과 같은 판정). 검증은 Play 육안 + 스크린샷.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 2 lane 2,543개 실행 — **신규 실패 0**. 사전 실패 3건은 이 작업과 무관하다:
      `CameraComposeMathTests.ShakeImpulse_*` 2건은 **병행 세션의 미커밋 WIP**(같은 워크트리에서
      `CameraComposeMath.cs`/그 테스트가 dirty · TDD 빨강), `UnitKitCatalogTests` 1건은
      HEAD 상태의 사전 회귀(말파이트 설명 2행 30자 > 28 — 관련 에셋·포매터 모두 clean)
- [x] 오프스크린 렌더 검증(에디터 비포커스라 Play sim 이 안 돈다 — `offscreen render` 기법):
      온전한 프랍 옆에 붕괴시킨 프랍을 세워 **그 프랍만** 그을리고 주저앉는 것을 확인.
      `brokenScale=0.29 = viewScale 0.4 × settleScale 0.72` · `intactScale=0.40`
- [x] 부서진 뒤 포신이 더는 돌지 않는다(계약 5) — `turret.enabled = false`
- [x] **그을림 세기 = 0.45**(사용자 결정 2026-08-21). 초판 0.30 은 노란 아군 본능을 거의 검게
      만들어 **어느 편이었는지 안 읽혔다**. 0.58 은 그을림이 아니라 먼지로 보인다.
      0.45 에서 아군(호박빛)·적(붉음)이 갈리면서 둘 다 탄 것으로 읽힌다 — 즉 잔해가 되어도
      `instinct-turret-readout` 의 「편이 읽힌다」가 유지된다
- [ ] **라이브 Play 체감(사용자)** — 실제 전장 배경 위에서의 판정. 오프스크린 flat bg 는
      실제보다 관대하다
- [ ] 판이 끝나고 **로비 왕복(씬 리로드)** 후 다시 들어가면 프랍이 온전한 상태로 돌아온다(계약 2).
      ※ 인-씬 재시작(`OnRestartRequested`)은 현재 **구독자 0** 이라(결과창이 「로비로」가 된 뒤
      의도적으로 dormant) 검증 경로가 아니다. 되살아나도 계약 2 는 성립한다 —
      `TeardownCurrentBattle` → `TeardownGeneratedMap`(`BattleBridge.cs:692`)이 `_generatedMap` 을
      dispose 하므로 `BeginPlacement` 의 `!_generatedMap.IsCreated` 가 참이 되어 맵과 프랍이
      함께 재빌드된다.
- [ ] 스크린샷 1장 (`Assets/Screenshots/`) — 부서진 본능과 멀쩡한 본능이 한 화면에
