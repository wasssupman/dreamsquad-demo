# 1 — 원점 산출기 하나 + 발사 요청 한 갈래 (코어 · H1 · H2)

## 목적
- **H1**: 효과가 원점을 스스로 추정하지 않게 한다. 트리거 종류 → 원점(좌표 · 원점 항 · 선택적 대상)을 **한 곳**이 내고, 효과 concrete 는 그 값만 읽는다.
- **H2**: `IntentApplier.SpawnProjectile` 의 대상/자리 두 갈래를 **궤적 결합 종류** 하나로.

## 변경 대상 (unit 0 표 1·2 로 확정)
- 신설 `Scripts/BattleCore/Trigger/EffectOrigin.cs` — 값 struct(좌표 · 원점 몸 · 대상 id · 발동 주체 id) + 정적 산출 함수(`TriggerEvent` + 트리거 종류 → `EffectOrigin`). 입력·출력 plain(제약 10). 오늘 감지자·`CoreSkillContext` 에 흩어진 「자리·몸 채우기」 규칙을 여기로.
- `Scripts/BattleCore/Trigger/TriggerDispatcher.cs` · `CoreSkillContext.cs` — 드레인 때 산출기를 한 번 불러 스킬 입력(`SkillTarget` · `SkillParams` 의 원점 필드)을 채운다.
- `Scripts/Skills/` 입력 형(`SkillTarget`/`SkillParams`) — 원점 필드 하나로 수렴(오늘 `CellA` · `EventPosition`/`EventBodyRadius` · `ctx.Position(caster)` 셋).
- 원점을 읽는 concrete 전부(표 2) — 원점 필드만 읽게. 대표: `TileMeteorSkill` · `SelfAreaBlastSkill` · `DeathSiteBlastSkill` · `TargetProjectileSkill` · `EmitPatternSkill`.
- `Scripts/BattleCore/Trigger/IntentApplier.cs` `SpawnProjectile` — 한 갈래.

## 구현
- 결합 종류별 채움(한 함수 안의 표): **대상**(호밍) = 대상 추적 · `TileRange` = 재조준 반경 / **칸**(낙하·포물선) = 착탄 = 대상이 있으면 그 좌표, 없으면 원점 좌표 · `TileRange` = 착탄 반경 · `Duration` = 비행 · 예고 = 효과 파라미터(U1) / **방향** = `TileRange` = 사거리.
- 궤적 = 의도의 명시 궤적 > 탄 정의. 「대상 없으면 하늘 낙하」 강제를 지우고 자리형 concrete 가 명시한다.
- 원점 항: 몸형은 발동 주체 몸(사망은 발화 시점 스냅샷 — 오늘 `Site↔SiteBody`), 자리형은 0(= 칸 반폭). 산출기가 트리거 종류로 정하고 concrete 는 모른다 — `DeathSiteBlastSkill` 헤더의 「누구의 자리인가는 감지자가 정한다」를 산출기가 잇는다.

## 완료 기준
- 탐침 `타격_운석의_반경은_바인딩_TileRange_다` · `타격_운석에도_착탄_예고가_뜬다`(예고 켠 파라미터) 해제 초록 · 짝 `현행_…` 뒤집거나 삭제.
- 산출기 단위 테스트: 트리거 종류 전부 1케이스 이상(표 1 의 행 = 테스트 행).
- 무변: 헤드리스(클린 export) · 골든 11(Unity) · EditMode Core(선행 외 0) · `EffectWitness`.
- `CoreArchitectureTests` 에 「원점 필드 외의 위치 읽기가 concrete 에 없다」 소스 단언.
