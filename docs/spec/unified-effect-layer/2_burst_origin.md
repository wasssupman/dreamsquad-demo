# 2 — 버스트는 원점 자리에서 나간다 (코어)

## 목적
발사 명세 버스트가 **바인딩을 든 자**(AA 의 호스트) 자리가 아니라 **원점 주체**(새로 배치된 유닛) 자리에서 나가게 한다. 소유자·수명은 그대로 바인딩을 든 자(계약 4). 케이스 1 AA 를 푼다.

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` — `EmitPattern`: 인스턴스에 원점 주체(= 의도 `Source`)와 그 좌표·몸 스냅샷을 싣는다.
- `Scripts/BattleCore/Combat/Emission/` 의 버스트 인스턴스 상태(`PatternSlotState.Instance`) — 원점 필드 3개(주체 id · 좌표 · 몸).
- `Scripts/BattleCore/Phases/CombatPhase.cs` — `StepEmitters`/`AdvanceSlot` 과 발사 요청 조립(스코프 중심·스코프 몸·`Origin`·방향 계산): 슬롯 원점을 읽는다. `Owner`·`OwnerFaction`·대상 진영은 바인딩을 든 자.

## 구현
- 원점 주체가 살아 있으면 **그 현재 좌표·몸**(보스 주기 발사처럼 움직이는 시전자 무변), 사라졌으면 마지막 스냅샷(계약 4).
- 기본값 = 바인딩을 든 자 자신 → 오늘의 모든 저작(원점 = 소유자)은 값이 같다. 평타 연발 슬롯(`atk.PatternSlots`)은 손대지 않는다.
- 스코프 도달은 `PatternScope.FilterByReach` 에 원점 몸을 넘긴다(제약 13 — 트리거한 대상의 몸).
- 탄 시드는 오늘처럼 의도 `Source` id × 카운터(이미 U 기준) — 무변.

## 완료 기준
- 탐침 `AA_새로_배치된_U_자리에서_N칸_안_적_각각에게_100_귀속은_H` · `A_와_AA_는_귀속만_다르고_탄_수_대상_집합_피해_합이_같다` 해제 초록 · `현행_AA_…` · `현행_A_와_AA_…` 뒤집거나 삭제.
- 새 테스트: 버스트 도중 원점 주체 퇴근 → 남은 발이 마지막 자리에서 나감 · 버스트 도중 호스트 사망 → 남은 발 중단(오늘 수명 규칙).
- 헤드리스 · 골든 11 · EditMode Core · PlayMode Core(배치 스킬 씬 테스트) 무회귀.
