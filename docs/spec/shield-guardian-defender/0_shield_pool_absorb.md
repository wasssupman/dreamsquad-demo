# 0 — ShieldSlot 버퍼 + 흡수 (Units)

## 목적

출처별 실드 슬롯의 상태·병합·흡수·소비를 Units 맥락에 만든다. 생산자 없이도 독립 검증 가능한 토대.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/ShieldSlot.cs` — `ShieldSlot : IBufferElementData { Entity source; float value; }` + 순수 static `ShieldMath`(병합·흡수)
- 신규 `Assets/_Project/Scripts/Battle/Units/IncomingShield.cs` — `IncomingShield : IBufferElementData { Entity source; float amount; }` (IncomingHeal 동형: 매 프레임 drain + Clear)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 병합 + 흡수 훅
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 에서 전 defender 에 `ShieldSlot`/`IncomingShield` 버퍼 사전 부착 (IncomingHeal 선례 — ECB 구조변경 없이 append 하기 위함)
- 신규 EditMode 테스트 `Assets/_Project/Tests/EditMode/ShieldMathTests.cs`

## 구현

- **병합 (`ShieldMath.Merge`)**: IncomingShield drain 시 entry 마다 — 같은 `source` 슬롯이 있으면 `value = max(value, amount)`(같은 출처 중첩 불가), 없으면 새 슬롯 append(다른 출처 합산). 같은 프레임 같은 출처 다중 부여도 max 로 수렴. 유효 실드 = 슬롯 합.
- **흡수 (`ShieldMath.Absorb`)**: `totalDamage *= dmgTakenMul`(기존) **후** — 슬롯 **앞(오래된 것)부터** 차감, 소진 슬롯은 제거(RemoveAt, 삽입 순서 유지 — 결정론), 관통분만 Health 차감.
- `DamageApplicationSystem.OnUpdate` 루프 내 순서: ① IncomingShield drain→Merge→Clear ② 데미지 합산×dmgTakenMul ③ Absorb ④ 이후 모든 기존 분기(wake-on-hit·데미지 넘버·가시갑옷·킬 귀속·DeadTag)는 **관통분 기준 — 조건식 무변경**(계약 3).
- 데미지 넘버 per-hit 표시는 관통분 비례 배분: `hitShown = hitAmount × (관통 / 흡수전총량)` (0 이면 폰트 스킵 — 완전 흡수 히트 무표시).
- **source 는 중첩 키일 뿐 수명 링크 아님**(계약 4) — 부여자 사망/파괴와 무관하게 잔여 실드 유지, dead-source 청소 로직 없음. Entity 는 version 포함이라 재활용 id 와 키 충돌 없음.
- 힐(pulseHeal/Regen)은 실드와 무관 — Health 만 회복(기존 그대로). `ShieldSlot` 미보유 엔티티(적)는 lookup 가드로 전 경로 무변경.
- 순수 함수는 plain 입력(스팬/배열 또는 DynamicBuffer 값 복사) — Burst-safe, EditMode 에서 아키텍처 무관 검증(제약 10).

## 완료 기준

- [x] compile 클린.
- [x] `ShieldMathTests` 그린: 같은 출처 max(잔량>B / 잔량<B) / 다른 출처 합산(100+100=200) / 부분 흡수(오래된 슬롯 우선) / 슬롯 경계 걸친 흡수 + 소진 슬롯 제거 / 완전 흡수(관통 0) / 슬롯 없음(전량 관통) / 과잉 데미지.
- [x] 기존 EditMode 전체 그린 (버퍼 미부여 경로 무회귀).

확인 2026-07-21 · 커밋 `b5cb13b4` (EditMode 1143/1145 · skip 2 = 기존 known-skip)
