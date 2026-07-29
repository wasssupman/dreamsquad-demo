# 3 — handoff summary

## Commit

- `ac5210cb` test(bleed-fighter-defender): unit 0 — outputs ApplyStack 경로 PlayMode 회귀 가드
- `0aa709b5` feat(defenders): 넉업 심 + on-place 변종 2종 (unit 1 = ApplyStackNearby)
- `b770b797` feat(defenders): 난도질꾼·말파이트 유닛 에셋 + 카탈로그 등록 (unit 2)
- `c8808843` refactor(defenders): on-place 분기 중복 제거 (unit 1 분기가 공용 헬퍼로 접힘)

후속(출혈 상태 VFX + 밸런스, 2026-07-29 사용자 Play 확인 통과):

- `c4d799b6` feat(status-fx): 전투 스택 오라 4종 — DoT 진행 중에만 점등
- `401cdeaa` fix(status-fx): 스택 오라를 머리 위 뱃지에서 발밑 지면 연출로
- `1e2ec82d` tune(bleed): 출혈을 5초 지속 도트로 — 틱 5 / 0.5s · 10틱 · 1회분 총 50
- `bf0269b8` fix(status-fx): 스택 오라가 도트 후반부에 꺼지던 문제 — 종류를 bridge 가 래치

## Implemented

- 난도질꾼(`slasher`) — Fighter·Common·코스트 2, HP 350 · 사거리 1 · **쿨다운 0.3** · 직격 2.67
- `outputs` 에 `ApplyStack(Bleed 1스택 / perApp 2s / max 5)` 병기 — 히트마다 **누적**되고 5스택에서 터진다
- 배치 스킬 "등장 난도질" = `OnPlaceEffectType.ApplyStackNearby` 신설 (반경 2, **5스택** = 임계치를 한 번에 주어 즉시 출혈)
  - `onPlaceStackKind` 필드로 스택 종류 지정 — 분기에 하드코딩 없음
  - `maxStack` 은 그 StackKind 를 소유한 `StackModifierSO` 에서 읽는다(유닛이 아니라 스택의 성질)
- `UnitKitSummary` 에 대응 절 추가(신규 enum 멤버는 default 가 빈 문자열이라 조용히 설명이 빈다)
- **시뮬 신규 코드 0** — 출혈은 기존 `AttackOutputKind.ApplyStack` arm 의 첫 실사용

## Key Files

- `Assets/_Project/Data/Defenders/Defender_Slasher.asset`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyOnPlaceEffect` 의 ApplyStackNearby 분기
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `onPlaceStackKind` · enum 멤버
- `Assets/_Project/Tests/PlayMode/DefenderApplyStackOutputTest.cs` (outputs 경로)
- `Assets/_Project/Tests/PlayMode/OnPlaceApplyStackNearbyTest.cs` (배치 반경 필터)
- `Assets/_Project/Data/Dreamcatcher/StackModifier_Bleed.asset` — 임계·틱 저작 지점(`atStack 5 · Consume` · 틱 5 / 0.5s · 지속 4.85s = **10틱 · 1회분 50**)

- 출혈 상태 오라 = PixPlays ElementalAuras 사본, 발밑 지면 연출(`billboard=false` · offset y 0.05 · scale 0.7)
- 오라 점등 판정은 **DoT 진행 여부**, 종류는 **bridge 래치**(`_stackAuraLatch`) — 슬롯이 도트보다 먼저 죽는다

## Verified

- 리그 PlayMode: outputs 경로 · 배치 반경 필터 · 리팩토링 후 7/7 green
- **mutation 검증**: `AttackSystem` 의 ApplyStack enqueue 게이트를 `false` 로 끊으면 1/1 실패 → 테스트에 검출력이 있음을 증명(체크섬으로 원복 확인)
- 에디터 EditMode 1543건 중 사전 실패 2건만(MobileBuild 프리플라이트 · 미커밋 MapDocument_Zig)
- 출혈 밸런스는 **리그 PlayMode 프로브 실측**으로 고정: 첫 발동 직후 공격자를 제거해 재적용을
  끊고 순수 1회분을 셌다 — 정확히 10틱 · 총량 50.00 · 마지막 틱 후 잔여 0.33s
- 오라 지속 회귀 가드 `BleedAuraOutlastsStackSlotTest` green · 사용자 Play 확인 통과(2026-07-29)

## Notes (되돌리지 말 것)

- **출혈은 누적→발동→갱신형 도트**(`atStack 5 · Consume`). 5타에서 발화하고 0으로 리셋, 발동하면 5초간 0.5초 간격 10틱(틱당 5, 1회분 50). **`stackCount` 는 안정적 관측값이 아니다**(임계에서 소모됨) — 관측은 **파생 DoT** 로. 초판 `atStack 1` 은 누적이 없어 사실상 플랫 도트였고 사용자 지적으로 재설계했다.
- **강도 누적형으로 바꾸지 말 것** — `stackCount > lastTriggeredStack` 게이트 때문에 상한 도달 후 발화가 멎는다.
- **지속(4.85s) > 발화 주기(1.5s) 는 의도된 것.** 계속 맞는 적은 출혈이 끊기지 않는다 — `CcEffectMerge` 가 `remainingTime = max` 로 갱신하고 `tickTimer` 를 보존해 틱 리듬까지 이어진다. 옛 "폭발 지속 < 주기" 규칙은 폐기됐다.
- **`duration` 을 `tickInterval` 의 정확한 배수로 두지 말 것.** 첫 틱 즉발 + `tickTimer` dt 누적 구조라 마지막 틱과 만료가 같은 프레임에 겹치면 틱 수가 흔들린다. **`5.0` 이 아니라 `4.85` 인 이유가 이것** — 5.0 이면 11번째 틱이 만료와 겹쳐 10틱(50)/11틱(55) 사이에서 진동한다. 리그 실측: 1회분 정확히 10틱 · 50.00.
- **`maxStack` 은 producer(outputs·onPlace) 소유, `thresholds` 는 SO 소유** — 한쪽만 바꾸면 조용히 어긋난다.
- `StackModifier_Bleed` 를 쓰는 **배포 에셋은 난도질꾼뿐**이다(ember 는 테스트가 런타임 생성하는 카드). Bleed 를 쓰는 카드가 생기면 그때 밸런스 공유를 재검토할 것.
- on-place 분기는 `CollectEnemiesInTileRange` 공용 헬퍼를 쓴다(리팩토링 커밋). 새 on-place 변종은 이 헬퍼를 쓸 것 — 쿼리/순회를 다시 복제하지 말 것.
- `_aliveAttackersQuery` 는 **`AttackUnitTag`** 로만 잡는다. 테스트 더미 적에 이 태그가 없으면 반경 안이어도 0명이 되어 vacuous 해진다.

## Follow-up

- 밸런스 감각: 단일 대상 총 14.9 DPS(직격 8.9 + 출혈 6.0). 공속을 바꾸면 발화 주기가 따라 움직이므로 틱 수치도 한 벌로 다시 잡아야 한다 — 산식은 README 초기값 섹션
- 나머지 후속 후보는 README 참조
