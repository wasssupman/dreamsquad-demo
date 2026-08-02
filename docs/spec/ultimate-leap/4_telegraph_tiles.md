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
