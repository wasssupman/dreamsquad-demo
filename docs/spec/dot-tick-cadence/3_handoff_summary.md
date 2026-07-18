# 3 — Handoff Summary (dot-tick-cadence)

## Commit
- `aedcb66f` feat(dot-tick-cadence): DoT 를 매프레임 연속 → 정해진 주기 이산 tick 으로 (코드+에셋+테스트 15파일)
- 문서 확정/handoff 는 후속 docs 커밋.

## Implemented
- DoT(`CcKind.DoT`)가 매 프레임 `scalar×dt` 소수를 흩뿌리던 것을 **`tickInterval` 주기마다 `scalar` 청크 1회** 지급으로 전환. 데미지 폰트 "1" 스팸 해소(청크 정수).
- `HazardEffect`(SO)에 `tickInterval`, `CcEffect`(런타임)에 `tickInterval`+`tickTimer` append-only 추가.
- `DotApplySystem`: `tickInterval>0` → `DotTick.Advance`(순수)로 tick 수 계산 후 청크 지급, `<=0` → 레거시 연속. 두 Job 다 `ref DynamicBuffer<CcEffect>` 로 timer 되쓰기.
- **첫 tick 즉발**: `CcEffectMerge` add-path 가 `tickTimer=tickInterval` → 접촉 즉시 1회. 이후 존 인터벌 페이싱(머무는 동안).
- `CcEffectMerge.Apply` = CcEffect 병합 **단일 정책**(`CcApplySystem`·`EffectSpawner` 공유). 누적기 보존 + interval 변경 시 진행률 환산.
- 에셋: `Hazard_Fire_1x1`(param1=10, tickInterval=0.5), `Hazard_Poison_1x1`(param1=20, tickInterval=1.0). 실효 20 DPS.

## Key Files
- `Assets/_Project/Scripts/Battle/Effects/DotTick.cs` — 순수 누적 산식(+`DotTickTests`)
- `.../Effects/CcEffectMerge.cs` — 병합 정책 단일 소스
- `.../Effects/DotApplySystem.cs` — tick 지급/연속 분기
- `.../Effects/CcApplySystem.cs`, `EffectSpawner.cs` — 둘 다 CcEffectMerge 위임
- `.../Data/Hazards/Hazard_{Fire,Poison}_1x1.asset`
- Tests: `DotTickTests`, `DotApplySystemTests`, `CcApplySystemTests`

## Verified
- EditMode **964개 green (957 pass 이전 → 최종 962 pass, 0 fail, skip 2 = 기존 Ignored)**. 신규 12.
- ecs-reviewer: 버그 0, CRITICAL/HIGH 0. 맥락 경계·ref 버퍼 안전·하위호환·결정론 CONFIRMED.
- Play 배틀로그(`GameLogs/session-20260718-102521`): dot_damage 정수 청크만(10×15, 20×5), Fire 케이던스 0.484~0.503s, 이중발동 없음(동시타=서로 다른 적), 콘솔 에러 0.

## Notes (되돌리면 안 되는 의도)
- **첫 tick 즉발은 의도**(사용자 확인: "존 트리거→유닛 즉발→인터벌 페이싱"). 지나가는 적이 접촉마다 풀 청크를 받는 건 이 즉발의 귀결(연속 모델 대비 mover 데미지 증폭) — 승인됨.
- **하위호환**: `tickInterval<=0` = 연속. `StackModifierTickSystem` 스택 DoT·3x3 존은 tickInterval 미설정이라 불변. 되돌리지 말 것.
- **Fire+Poison 비스택**: 같은 `CcKind.DoT` 슬롯 공유 → 겹치면 `scalar` last-writer(10/20 교대). 사용자 "유지" 결정. timer 는 환산으로 매끈하지만 amount 교대는 by-design.
- 폰트 스포너(`DamageNumberSpawner` `Max(1,RoundToInt)`)는 무수정 — 청크가 정수라 바닥 문제 자연 해소.

## Follow-up
- 3x3 화염/독 존 이산 tick 통일 (현재 연속)
- `StackModifierTickSystem` 스택 DoT 이산 승격
- Fire+Poison 완전 분리(merge key `(kind, source)`) = 원소별 독립 스택 — 별도 spec(현재 스코프 밖)
- tick 순간 임팩트 VFX/사운드
