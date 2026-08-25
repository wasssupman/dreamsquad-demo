# 3 — 드림캐쳐 카드 (슬롯 arm)

## 목적

카드 mechanics **32행/30장** 중 슬롯을 경유하는 것들을 옮긴다.
부메랑 · 잿불 · 불나방떼 · 광란 · 빈사폭주 · 진동갑주 등이 여기다 —
**이들은 이미 보스 스킬과 같은 어휘를 쓴다.** 별도 「드림캐쳐 레이어」가 아니었다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — arm 18곳
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — RESOLVE arm
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — OnKill/OnDamagedN arm
- `Assets/_Project/Scripts/Skills/Concrete/` · `Data/UnitSkills/`
- **카드 에셋과 시트는 무변경** (계약 5)

## 실측 재고 (2026-08-26) — **핵심은 payload 가 아니라 seam 이다**

32행을 (트리거 × payload)로 갈랐다:

| 트리거 | 행 | 감지자 | seam 있나 |
|---|---|---|---|
| `AttackN` | 11 | `AttackSystem` (RESOLVE) | **없음** (공격 seam 은 생산자 0) |
| `OnKill` | 4 | `DamageApplicationSystem` | **없음** |
| `OnDeath` | 2 | `UnitLifecycleSystem` | **없음** |
| `OnDamagedN` | 2 | `DamageApplicationSystem` | **없음** |
| `OnShieldBreak` | 2 | `DamageApplicationSystem` → 브리지 | **없음** |
| `OnRetire` | 2 | 브리지 | **없음** |
| `HealthThreshold` | 2 | `HealthThresholdSystem` | ✅ 있음 |
| `PeriodicTimer` | 2 | `BossPeriodicTriggerSystem` | ✅ 있음 |
| (없음/부착 자격) | 5 | — | 해당 없음 |

**concrete 는 절반이 이미 있다.** `SelfTileAoe` 가 **6개 트리거에 걸쳐 7행**을 차지하고,
`AreaSleep`·`EmitProjectilePattern` 을 더하면 **9행이 concrete 를 이미 갖는다**.
새 concrete 가 필요한 것은 13 payload / 23행.

**그런데 오늘 안전하게 라우팅할 수 있는 카드 행은 사실상 1행뿐이다** —
`HealthThreshold × SelfTileAoe`. 나머지는 그 트리거의 감지자가 `SkillFiredEvent` 를
**아예 안 넣기 때문**이다. 라우팅만 열면 그 카드는 **조용히 죽는다**(slot 은 붙는데
아무도 안 읽는다) — unit 8 이 경고한 바로 그 실패 유형이다.

