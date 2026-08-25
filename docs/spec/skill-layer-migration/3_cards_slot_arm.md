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
