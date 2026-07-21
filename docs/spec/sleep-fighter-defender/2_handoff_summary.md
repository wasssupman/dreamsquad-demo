# 2 — handoff summary

## Commit

- `6141e655` docs: 스펙 (units 0~1)
- `a679eb91` feat: sleep-on-hit CC 경로 (unit 0)
- `dd0cfd66` feat: 투머치토커 에셋 + 카탈로그 등록 (unit 1)

## Implemented

- SO `sleepOnHitSec`(0=비활성) → `DefenderCcData` 미러 → 배치 베이크 1줄 → AttackSystem RESOLVE 에서 주 타겟(bestTarget 1체)에 `CcKind.Sleep` enqueue. 신규 시스템/채널/컴포넌트 0.
- 투머치토커(id `too_much_talker`, role 3 근접): cost 3 · 에픽 · HP 800 · range 2 · cd 3.0 · Damage 35 · 수면 3.5s(≥cd → 단독 상시 잠금). 어그로/넉백/투사체/onPlace 없음.
- DefenderCatalog 등록(18번째) — 스쿼드 페이지 자동 노출.
- 잠 연출(zzz)은 기존 `StatusFxKind.Sleep` 리컨사일이 자동 커버(신규 배선 0).

## Key Files

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE 넉백 블록 옆 sleep 분기
- `Assets/_Project/Scripts/Battle/Combat/DefenderCcData.cs` · `Data/DefenderUnitData.cs` — 필드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateDefenderEntity` 베이크
- `Assets/_Project/Data/Defenders/Defender_TooMuchTalker.asset` (+ .meta) · `Data/DefenderCatalog.asset`

## Verified

- compile 클린 · EditMode 1135/1135 (skip 2 = 기존 known-skip) — unit 0/1 각각 실행
- 사용자 Play 검증 통과: 로스터 노출/편성 · 히트→zzz+정지 · 단독 상시 잠금+3.5s 자연 기상 · 타 타워 히트 시 즉시 기상(wake-on-hit) · 콘솔 0

## Notes (되돌리면 안 되는 것)

- **시스템 순서가 정답의 일부**: CcApply→Movement→Attack→DamageApplication→CcClear 순서라 자기 히트(프레임 N)가 자기 Sleep(N+1 적용)을 못 깨운다. 이 순서를 바꾸면 이 유닛이 자기 수면을 자가 해제한다.
- wake-on-hit 유지는 **사용자 결정**(밸런스 밸브) — no-wake 변종은 후속 후보로만.
- sleep 은 bestTarget 1체 스코프(넉백 동일). 다중 타겟 유닛에 붙이면 주 타겟만 잠든다.
- 투사체 유닛에 `sleepOnHitSec` 설정 시 발사 시점 적용(넉백 quirk 공유) — 원거리 수면은 payload 이관 후속.
- 아트는 placeholder(브루저 초상/VFX/보이스, 파츠 재조합) — **guid 유지 교체** 전제.
- 파이프라인 맵/CLAUDE.md 채널 목록 갱신 불필요 — 새 아키타입/정거장/큐 없음.

## Follow-up

- README "후속 후보" 참조 (배치 광역 수면 펄스 · no-wake 변종 · 전용 아트 · 보스 수면 면역 · 투사체 수면).
- 다음 spec: 가디언 실드 신규 메커니즘(A초당 실드 B를 C유닛에, SELF/ALL/MINHEALTH 필터 + 체력바 실드 표기 논의) — 사용자 지시로 별도 폴더 예정.
