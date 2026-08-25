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
| **3a** | **공격 seam 개통** — `AttackSystem` RESOLVE 가 `SkillFiredEvent` 를 넣는다. 값 스냅샷 계약이 여기서 처음 실제로 쓰인다(9단 타겟팅 결과는 재현 불가) | — |
| **3b** | `AttackN` payload 들 (11행) — `HeavyStrike` · `ApplyCcToTarget` · `ApplyStackToTarget` · `ProjectileToTarget` · `SelfStatBuff` | 3a |
| **3c** | **죽음 seam 개통** — `OnKill`/`OnDeath`/`OnDamagedN`. ⚠ 드레인 시점에 host 가 **이미 없다**(unit 0 실측) | — |
| **3d** | 죽음 계열 payload (8행) | 3c |
| **3e** | `OnShieldBreak`/`OnRetire` seam + payload (4행) | — |
| **3f** | 부착 자격만 있는 5행 + 남은 payload | |
| **3g** | 카드 bake 전면 개방 + 카드 arm 철거 | 위 전부 |

⚠ **3g 는 unit 8 의 전제이기도 하다** — 카드 경로가 열리기 전에는 arm 을 못 지운다.

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
