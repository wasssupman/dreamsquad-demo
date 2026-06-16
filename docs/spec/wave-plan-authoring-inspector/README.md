# Wave Plan Authoring Inspector Spec

**작성일**: 2026-06-17
**상태**: 초안 — 승인 대기. 구현 전.
**선행 spec**: `docs/spec/wave-authoring-test-mode/` (완료. `WavePlanAsset`/`TestModeContext`/`SceneNames`/저장 스쿼드 반입 경로 제공)

## 상위 목표

`WavePlanAsset` 을 기본 인스펙터(중첩 리스트 폴드아웃)보다 **편하게 작성**할 수 있는 경량 CustomEditor 를 추가한다. 런타임/데이터 모델/결정론은 무변경 — 순수 에디터 편의성. 추가로 인스펙터에서 **"이 플랜으로 바로 테스트"** 를 눌러 BattleScene Play 진입할 수 있게 한다.

## 검증 질문

`WavePlanAsset` 을 선택했을 때, 웨이브/그룹을 버튼으로 빠르게 추가·삭제·복제하고 적 SO·시각·수량을 인라인 편집하며, 잘못된 입력(시각>durationSec 등)을 경고로 보고, 웨이브별 타임라인과 총합 요약을 한눈에 볼 수 있는가? 그리고 "테스트" 버튼으로 그 플랜이 BattleScene Play 로 바로 뜨는가?

## 작업 단위

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | CustomEditor | `0_custom_editor.md` | `WavePlanAssetEditor` — 웨이브/그룹 추가·삭제·복제, 인라인 편집, 검증 HelpBox, 읽기전용 타임라인 바, 총합 요약. SerializedProperty 기반(Undo/dirty 정상). |
| 1 | 테스트 런치 | `1_test_launch.md` | 인스펙터 "▶ Test this plan" 버튼 + `SessionState` 캐리 + `playModeStateChanged(EnteredPlayMode)` 부트스트랩으로 `TestModeContext.Set` → BattleScene Play. |
| 2 | Handoff | `2_handoff_summary.md` | 종료 요약. |

의존 순서: `0 → 1`.

## Feature-wide 계약

- **에디터 전용**: 모든 코드는 `Assets/_Project/Editor/` (asmdef 없음 → `Assembly-CSharp-Editor`, `Wassup.Runtime` 자동 참조). 네임스페이스 `Wassup.Editor`. 런타임 asmdef·데이터 모델·결정론 무변경.
- **데이터 모델 불변**: `WavePlanAsset`/`AuthoredWave`/`AuthoredSpawnGroup` 필드 그대로 사용. 새 필드 추가 없음. 편집은 `SerializedObject`/`SerializedProperty` 로만(Undo·멀티오브젝트·dirty 보장).
- **타이밍 모델 계승**: 웨이브=`durationSec`(N) 구간, 그룹별 `triggerTimeSec`(0~N) 상대, 웨이브 `intervalSec`. 타임라인 시각화·검증은 이 의미를 따른다.
- **검증은 경고만**: 잘못된 입력(시각<0 / 시각>durationSec / unit null / count≤0 / 빈 웨이브·그룹)은 HelpBox 경고로 표시하되 저장은 막지 않는다(작성자 자유).
- **테스트 런치 캐리**: Play 도메인 리로드로 static 초기화되므로, 선택 플랜은 `SessionState`(에디터 세션) 에 GUID 로 저장하고 `EnteredPlayMode` 에서 `TestModeContext.Set(plan, null)` 적용(디펜더는 GameManager 의 저장 스쿼드 반입). 1회 적용 후 키 제거.
- **테스트 런치는 BattleScene 기준**: 버튼은 BattleScene 을 열고(필요 시 저장 프롬프트) Play 진입. 아웃게임 피커 경로는 그대로 유지(병행).

## 비목표 / 후속 후보

- 타임라인 **드래그로 이동**(핸들) — 본 spec 은 읽기전용 시각화까지. 인터랙티브 드래그는 후속.
- 전용 EditorWindow(독립 창) — 본 spec 은 인스펙터 CustomEditor.
- 플랜 밸런싱/난이도 보조, import/export, 적 미리보기 썸네일.
- 런타임(인게임) 작성 UI.

## 참고

- 기존 CustomEditor 패턴: `Assets/_Project/Editor/PropDataEditor.cs`(`[CustomEditor]`+버튼), `SeasonBackdropDataEditor.cs`.
- 런타임 진입 계약: `TestModeContext.Set(plan, preset)` → `GameManager.Start` 테스트 분기(드래프트 스킵, 저장 스쿼드). `SceneNames.Battle`.
