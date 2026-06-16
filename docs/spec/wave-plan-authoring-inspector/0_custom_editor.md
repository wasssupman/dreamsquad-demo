# 0 — WavePlanAssetEditor (CustomEditor)

## 목적

`WavePlanAsset` 인스펙터를 경량 CustomEditor 로 대체해 웨이브/그룹 작성 편의를 높인다. SerializedProperty 기반(Undo/dirty/멀티 정상). 데이터 모델·런타임 무변경.

## 변경 대상

- 신규: `Assets/_Project/Editor/WavePlanAssetEditor.cs` (`Wassup.Editor`, `[CustomEditor(typeof(WavePlanAsset))]`).

## 구현

- 헤더: `displayName`, `timerDurationSec`(0=endless 라벨).
- 플랜 요약: 총 웨이브 수 / 총 전투 길이(Σ durationSec) / 총 스폰 수(Σ count).
- 웨이브 박스(접기 = element.isExpanded): `durationSec`, `intervalSec`, 그룹 행 목록.
  - 그룹 행: `unit`(ObjectField AttackUnitData) + `triggerTimeSec`(0~durationSec) + `count` + 제거(✕).
  - 그룹 추가/웨이브 추가·복제(InsertArrayElementAtIndex)·위/아래 이동(MoveArrayElement)·제거(DeleteArrayElementAtIndex).
  - 읽기전용 타임라인 바: GetRect + EditorGUI.DrawRect. x = (triggerTimeSec/max(durationSec,ε))·width 위치에 그룹 마커.
- 검증 HelpBox(경고만, 저장 안 막음): 웨이브 0개 / 빈 그룹 / unit null / count≤0 / triggerTimeSec<0 또는 >durationSec.

## 완료 기준

- 컴파일 0, 콘솔 에러 0.
- 에셋 선택 시 CustomEditor 가 로드되고 add/remove/duplicate/move 가 SerializedProperty 로 동작(Undo 가능).
- 잘못된 입력에 경고 HelpBox 표시. 타임라인 바·요약 표시.
- (시각 최종 확인은 사용자.)

---

*완료 확인*: 2026-06-17 — 컴파일 0, 콘솔 에러 0. `Editor.CreateEditor` 가 `Wassup.Editor.WavePlanAssetEditor` 반환(등록 확인). SerializedProperty add/group/duplicate/move/delete 전부 예외 없이 동작, 편집 데이터 FromPlanAsset 변환 정상. 에셋 선택+repaint 시 OnInspectorGUI 무에러. 커밋 `89b8064`.
