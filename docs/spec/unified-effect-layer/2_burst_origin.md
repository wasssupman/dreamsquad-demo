# 2 — 버스트는 원점 자리에서 나간다 (코어)

## 목적
발사 명세 버스트가 **바인딩을 든 자**(AA 의 호스트) 자리가 아니라 **원점 주체**(새로 배치된 유닛) 자리에서, **그 유닛 몫으로** 나가게 한다(계약 4 · U2 · U3). 케이스 1 AA 를 푼다.

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` — `EmitPattern`: 인스턴스에 원점 주체(= 의도 `Source`)를 싣는다.
- `Scripts/BattleCore/Combat/Emission/` 의 버스트 인스턴스 상태(`PatternSlotState.Instance`) — 원점 주체 id 하나.
- `Scripts/BattleCore/Phases/CombatPhase.cs` — `StepEmitters`/`AdvanceSlot` 과 발사 요청 조립(스코프 중심·스코프 몸·`Origin`·방향 계산): 슬롯 원점을 읽는다. `Owner`(킬 귀속)·`OwnerFaction`·대상 진영·공격 층도 **원점 주체**의 것.

## 구현
- 원점 주체의 **현재 좌표·몸**을 매 발 읽는다(보스 주기 발사처럼 움직이는 시전자 무변). 원점 주체가 없어지면 슬롯을 닫는다(U2). 바인딩을 든 자가 없어지면 오늘처럼 바인딩째 사라져 멈춘다.
- 기본값 = 바인딩을 든 자 자신 → 오늘의 모든 저작(원점 = 소유자)은 값이 같다. 평타 연발 슬롯(`atk.PatternSlots`)은 손대지 않는다.
- 스코프 도달은 `PatternScope.FilterByReach` 에 원점 몸을 넘긴다(제약 13 — 트리거한 대상의 몸).
- 탄 시드는 오늘처럼 의도 `Source` id × 카운터(이미 U 기준) — 무변.

## 완료 기준
- 탐침 둘을 U3 에 맞게 **이름·단언 수정 후** 해제 초록: `AA_…_귀속은_H` → `…_귀속은_U` · `A_와_AA_는_귀속만_다르고_…` → 「탄 수·대상 집합·피해 합·귀속(쏜 유닛) 이 같다」 · `현행_AA_…` · `현행_A_와_AA_…` 뒤집거나 삭제.
- 새 테스트: 버스트 도중 원점 주체 퇴근 → 남은 발 없음(U2) · 버스트 도중 호스트 사망 → 남은 발 없음 · AA 탄 킬 → 새 유닛의 OnKill 바인딩 발화, 호스트의 OnKill 은 무발화(U3).
- 헤드리스 · 골든 11 · EditMode Core · PlayMode Core(배치 스킬 씬 테스트) 무회귀.
