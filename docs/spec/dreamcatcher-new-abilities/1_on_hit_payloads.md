# 1 — 온-히트 payload (frost_arrow · ember_bite)

## 목적

AttackN 트리거 시 맞은 적(`bestTarget`)에게 CC/스택을 거는 두 payload 를 bake + AttackSystem RESOLVE 에서 발동한다. 기존 채널(EnemyCc·StackModifier)만 사용, 신규 채널 0.

## unit 1 발견에 따른 설계 확정

- **frost_arrow = Stun(짧은 얼림)**. 이 엔진의 "Slow" 는 CcEffect 가 아니라 MoveSpeedMul StatModifier(ZoneApplySystem:45-57)라, CC 파이프·shatter 시너지를 살리려면 실제 CcEffect 인 **Stun** 사용. 적 Stun = 자기주도 이동+공격 정지(MovementSystem:70-72, AttackSystem:138). 짧게(예 0.6s/3타)로 튜닝해 perma-lock 방지.
- **ember_bite = Bleed 스택**. 스택→DoT 는 `StackModifierTickSystem` 이 `BattleBridge.GetStackThresholds(Bleed)` 의 ThresholdRule 로 발동 → **Bleed ThresholdRule(SO) 이 있어야** DoT 발생(없으면 스택만 쌓이고 무동작). unit 3(assets)에서 Bleed 규칙 존재 확인/추가. 그 DoT/Stun 은 CcEffect 로 적에 부착 → shatter 시너지 성립.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` (`ApplyDreamcatcherCardToUnit` 제너릭 슬롯 bake)
  - `ApplyCcToTarget`: 투사체 불요. `slot.payload`, `slot.duration`, `slot.magnitude`, **번역** `slot.ccKind = MapDcCc(m.payload.ccKind)`(DcCcKind→Battle.CcKind) 세팅. `AttackN` + period>0 검증.
  - `ApplyStackToTarget`: 투사체 불요. `slot.magnitude`(스택 수), `slot.duration`(스택당 지속), **번역** `slot.stackKind = MapDcStack(m.payload.stackKind)`. `AttackN` 검증.
  - `MapDcCc`/`MapDcStack` static 번역 헬퍼 추가(스위치, 기본값 명시).
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` (RESOLVE DC-slot 블록 :687-694)
  - 현재 `payload != ProjectileToTarget → warn / else carrier` 를 **payload 디스패치**로 확장:
    - `ProjectileToTarget` → 기존 carrier.
    - `ApplyCcToTarget` → `ccWriter.Enqueue(EnemyCcEvent{ target=bestTarget, effect=CcEffect{ kind=slot.ccKind, remainingTime=slot.duration, (Impulse 면 vector=dir*magnitude) } })`. dir = normalize(bestTargetPos-atkPos). ccWriter 부재 시 skip.
    - `ApplyStackToTarget` → `stackModSingleton.queue.Enqueue(StackModifierApplyEvent{ target=bestTarget, kind=slot.stackKind, countDelta=(byte)max(1,magnitude), maxStack=5, perAppDuration=duration, source=attackerEntity })`. hasStackQ 가드.
    - 그 외 → 기존 warn.

## 완료 기준

- [x] 4개 어셈블리 `dotnet build` 오류 0개.
- [x] AttackN×ApplyCcToTarget(Stun) bake→발동: RESOLVE 가 EnemyCc 채널로 Stun enqueue (코드 경로 확립).
- [x] AttackN×ApplyStackToTarget(Bleed) bake→발동: RESOLVE 가 StackModifier 채널로 Bleed enqueue.
- [x] 기존 ProjectileToTarget(poke_needle) 경로 보존(디스패치 첫 분기 = 기존 carrier 본문 그대로).
- [ ] PlayMode 발동 assertion — unit 3(카드 통합)에서 검증. ember 는 Bleed ThresholdRule(SO) 존재가 전제(unit 3 확인).

확인: 2026-07-13 — dotnet build 컴파일 검증. (Unity PlayMode 미실시.)
