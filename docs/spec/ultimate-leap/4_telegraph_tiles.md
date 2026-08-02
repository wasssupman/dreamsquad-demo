# 4 — 착지 예고 빨간 타일

## 목적

이탈 순간부터 착지까지, 착지 셀 중심 `slamTileRange` 반경(Chebyshev — 슬램 피해 범위와 **같은
계산**)을 빨갛게 표시한다. 예고 타일 = 피해 타일이 이 유닛의 존재 이유다 — 계산이 갈라지면
예고가 거짓말한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.UltimateLeap.cs` — 예고 표시/해제 (unit 3 partial 에 추가)
- 타일 표시 경로: `placement-attack-range-preview` 의 전용 tilemap tint 경로
  (`_rangeTilemap`, sorting -12, `Tilemap.color` 펄스) 재사용 검토가 1순위

## 구현

- 타일 집합 = `GridMath.RangeToTiles(slamTileRange)` — 슬램 TileAoe 가 쓰는 것과 **동일 함수**
  (예고=피해 일치의 구현적 보장).
- 표시 시작 = Ascend 이벤트 드레인 프레임. 해제 = Descend 처리(강하 완료·슬램 발화)와 같은 지점.
  teardown 시에도 해제(오버라이드 Clear 와 co-locate — "예고만 남는" 조합을 구조적으로 배제).
- 룩: 기존 range-preview 가 노란 펄스라면 빨강 계열 tint 로 구분. 별도 tilemap 을 새로 만들지
  말고 **기존 `_rangeTilemap` 인스턴스를 공유**할 수 있는지 먼저 확인 — 드래그 중이 아닐 때만
  궁극기가 뜨는 게 아니므로(드래그 중 발동 가능) 동시 사용 충돌이 있으면 전용 tilemap 1개 신설.
  이 판단은 구현 중 확정하고 결과를 이 문서에 기재한다.
- 펄스 케이던스: 잔여 시간에 비례해 빨라지는 점멸은 **후속 후보**(README) — 이번엔 고정 펄스.

## 완료 기준

- compile 클린 · EditMode 무회귀
- 예고 타일 집합과 슬램 피해 타일 집합이 같은 함수에서 나온다(코드 리뷰 확인 항목)
- teardown/배틀 종료 시 예고가 화면에 남지 않는다
- (Play 확인은 unit 5)

## 구현 중 확정 — 전용 타일맵으로 갔다

`_rangeTilemap` **공유는 불가**로 확정. `SetPlacementRange`/`SetPlacementCells` 가 매번
`ClearPlacementRange()` 로 시작하는 단일 owner set/clear 채널이라, 예고 2초 동안 플레이어가
유닛을 드래그하면 배치 프리뷰와 예고가 서로를 지운다. **예고 중 배치를 막을 수도 없다** —
유닛을 빼고 다시 놓는 것이 이 스킬의 놀이다.

`active-ally-zone` 이 같은 이유로 전용 타일맵을 판 선례라 그대로 따랐다:
`TilemapMapView.SetTelegraphCells` / `ClearTelegraphCells` + `landingTelegraphColor`(빨강).
z = -0.045(zone -0.03 위, range -0.05 아래), sorting -13.

**refcount 는 두지 않았다** — 궁극기는 생존당 1회라 동시 예고가 존재할 수 없다. zone 이
refcount 를 가진 것은 장판이 여러 장 겹치기 때문이고, 여기는 그 조건이 없다(제약 8).

해제 3지점: Descend 드레인(착지 확정 순간 — 강하 연출이 끝날 때까지 남기면 "아직 피할 수 있다"는
거짓 신호) · `DisposeUltimateLeapChannel`(매치 teardown) · `TilemapMapView.Clear`(맵 리빌드).

## 검증 기록

- 2026-08-02 · EditMode 1809 중 1807 통과·실패 0 · compile 클린.