⚠ **그래서 unit 3 의 첫 작업은 concrete 가 아니라 seam 이다.** 토대 unit 4 가 연 3 seam
(#4 주기 · #35 공격 · #45 경계) 중 **공격 seam 은 아직 생산자가 0**이고, 죽음 계열
(`OnKill`/`OnDeath`/`OnDamagedN`/`OnShieldBreak`)과 `OnRetire` 는 seam 자체가 없다.

## 작업 분할

| 슬라이스 | 내용 | 선행 |
|---|---|---|
| **3a** | **공격 seam 개통** + 첫 소비자 `ApplyCcToTarget`(3행) | **완료** (2026-08-26) |
| **3b** | `ApplyStackToTarget`(2) · `SelfStatBuff`(4) | **완료** (2026-08-26) |
| **3b'** | `ProjectileToTarget`(2) — 어댑터에 **유도 투사체 의도**가 필요하다(오늘 `SpawnProjectile` 은 하늘낙하×광역 고정) | 3a ✅ |
| **3c** | **죽음 seam 개통** — `OnKill`/`OnDeath`/`OnDamagedN`. ⚠ 드레인 시점에 host 가 **이미 없다**(unit 0 실측) | — |
| **3d** | 죽음 계열 payload (8행) | 3c |
| **3e** | `OnShieldBreak`/`OnRetire` seam + payload (4행) | — |
| **3f** | 부착 자격만 있는 5행 + 남은 payload | |
| **3g** | 카드 bake 전면 개방 + 카드 arm 철거 | 위 전부 |

⚠ **3g 는 unit 8 의 전제이기도 하다** — 카드 경로가 열리기 전에는 arm 을 못 지운다.

## 3a 에서 나온 것 (2026-08-26)

**seam 은 소비자 없이 열지 않는다.** 열기만 하면 `ExecutedCountOf(Attack)` 이 여전히 0이라
그물이 그 축을 못 본다 — 그래서 첫 payload(`ApplyCcToTarget`, 3행)를 같이 태웠다.
어댑터 주석의 규율(「그 동사를 처음 요구하는 unit 에서 채운다 — 그때 그것을 쓰는
concrete 와 그물이 같이 온다」)이 seam 에도 그대로 적용된다.

**값 스냅샷 계약이 여기서 처음 실제로 쓰인다.** `bestTarget` 은 9단계 오버라이드의
합성물이라(최근접 → 힐러 재랭킹 → priority → 적 락 → 어그로 → frontmost → 지속 락 →
커밋 유지 → facing) 드레인 시점에 재질의하면 **다른 답이 나온다**. 그래서 대상·위치·방향·
killer 통행 층을 발화 시점 값으로 싣는다.

**방향은 `SkillTarget` 에 넣었다** — 대상과 같은 축이기 때문이다. 유도탄이 다른 데
맞아도 밀리는 방향은 «쏜 방향» 이고(계약 6), 드레인 시점엔 둘 다 이미 움직였다.

⚠ **어댑터는 두 풀(적/방어유닛) 안의 엔티티만 다룬다.** 서리화살 그물의 더미가
`AttackUnitTag` 가 없어 풀 밖이었고, 그래서 CC 가 조용히 사라졌다 — `SimEntityId` 와
같은 계열의 함정이다(핸들은 만들어지는데 되돌릴 수 없다). 실적 아키타입을 모사하게 고쳤다.

**세 seam 이 전부 증인을 갖게 됐다** — 주기(마메모·도발) · 경계(짱쎈놈) · 공격(서리화살).

## 3b 에서 나온 것 (2026-08-26)

**`HeavyStrike` 는 이전 대상이 아니다.** arm 이 **비어 있다** — 강공 배율은 RESOLVE
상단의 pre-scan 이 슬롯을 직접 읽어 산출하고, 발동 루프에서의 역할은 카운터 tick 뿐이다.
concrete 를 만들면 **아무것도 안 하는 concrete** 가 생긴다(제약 8). 「발동하면 무언가를
한다」가 아니라 「슬롯이 데이터로 읽힌다」는 성격이라 이 레이어의 대상이 아니다.
⚠ 3g 가 arm 을 걷을 때 이 빈 분기는 **unhandled 경고를 막는 용도**로 남거나,
pre-scan 전용 payload 를 명시 목록으로 빼야 한다.

**저작 배율의 버킷 변환을 도메인에 복제하지 않았다.** 규칙이 자명하지 않다 —
「배율 ≥ 1 은 **가산 버킷**에 `배율−1`, 미만은 곱셈 버킷에 배율 그대로」이고,
누적 상한 계산이 그 버킷 선택에 매여 있어 한쪽만 고치면 **조용히 한 스택만큼 어긋난다**
(그 함수가 스스로 그렇게 경고한다). 그래서 `SkillCombineOp.FromAuthoredMultiplier` 를
두어 **도메인은 「이건 저작 배율이다」까지만 말하고** 변환은 어댑터가 한다.
⚠ 처음엔 `Multiplicative` 로 하드코딩했다가 그 함수를 읽고 잡았다 — 그물이 아니라
**코드를 읽어서** 잡힌 축이라 회귀 그물이 따로 없다.

**스택 상한의 `tileRange` 겸직을 끊었다.** 카드 경로가 「반경」 칸을 상한으로 쓰고
있었는데, 라이브 저작 실측에서 그 겸직값(0/5)과 스택 SO 의 상한(4종 전부 5)이
**같은 값**이라 끊어도 동작이 안 변한다. 광역판과 규칙이 하나가 됐다.

**라우팅은 payload 단위이고 seam 은 트리거 단위라, 둘이 어긋나도 안전하다.**
`SelfStatBuff` 는 `AttackN`·`HealthThreshold`(seam 있음)와 `OnKill`(seam 없음)로
저작된다. 라우팅 분기가 있는 감지자에서는 seam 으로 가고, 없는 감지자에서는 legacy
arm 이 그대로 돈다 — **이중 발화도 조용한 죽음도 없다.** 이게 이전 중 상태의 안전 조건이다.

## 구현

1. **카드 authoring 을 바꾸지 않는다.** 카드 mechanics 는 시트가 덮는 유일한 경로
   (`DcSheetApplier.OverlayMechanics` — `so.mechanics[slot]` 에 값만 덮고 구조·projectile ref 는
   불변)라, 어댑터가 카드 mechanics → `skillId`+params 로 번역한다.
   **이 spec 의 시트 손실은 0.**
2. **트리거 분포**(실측): `AttackN` 11 · `OnKill` 4 · `OnDamagedN` 2 · `OnShieldBreak` 2 ·
   `OnDeath` 2 · `OnRetire` 2 · 나머지. 그물이 가족 선행이다(계약 3).
3. **`RESOLVE` arm 은 감지 시점의 프레임-로컬 값을 소비한다** — `bestTarget`/`bestTargetPos`
   스냅샷(`AttackSystem.cs:1798~1997`), 넉백 방향 계산, 킬 스탬프(killer 통행 층).
   드레인 시점에 재질의하면 **틀린다.** 토대 unit 4 의 값 스냅샷 계약이 여기서 실제로 쓰인다.
4. **`DamageApplicationSystem` 은 이미 목표 구조에 가깝다** — `ShieldBreakEvent` →
   `DrainShieldBreakEvents` 가 이미 「스냅샷 + 이벤트 + 실행기」 모양이다.
   이 가족에서 그 형태를 `SkillFiredEvent` 로 흡수할지, 채널을 남길지 판정한다(후속 후보).
5. **문안 포매터를 저작 SO 로 옮긴다** — `UI/Dreamcatcher/DreamcatcherCardText.cs` 의 20 case.
   ⚠ 카드 문안은 **formatter 가 에셋 `description` 을 이긴다**(에셋 텍스트는 폴백).
   타입 필드가 생기면 case 별 스칼라 해석이 필드명 열람으로 대체된다.
   **도메인으로 옮기면 계약 1 위반이다** — 저작 계층 소유.
6. **부착 자격은 `ISkill` 요구 플래그로 선언만 한다** — `Core/Dreamcatcher/DcApplicability.cs`
   (25 case)는 이미 ECS 무참조 순수이고 case 내용이 스킬의 자기 서술이다.
   판정기 자체는 잔존시킨다(전면 이관은 후속 후보).

## 완료 기준

- [ ] 슬롯 경유 카드 행이 concrete 로 존재하고 legacy arm 이 죽었다
- [ ] **카드 에셋·시트 무변경** (`git diff` 로 `Data/Dreamcatcher/` 변경 0 확인)
- [ ] `RESOLVE` arm 이 값 스냅샷을 받아 동작한다 (재질의 없음)
- [ ] 문안이 저작 SO 필드에서 나오고 formatter 우선순위가 유지된다
- [ ] `BattleBridge.Dreamcatcher.cs` 의 payload arm 이 사라졌다
- [ ] 그물 초록 + Play 로 대표 카드 5장 육안
