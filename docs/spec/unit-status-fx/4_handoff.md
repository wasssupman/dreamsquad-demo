# Unit 4 — handoff (unit-status-fx)

> 어그로 전용 아이콘 → 상태별 프리팹 연출 인프라 일반화. Unit 0~2 한 커밋으로 구현.

## Commit
- `02a9db24` feat(status-fx): 어그로 아이콘 → 상태별 프리팹 연출 시스템 일반화 [unit-status-fx 0-2]

## Implemented
- `StatusFxKind` enum(append-only, 현재 `Aggro`).
- `StatusFxRegistry` SO: `kind → {prefab, localOffset, scale, billboard, fallbackTint}`. **상태마다 다른 프리팹**을 끼운다. prefab 비면 절차 "!" 폴백.
- `StatusFxSpawner`(구 AggroIconSpawner): `(entity, kind)` 키 · kind별 풀 · BeginFrame/Ensure/EndFrame reconcile · 멀티 상태 동시.
- `StatusFxView`(구 AggroIconView): 프리팹 Instantiate 또는 절차 "!" 폴백(약한 펄스) · anchor+offset follow · 옵션 빌보드.
- BattleBridge `ReconcileStatusFx`: 상태별 ECS 소스 → Ensure. v1 Aggro=`Aggroed` 쿼리.
- 어그로 무손실 이관: `StatusFxRegistry.asset`(Aggro 항목, prefab 없음→"!" 폴백, offset +1.5Y, scale 0.5, tint 붉은 주황) + 씬 재배선(`AggroIconSpawner` GO 삭제 → `StatusFxSpawner` GO, `BattleBridge.statusFxSpawner`+`registry` 연결).

## Key Files
- `Assets/_Project/Scripts/Data/{StatusFxKind,StatusFxRegistry}.cs`
- `Assets/_Project/Scripts/Presentation/{StatusFxSpawner,StatusFxView}.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`ReconcileStatusFx`)
- `Assets/_Project/Data/Config/StatusFxRegistry.asset` (guid c2077d83…)

## Verified
- 컴파일 클린. EditMode 604 통과(무관 `ResultLeaderboardModelTests` 한글화 1건 제외). Play 진입 에러 0. 씬 diff 무관변경 0(reflection 검증).

## Notes (되돌리면 안 됨)
- 풀은 **kind별**(프리팹이 kind마다 달라 회수는 같은 kind 로만). 활성 키 `(entity, kind)`.
- 폴백 "!"만 펄스, 프리팹은 자체 애니메이션 가정.
- 새 상태 추가 = registry 항목 + `ReconcileStatusFx` 에 쿼리+Ensure 몇 줄. provider 추상화는 소스 2개 이상일 때(현재 1개=Aggro, 프리마추어 금지).

## Follow-up
- 실제 상태 추가(스턴/빙결/독): 각 ECS 소스 준비 시 registry 프리팹 + reconcile 훅.
- 어그로 "!" → 전용 프리팹 연출(가디언 tether 등). 재사용 맵: BlobShadow(발밑)·DragPlacement cord(tether)·SetHealthTint(틴트 충돌 주의).
- 버프/디버프/드캐 **아이콘 스트립**(정보 배지) = 별개 축 `unit-modifier-indicators`.
