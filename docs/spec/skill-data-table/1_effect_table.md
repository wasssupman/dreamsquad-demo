# 1a · 1b — 효과 표 + 안정 id (코어)

## 목적
코어 정의표에 **효과 표**를 두고, 규칙 줄(`BindingDef`)은 효과 값을 들지 않고 효과 id(표 인덱스 + 안정 `Id` 문자열)를 가리킨다. 탄·패턴·장판 표와 같은 방식(계약 1).

## 변경 대상
- `Scripts/BattleCore/Match/MatchDefinition.cs` — `EffectDef` 표(`string Id` + 오늘 `BindingDef` 의 효과 칸들) · `MatchDefinition.Effects`.
- `Scripts/BattleCore/Trigger/BindingDef.cs` — 효과 칸 → `EffectIndex`. 트리거 · 주체 · 게이트 · 수명 칸은 남는다.
- `TriggerDispatcher` · `IntentApplier` · `CoreSkillContext` — 효과 값을 효과 표에서 읽는다(`BindingDef.ToParams` 자리).
- 빌더(`BindingDefinitionBuilder` · `CardDefinitionBuilder`) — 이 unit 에서는 **오늘 저작을 그대로** 효과 표로 번역(같은 값 = 같은 줄 dedupe, id = 저작 경로에서 파생한 임시 id). 저작 형식 통합은 unit 4.
- **1a**(컴파일 안전): 효과 표 + 읽기 접근자만. 옛 효과 칸은 남기고 소비처 전부를 접근자로 — 목록 밖 소비처 포함: `CombatPhase` · `CoreEvent` · `CommandPhase` · `Applicability` · `RangeCatalog` · `CardProbe` · 뷰(`CoreVfxSpawner` · `CoreCardDragSlot`).
- **1b**: 옛 칸 제거 · 효과 칸을 직접 채우는 테스트(약 21 파일) 이전 · **패턴·장판·슬램·길막 폭발 피해를 효과 줄로(U10 · `tables.md` §6 — 디버그 장판 스폰 명령이 피해를 싣는다)**.
- 인스턴스 값은 효과 표로 올리지 않는다: 호접몽 `StackId = InstanceId`(`BindingRegistry.cs:220`) · 부착 캐스트 FireCap/Lifetime 은 **규칙 인스턴스**에 남긴다.
- 런타임 조립 기믹 줄(`CoreEffect` · `DefIndex=-1`)은 효과 표 밖 — 지금 경로 유지(범위 밖 명시).
- 해시: README 계약 8(해석된 값만 · id·순서 제외) — 목표 재베이크 0.

## 완료 기준
- 굽기 스냅샷: 작성기가 효과 줄을 **해석해 오늘과 같은 텍스트 형식**을 내게 하고 **파일 diff 0** 으로 동치 증명(재생성하지 않는다).
- 헤드리스(`verify-fresh-skills.sh`) · EditMode 3 · 골든 11 · PlayMode Core(빌더 경로) · 리뷰 = 묶음(1a–3).

- 1a 구현 2026-09-28 · `26829ee61`(이름) · `34edea449`(효과 표) — 헤드리스 1006/5 · Unity EditMode 3 어셈블리 2624 중 실패 = 선행 2 + 카드 아트 1 · 굽기 스냅샷 2종 파일 무변 · 골든 11 일치. 리뷰 = 묶음(1a–3).
- 1b 구현 2026-09-28 · `f8df37837`(인라인 칸 제거 · 테스트 이전) · `4e225742b`(U10 피해 → 효과 줄) · `64539a1e7`(스냅샷 격리 재생성 — 예상 diff 와 정확히 일치) — 헤드리스 1006/5 · EditMode 3 어셈블리 2625 중 실패 = 선행 3(문안 2 + 카드 아트) · 골든 11 · PlayMode Core 97/97. 리뷰 = 묶음(1a–3).
