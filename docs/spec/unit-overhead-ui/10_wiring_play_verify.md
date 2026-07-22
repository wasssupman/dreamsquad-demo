# 10. 아이콘 registry + 씬 배선 + Play 검증 [wiring]

## 목적

Codex 아이콘(unit 9)을 registry 에 매핑하고 씬 레이어에 배선 → 스택행 활성화.

## 변경 대상

- **신규** `Assets/_Project/Data/StackIconRegistry.asset` — `Fatigue→icon_stack_fatigue`, `Heat→icon_stack_heat`.
- `Assets/_Project/Scenes/BattleScene.unity` — `UnitOverheadUiLayer.stackIcons` = 위 registry (GameObject `DcIconStripSpawner`).

## 구현

- Registry SO 생성(MCP `manage_scriptable_object`): entries[0]=Fatigue+fatigue guid, entries[1]=Heat+heat guid. 참조 해석 확인(icon_stack_fatigue/heat).
- 씬 배선: `UnitOverheadUiLayer.stackIcons` → registry(guid 5aebe865…). BattleScene diff 는 **stackIcons 한 줄로 격리**(무관 재직렬화 드리프트 — 타 세션 CostDisplay/tooltip 스크립트 변경 —는 HEAD 로 되돌림, 커밋 오염 방지).

## 완료 기준

- Unity 컴파일/콘솔 에러 0. ✅
- Registry 참조 2매핑 해석 OK. ✅ 씬 stackIcons 배선 OK(diff 격리). ✅
- ⚠ **Play 육안 검증(사용자)**: 온천/번아웃 기믹 매치에서 유닛 머리 위 드림캐쳐 행 위에 열기/피로도 아이콘 + 카운트 배지가 뜨는지. (awakening 테스트지급은 원복됨 — 정상 흐름으로 스택 축적 후 확인.)
