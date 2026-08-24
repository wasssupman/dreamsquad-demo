# 0 — 프로토콜 표면 도출

## 목적

`ISkillContext` 의 동사를 **현존 arm 전수에서 뽑는다.** 정의하지 않는다.
`skill-fire-dispatch` rev 3 이 조사를 미루고 추정을 확정문으로 써서 착수 불가 판정을 받았다.
이 unit 은 **코드를 0줄 쓴다** — 산출물은 표뿐이다.

## 변경 대상

문서만: 이 파일에 산출표를 채운다. 코드·에셋 무변경.

읽을 arm (**3어휘**):

| 어휘 | 위치 |
|---|---|
| `DcPayloadKind` (26종) | `Scripts/Bridge/BattleBridge.cs`(28) · `BattleBridge.Dreamcatcher.cs`(18) · `Battle/Combat/AttackSystem.cs`(10) · `Battle/Units/DamageApplicationSystem.cs`(7) · `Battle/Combat/BossPeriodicTriggerSystem.cs`(6) · `Battle/Combat/HealthThresholdSystem.cs`(5) |
| `OnPlaceEffectType` (arm 9종) | `Scripts/Bridge/BattleBridge.cs:5393~5590` if/else 체인 |
| **`SkillEffectType` (6종)** | `Scripts/Bridge/BattleBridge.cs:2505~2583` `CastSkillAtTile`/`CastPortal` switch, 구현 `:2819~2958` |

⚠ 세 번째 어휘를 빼면 액티브 가족이 요구하는 동사(타일 지정 존 스폰 · 2타일 링크 ·
플레이어 시전)가 표면에서 누락된 채 계약 9 가 **형식만** 지켜진다.

## 구현

1. **질의 표** — 동사 · 소비 arm 수 · 포트로 감쌀 수 있는가.
   선행 실측(critic): **12동사** — `Position` · `CellOf/CellCenter` · `Facing` ·
   `Opponents(caster,r,filters)` · `Allies` · `DensestOpponentCluster` · `LandingCellNear` ·
   `Stat(id,kind)` · `Health` · `ShieldValueFrom` · `Has(id,pred)` · `TraversalLayers`.
2. **의도 표** — intent · 대응 채널. 선행 실측: **14종**(존 캐리어 스폰 포함).
3. **`Opponents` 필터 축을 enum flag 로 명세**하고 arm 별 현행 조합을 박제한다.
   오늘 후보 수집이 **5개 구현**이고 필터가 서로 다르다 — BossPeriodic 공유 풀은 무필터,
   `IsLegalOnPlaceTarget`, AttackSystem 3술어, AreaSleep 은 `PendingDeployment` 추가.
   못박지 않으면 「같은 이름, 다른 후보」 버그가 프로토콜 아래로 숨는다.
4. **«큰 것을 읽는» arm 표시.** 선행 실측: `FlowFieldSingleton` 소비는 blink/leap **2개뿐**이고
   `BlinkMath`·`DefenderDensity` 가 이미 격자를 인자로 받는 순수 함수라 질의 2개로 봉합된다.
   새로 발견되면 여기에 추가한다 — **하나라도 감쌀 수 없으면 계약 1 이 거짓이 된다.**
5. **직접 쓰기 구멍 열거.** 계약 3 이 「의도 방출만」인데 오늘 반례가 있다:
   `AttackState.cooldownRemaining` 직접 연장(`BattleBridge.cs:5544`) ·
   `AwakeningReward` 덮어씀(`.Dreamcatcher.cs:1120`) ·
   `EffectSpawner.ApplyCc` 가 `CcEffect` 라이브 버퍼 직접 append(큐 우회 — 같은 CC 경로 2개) ·
   진행형 상태 부착 4종. 각각 intent 화 / 예외 명문화 중 하나로 판정한다.
6. **요청-응답 arm 3종의 처리 결정.** `Execute` 는 void 인데 오늘 반환값 계약이 있다 —
   부착 코드(-1 = 무차감 거절 → 코스트 환불) · `RegisterPlacementAura` revoke 핸들 ·
   affected 수(로그). 스킬 밖 유지 / 별도 포트 메서드 / 어댑터 계측 중 택일.
7. **`SkillParams` 겸직 해소안.** `tileRange` 가 **8가지 이상** 의미를 겸직하고
   (`AoE 반경`·`궤도 반경`·`maxStack`·`피해감소%`·`폴백 반경`·`착지 링 상한`·`최대중첩`·`조준 사거리`),
   `period` 는 AttackN 카운트이자 orbitCount 다. bake 가 값 **변환**까지 한다(coneCosSq 사전계산).
   → **skillId 별 typed params + 디스패처 번역층.** `skill-fire-dispatch` 계약 4 의
   「params 뷰 struct」를 계승한다 — 새 발명이 아니다.
8. **Mono 도메인 의도 분류.** `GainCost`·`ReduceSkillCooldown` 은 ECS 를 전혀 안 만지고,
   hand-op(`RecallAttachedToFront`)은 실행자가 `DreamcatcherHandController` 다.
   의도 어휘를 sim 계열 / Mono 계열로 이원 명시하거나 예외로 판정한다.
9. **행 → 담당 unit 대조표.** census ~75행(적 13 · 방어유닛 규칙 5 · 레거시 9 · 카드 32 ·
   캐스트 8 · 소환 1 · 액티브 6)을 `skill-layer-migration` 의 어느 문서가 맡는지 전부 배정한다.
   배정 없는 행이 남으면 그것이 끝점 미달이다.

## 완료 기준

- [ ] 질의·의도 표가 **3어휘 전수**에서 도출됐고, 각 동사에 소비 arm 이 1개 이상 붙어 있다
- [ ] 감쌀 수 없는 읽기가 **0건**임이 표로 확인됐다(있으면 계약 1 을 재론한다)
- [ ] 직접 쓰기 구멍 · 요청-응답 arm · Mono 의도가 각각 «intent 화» 또는 «예외» 로 판정됐다
- [ ] `Opponents` 필터 축이 enum flag 로 명세되고 arm 별 현행 조합이 박제됐다
- [ ] ~75행 전부에 담당 unit 이 배정됐다 (미배정 0)
- [ ] `ISkill`·`ISkillContext`·`SkillFiredEvent` 시그니처가 고정됐다 — **caster 없음**과
      **대상 셀 A/B**(Portal 2타일)를 표현한다
- [ ] 코드 변경 0줄
