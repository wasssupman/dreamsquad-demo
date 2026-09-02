# 13 — 적 몸 크기를 티어로

> 외부 세션 확정 4: 적 = 티어 반경표 **소 0.25 / 중 0.5 / 대 1.0 / 보스 개별** + 접지폭 ≈ 2r
> 발주 규격(원장은 unit 17).

## 목적

자유 float 저작을 티어로 이산화한다 — 「사거리 N」의 상대별 의미가 유한해지고, 저작 실수
(아무도 근거를 모르는 0.4)가 불가능해진다. **오늘 저작값이 이미 표와 일치**해 값은 하나도
안 바뀐다: 일반 24종 = 기본값 0.25 = 소, 마메모 0.5 = 중, 나이트메어 0.558·짱쎈 0.615 = 보스 개별.

## 변경 대상

- `Scripts/Data/AttackUnitData.cs` — `bodySize` enum {Small, Medium, Large, Boss} 신설.
  `BodyRadiusTiles` 파생: Small 0.25 / Medium 0.5 / Large 1.0 / Boss → 기존 `bodyRadius` float.
- `Data/Enemies/*.asset` — 일반 24종 Small(기본값 무변) · 마메모 Medium · 보스 2종 Boss.
- 시트 — 컬럼 없음 → 임포터 스킵(unit 3 의 `bodyRadius` 선례). SO 저작으로 끝.

## 구현

- Boss 만 float 를 읽는다 — 표를 강제하면서 보스 개별 튜닝 축은 남긴다.
- Large(1.0) 는 예약 — 오늘 소비 0. 첫 대형 적이 올 때 이 unit 을 안 고치고 켠다.

## 완료 기준

- [ ] 골든 **이벤트 본문 무변화**(값 동일 스냅). `configHash` 만 갈린다(스키마 확장 —
      unit 3 과 같은 세 번째 범주, 전/후 해시 쌍 기록).
- [ ] EditMode 초록.
