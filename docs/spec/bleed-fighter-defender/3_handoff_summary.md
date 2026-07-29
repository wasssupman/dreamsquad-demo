# 3 — handoff summary

## Commit

- `ac5210cb` test(bleed-fighter-defender): unit 0 — outputs ApplyStack 경로 PlayMode 회귀 가드
- `0aa709b5` feat(defenders): 넉업 심 + on-place 변종 2종 (unit 1 = ApplyStackNearby)
- `b770b797` feat(defenders): 난도질꾼·말파이트 유닛 에셋 + 카탈로그 등록 (unit 2)
- `c8808843` refactor(defenders): on-place 분기 중복 제거 (unit 1 분기가 공용 헬퍼로 접힘)

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
- `Assets/_Project/Data/Dreamcatcher/StackModifier_Bleed.asset` — 임계·틱 저작 지점(`atStack 5 · Consume` · 틱 4.5 / 1.0s · 지속 1.4s)

## Verified

- 리그 PlayMode: outputs 경로 · 배치 반경 필터 · 리팩토링 후 7/7 green
- **mutation 검증**: `AttackSystem` 의 ApplyStack enqueue 게이트를 `false` 로 끊으면 1/1 실패 → 테스트에 검출력이 있음을 증명(체크섬으로 원복 확인)
- 에디터 EditMode 1543건 중 사전 실패 2건만(MobileBuild 프리플라이트 · 미커밋 MapDocument_Zig)

## Notes (되돌리지 말 것)

- **출혈은 누적→폭발**(`atStack 5 · Consume`). 5타에서 발화하고 0으로 리셋 — 공속 0.3 기준 1.5초 주기, 1초 간격 2틱. **`stackCount` 는 안정적 관측값이 아니다**(임계에서 소모됨) — 관측은 **파생 DoT** 로. 초판 `atStack 1` 은 누적이 없어 사실상 플랫 도트였고 사용자 지적으로 재설계했다.
- **강도 누적형으로 바꾸지 말 것** — `stackCount > lastTriggeredStack` 게이트 때문에 상한 도달 후 발화가 멎는다. **폭발 지속 < 발화 주기**(1.4 < 1.5)도 지킬 것(`CcEffectMerge` 가 kind 슬롯을 덮어씀).
- **`maxStack` 은 producer(outputs·onPlace) 소유, `thresholds` 는 SO 소유** — 한쪽만 바꾸면 조용히 어긋난다.
- `StackModifier_Bleed` 를 쓰는 **배포 에셋은 난도질꾼뿐**이다(ember 는 테스트가 런타임 생성하는 카드). Bleed 를 쓰는 카드가 생기면 그때 밸런스 공유를 재검토할 것.
- on-place 분기는 `CollectEnemiesInTileRange` 공용 헬퍼를 쓴다(리팩토링 커밋). 새 on-place 변종은 이 헬퍼를 쓸 것 — 쿼리/순회를 다시 복제하지 말 것.
- `_aliveAttackersQuery` 는 **`AttackUnitTag`** 로만 잡는다. 테스트 더미 적에 이 태그가 없으면 반경 안이어도 0명이 되어 vacuous 해진다.

## Follow-up

- 밸런스 감각: 단일 대상 총 14.9 DPS(직격 8.9 + 출혈 6.0). 공속을 바꾸면 발화 주기가 따라 움직이므로 틱 수치도 한 벌로 다시 잡아야 한다 — 산식은 README 초기값 섹션
- 나머지 후속 후보는 README 참조
