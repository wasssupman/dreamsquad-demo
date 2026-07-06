# 2 — TileAoe Primitive (반경 멤버십 순수함수)

## 목적

착탄 셀 반경 flat AOE 의 "누가 맞나" 계산을 Burst 친화 **순수 static 함수**로 추출한다. 게임플레이-critical 타겟팅 계산이라 프로젝트 규칙상 EditMode 테스트 필수. 이 함수는 `MeteorResolutionSystem` 의 인라인 tile 멤버십(L70-77)과 **동형** — 지금은 신설만 하고, Meteor 를 이걸로 수렴시키는 dedup 은 cross-context 라 후속.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/TileAoe.cs`
- 신규 `Assets/_Project/Tests/EditMode/TileAoeTests.cs`
- `docs/spec/projectile-trajectory-payload/README.md` (명칭 `CollectInRange`→`IsInTileRange`/`TileDistance` 갱신)

## 구현

- `TileAoe.TileDistance(int2 a, int2 b)` = Chebyshev `max(|dx|, |dy|)`. 대각 이웃 = 거리 1 (게임의 사각 range 관례).
- `TileAoe.IsInTileRange(int2 candidateCell, int2 centerCell, int tileRange)` = `TileDistance ≤ tileRange`.
- **셀-공간 순수함수**. 월드→셀 변환은 caller 가 `GridMath.WorldToCell` 로 (이미 별도 테스트됨). → TileAoe 는 GridMath 의존 없는 순수 int2 산술이라 테스트가 값 대입으로 끝난다.
- **`CollectInRange`(리스트 materialize) 는 만들지 않는다**: unit 4 payload arm 과 ArtilleryShell(후속)은 candidate 스냅샷 루프 안에서 `IsInTileRange` 를 인라인 호출 후 `IncomingDamage` append (Meteor 패턴). collect 변형은 불필요한 alloc + 2중 루프라 회피.

## 완료 기준

- [x] 신규 .cs refresh **scope=all** → 컴파일 0 에러.
- [x] `TileAoeTests` green: 경계 inclusive(=range)/exclusive(=range+1), 대각 Chebyshev, 음수 오프셋 대칭, range 0(중심만). (6개, EditMode 489/490 — 1 실패=무관 ObstaclePlacer)
- [ ] 리뷰는 unit 4(payload arm 통합) 게이트에 흡수.

완료 확인: 2026-07-06 — 컴파일 0, TileAoeTests 6개 green.
