# 4 — 디스패처 3지점 + `SkillFiredEvent`

## 목적

감지(Burst)와 실행(managed concrete)을 잇는다. **핵심은 지점이 하나가 아니라 셋이라는 것.**

## 변경 대상

- 신설 `Assets/_Project/Scripts/Battle/Skills/SkillFiredEvents.cs` (29번째 채널)
- 신설 `Assets/_Project/Scripts/Battle/Skills/SkillDispatchSystem.cs` (managed `SystemBase`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 3점 세트(생성/싱글턴 파괴/Dispose)
- `CLAUDE.md` — 채널 목록 28 → 29 갱신

## 구현

1. **드레인 지점은 3개다.** 감지 시스템들이 각자 명시적 same-frame 하류 계약을 갖고
   그 구간이 서로 겹치지 않는다:

   | 구간 | arm | 하류 계약 |
   |---|---|---|
   | #4 → #8 | BossPeriodic | `ProjectileEmitterSystem`(`[UpdateAfter]`) · `ModifierApplySystem` · `AggroStateSystem` |
   | #35 → #36 | AttackN | 피해 정산 #36 · 발사 #40 |
   | #45 → #46 | HealthThreshold | 궁극기 카운트다운 #46 · blink #47 |

   `#8 < #45` 이므로 **한 지점으로 셋을 만족할 수 없다.** 단일 지점을 고르면 일부 arm 이
   1프레임 밀려 자장가·도발·오라·blink·`AttackN`×패턴이 전부 이산적으로 달라진다.
2. **`BattleBridge.Update` 는 원리적으로 탈락.** 라이브 루프가 `Mono Update →
   SimulationSystemGroup` 이라 그룹 산출 이벤트는 **다음 틱** 브리지 페이즈에 드레인된다
   (하네스 스텝 순서 Bridge→ECS 가 이를 박제). 인용되던 `HazardCastSystem [UpdateBefore]`
   선례는 **Burst ISystem ↔ Burst ISystem 한정**이라 managed 디스패처에 적용되지 않는다.
3. **디스패처 = `BattleSimGroup` 안의 managed `SystemBase`, 단일 클래스 · 인스턴스 3개.**
   MonoBehaviour 가 아니므로 제약 1(브리지 유일 창구)과 충돌하지 않고, 제약 3 의
   「managed 참조가 진짜 필요할 때」 요건을 충족하는 **정당한 첫 사례**다
   (현재 Battle 폴더 `SystemBase` 0개). 각 인스턴스를 위 구간에 어트리뷰트로 핀한다.
4. **읽기 표본점이 보존된다.** BossPeriodic arm 은 이번 프레임 **이동 전**(#18 앞) 위치를,
   AttackSystem arm 은 **이동 후** 위치를 읽는다. 3지점 구조가 이 차이를 자동으로 지킨다.
5. **`SkillFiredEvent` 는 값 스냅샷이다** — params 값 + 대상 핸들 + 발화 위치.
   드레인 시점에 슬롯을 재독하면 죽음 계열에서 host 가 이미 없다
   (`Battle/Units/UnitLifecycleSystem.cs:108~137` 이 **파괴 전에** 굽고,
   `DamageApplicationSystem.cs:389~405` 가 *"killer 가 살아 있는 지금 읽는다"*).
   선례 3개: `ShieldBreakEvent` · `EnemyKilledEvent` · `DefenderDeathEvent`.
6. **TOCTOU 가드** — 드레인 시 캐스터 생존·슬롯 유효를 재검증하고, 무효면 drop + loud log.
   전력이 있다: `BossPeriodicTriggerSystem.cs:134` *"죽음 큐가 끼면 시체가 한 번 더 스킬을 쓴다"*.
7. **재진입 차단기** — 드레인은 시작 시점 스냅샷 1회. 드레인 중 재유입분(피해 intent →
   같은 프레임 `OnDamagedN`)은 다음 틱. 지금은 감지 분산이 자연 차단기인데 통합이 그걸 잃는다.
8. **결정론 핀** — `SkillFiredEvent` enqueue 는 **메인스레드 한정, `ParallelWriter` 금지.**
   현재 `Assets/_Project/Scripts/Battle/` 전체에 `Schedule`/`ScheduleParallel` 이 **0건**이라
   plain `Enqueue` 로 충분하고 순서가 결정적이다. 관행을 계약으로 승격한다.
9. **이중 경로 라우팅 축** — 슬롯에 **베이크된 unmanaged `skillId`**(0 = legacy arm).
   Burst 감지는 managed 레지스트리를 읽을 수 없으므로 이 축이 없으면 이전 중간 커밋에서
   게임이 도는지를 보장할 수 없다.
10. **채널 수명주기 3점 세트** — 생성 `Persistent` / 싱글턴 파괴 / `Dispose`.
    하우스 패턴: `Battle/Combat/DcTriggerFiredEvents.cs:17`.

## 완료 기준

- [ ] `SystemBase` 3인스턴스가 (#4→#8)·(#35→#36)·(#45→#46) 에 어트리뷰트로 핀됐다
- [ ] **order-capture 재덤프**가 완료 기준에 포함되고 실행됐다 — arm 실행 이전은 생산자 위치
      이동과 등가라 M0 unit 0 의 박제를 갱신해야 한다
- [ ] `SkillFiredEvent` 가 값 스냅샷을 싣고, 드레인이 생존·유효를 재검증한다
- [ ] 재진입 차단기와 메인스레드 enqueue 계약이 테스트로 고정됐다
- [ ] 베이크된 `skillId` 라우팅 축이 동작한다 (0 = legacy 로 라우팅되는 테스트)
- [ ] `CLAUDE.md` 채널 목록이 29개로 갱신됐다
- [ ] unit 1 그물 전건 초록 — **아직 아무 arm 도 이전하지 않았으므로 동작이 바뀌면 버그다**
