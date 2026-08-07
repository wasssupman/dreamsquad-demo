# 4. 경로 폭 1 제한 해제 (2×2 walk 블록 금지 철회)

rev 2026-08-07 — placement-mask(B-1) 방향 연장: 배치가 타일 종류에서 풀렸듯, 경로 저작도 폭 1 복도 강제를 푼다 (사용자 결정).

## 목적

Validate 의 "2×2 walk 블록 금지"는 저작 규칙이지 런타임 요구가 아니다 — flow field 는 다중 소스 BFS 라 임의 폭 walkable 영역에서 성립하고, `MovementCellTrim` 도 셀 단위 벽 판정이라 폭과 무관하다. 넓은 길을 그릴 수 있게 이 검사만 제거한다.

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs` — `Validate()` 의 2×2 검사 블록 제거

## 구현

1. Validate 에서 2×2 walk 블록 검사만 제거. **나머지 검증은 유지** — 스폰 2~4·골 1~4, 스폰/골 Walk 위, 다중 소스 BFS 연결성(이 셋이 런타임 `MapConnectivity` 계약과 일치하는 부분).
2. mergeDegree/chokepoint 는 Bake 산식 그대로 둔다 — 런타임 소비처 0(GeneratedMap/MapDocument/Builder 데이터 배관뿐, 2026-08-07 확인)이라 넓은 길에서 값이 커져도(내부 셀 degree 4 → chokepoint=1) 무해. 소비처가 생기면 그때 의미를 재정의한다.

## 알려진 성질 (버그 아님)

- 넓은 길에서 적들은 flow 방향 동률 tie-break(+x→−x→+y→−y 고정)로 **한쪽 열에 수렴**할 수 있다 — 결정론 사양. 폭을 시각적으로 채우는 분산은 스폰 좌우 오프셋(기존)만이고, 그 이상은 범위 밖(자유 이동 후속에서 해소).

## 완료 기준

- 2×2 이상 walk 덩어리를 포함한 맵이 Validate 통과 → Bake 가능.
- 기존 검증 3종(스폰 수/Walk 위 스폰·골/연결성)은 여전히 검출.
- compile 클린. 넓은 길 맵의 적 이동 모양새는 Play 육안 확인(placement-mask unit 3 육안 축과 함께).
