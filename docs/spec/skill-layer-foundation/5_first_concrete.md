# 5 — 첫 concrete: 자장가

## 목적

경로 전체를 **하나의 스킬로 관통**한다. 저작 SO → bake → 슬롯 → 감지 → `SkillFiredEvent` →
디스패처 → `ISkill.Execute` → intent → 어댑터 → 소유 맥락 채널.

자장가(`DcPayloadKind.AreaSleep`)를 고르는 이유: 질의(반경 내 상대 진영 · 거리 랭킹 ·
상한)와 의도(CC 부여)가 둘 다 대표적이고, 오늘 그 선별 로직
(`BossPeriodicTriggerSystem.cs:294~317` skip-rank)이 **단위 테스트가 아예 불가능**한 자리에 있다.
이전하면 처음으로 테스트 표면에 올라온다.

## 변경 대상

- 신설 `Assets/_Project/Scripts/Skills/Concrete/AreaSleepSkill.cs` (도메인 — ECS 참조 0)
- 신설 `Assets/_Project/Data/UnitSkills/UnitSkill_AreaSleep.asset` + `SkillDescriptor` 파생
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 가 `skillId` 를 굽는다
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — 해당 arm 을 enqueue 로
- `Assets/_Project/Tests/EditMode/` — `TestSkillContext` 기반 단위 테스트

⚠ **이름 충돌 주의**: `Data/Skills/Skill_*.asset` 은 액티브 드림캐쳐(`SkillData`)가 점유 중이다.
이 spec 은 `Data/UnitSkills/UnitSkill_*.asset` 을 쓴다.

## 구현

1. **`AreaSleepSkill : ISkill`** — `Execute(caster, in params, ctx)`.
   `ctx.Opponents(caster, r, filters)` → 거리 랭킹 + skip-rank → `ctx.Emit(CcIntent{Sleep})`.
   concrete 안에 진영도 host 종류도 없다.
2. **저작 SO** — `magnitude`/`duration`/`tileRange` 겸직을 **타입 필드**로 푼다
   (`SleepCount` · `Radius` · `Duration`). unit 0 이 정한 typed params 형식을 따른다.
3. **라우팅** — 이 스킬을 쓰는 에셋만 `skillId != 0` 으로 굽는다. 나머지는 legacy arm 그대로.
4. **legacy arm 은 아직 지우지 않는다.** 이 unit 은 두 경로 공존이 정상이다.
   철거는 `skill-layer-migration` 의 해당 가족 문서에서.
5. **`EnemyCcEvent` 를 그대로 쓴다.** 새 채널을 만들지 않는다 — 어댑터가 기존 소유 맥락
   채널에 enqueue 하는 것이 계약 3 이다.

## 완료 기준

- [ ] `AreaSleepSkill.cs` 에 ECS·UnityEngine 참조 **0** (asmdef 가 컴파일로 강제)
- [ ] `TestSkillContext` 로 skip-rank 선별을 검증하는 단위 테스트 초록 — **ECS 월드 없이**
- [ ] 마메모의 자장가가 인게임에서 **이전 전과 동일하게** 동작한다 (unit 1 그물 대조)
- [ ] 같은 `UnitSkill_AreaSleep` 에셋을 **다른 host** 에 슬롯 한 줄로 달면 코드 0줄로 동작한다
- [ ] legacy arm 과 신 경로가 공존하고 `skillId` 로 갈린다
- [ ] EditMode 코어 lane + Assets lane 초록, Play 스모크 1회
