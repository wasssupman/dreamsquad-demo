# 1 — 슬롯 비용·역할·구매 가능 상태

## 목적

플레이어가 드래그 전에 각 유닛의 비용과 현재 구매 가능 여부를 읽게 한다. 긴 이름과 역할 식별성도 같은 슬롯 정보 계층에서 해결한다. 선행: unit 0.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs`
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs`
- `Assets/_Project/Data/Config/BattleHudTrayConfig.asset`

## 구현

- 슬롯 생성 시 portrait/name 외에 좌상단 비용 chip과 우상단 role badge를 만든다.
- 비용은 `DefenderUnitData.cost`, role 표기는 Config의 `DefenderClass` entry를 사용한다.
- 이름은 반투명 하단 band 안에서 한 줄 auto-size를 사용하고 config의 min/max 범위를 지킨다.
- `DefenderSelector`가 슬롯별 Image/TMP 참조를 캐시하고 `CostRuntime.Current` 또는 integer 값이 바뀔 때만 affordability visual을 갱신한다.
- 구매 불가는 portrait dim/desaturate에 준하는 색 조정 + 비용 chip 강조 + 부족 glyph를 함께 사용한다. 슬롯 raycast 차단은 unit 4에서 수행한다.
- current cost가 없거나 runtime 미초기화면 false-negative를 피하도록 neutral/available 표현을 유지한다.

## 완료 기준

- [x] 1~5 비용 유닛이 실제 데이터와 같은 숫자를 표시.
- [x] 현재 cost 경계를 넘나들 때 해당 슬롯만 즉시 available/unavailable 전환.
- [x] 색을 구분하지 못해도 비용 숫자와 glyph로 상태 판독 가능.
- [x] 포이즌/파이어/아이스 캐스터 연속 배치에서 이름 겹침·잘림 없음.
- [x] 7슬롯 재빌드/드래프트 재진입 시 캐시 누수·stale 상태 없음.

확인 2026-07-12 — 스크립트 배틀 캡처 검증: available(10)/mixed(2) 전환에서 2코스트만 밝게 유지 + dim·빨간 숫자·"X" glyph 동반(색 단독 금지). 캐스터 4연속(포이즌/파이어/아이스/블로킹) 이름 autosize 한 줄 유지. role 배지(원/수/근/술/보) + neutral 폴백(unit 0 이월분 검증). code-review(low) 1건(✕→X 글리프 안전) 반영. 콘솔 에러/경고 0. 사용자 확인 2026-07-12("ㄱㄱ"). CostChip PNG 는 textureType=Sprite 로 재임포트(Codex 커밋분은 Default 였음).
