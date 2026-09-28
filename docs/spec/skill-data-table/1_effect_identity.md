# 1 — 효과 표 + 안정 id (코어)

## 목적
코어 정의표에 **효과 표**를 두고, 규칙 줄(`BindingDef`)은 효과 값을 들지 않고 효과 id(표 인덱스 + 안정 `Id` 문자열)를 가리킨다. 탄·패턴·장판 표와 같은 방식(계약 1).

## 변경 대상
- `Scripts/BattleCore/Match/MatchDefinition.cs` — `EffectDef` 표(`string Id` + 오늘 `BindingDef` 의 효과 칸들) · `MatchDefinition.Effects`.
- `Scripts/BattleCore/Trigger/BindingDef.cs` — 효과 칸 → `EffectIndex`. 트리거 · 주체 · 게이트 · 수명 칸은 남는다.
- `TriggerDispatcher` · `IntentApplier` · `CoreSkillContext` — 효과 값을 효과 표에서 읽는다(`BindingDef.ToParams` 자리).
- 빌더(`BindingDefinitionBuilder` · `CardDefinitionBuilder`) — 이 unit 에서는 **오늘 저작을 그대로** 효과 표로 번역(같은 값 = 같은 줄 dedupe, id = 저작 경로에서 파생한 임시 id). 저작 형식 통합은 unit 4.
- 해시: `Canonicalize` 에 효과 표. **재현 키가 바뀐다** → 골든·라이브 기준선 재베이크는 **이 unit 의 마지막 격리 커밋**.

## 완료 기준
- 굽기 스냅샷(카드 · 유닛 능력 · 악몽)을 **효과 해석 후 값 기준**으로 대조해 전후 동치(형식 변경분은 스냅샷 재생성 · 줄 단위 1:1 확인 — unit 7 절차).
- 하드 케이스 1 탐침에 「A 와 AA 가 **같은 효과 줄**을 가리킨다」 단언 신설.
- 헤드리스(`verify-fresh-skills.sh`) · EditMode 3 · 골든 11(재베이크 사유 기록) · core-reviewer.
