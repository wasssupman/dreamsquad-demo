# 2. Draft Card DMG from Outputs

## 목적

드래프트 카드의 DMG 표기가 죽은 `attackDamage`를 읽어 실데미지와 어긋나는 결함 정정 (Archer 표기 25 vs 실 15, Guardian 동일). 표기를 outputs 파생으로 전환한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs:261` (stats 문자열 조립부)

## 구현

- `unit.attackDamage` → `AttackOutputStats.TryGetUniqueMagnitude(unit.outputs, Damage, out var dmg)` 성공 시 그 값, 실패 시 `"-"` 표시.
- **Healer/hazard caster(BlockingCaster·FireCaster·IceCaster·PoisonCaster)의 "DMG -"는 의도된 동작** — 이 유닛들의 데미지는 직접 공격이 아니므로 숫자를 표시하는 쪽이 거짓말이다. heal량 표기는 후속 후보 (README 참조).
- HP/RNG/CD 표기는 무변경. UI 레이아웃/추가 라인 확장 금지.

## 완료 기준

- [x] compile 오류 없음 (2026-07-02)
- [x] 데이터 검증: asset outputs 실측 — Archer Damage=15, Marksman=40, PoisonCaster outputs 없음, Healer는 Heal만(Damage 없음) → 표기 로직상 Archer **15**, Marksman 40, PoisonCaster·Healer `"-"` 렌더 확정
- [ ] 수동 Play (Draft 화면) 시각 확인 — 사용자 확인 대기
- [x] 기존 스위트 회귀 없음 (444개, 기지 실패 ObstaclePlacer 1건 제외)
