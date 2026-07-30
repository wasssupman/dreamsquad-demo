# authored-preset-removal — 기획자 authoring 프리셋 페이지 철거

> 상태: **초안 2026-07-30**
> 선행: 없음 (자기증적 — 이 spec 만으로 컴파일·Play 그린)
> 후속: `page-local-presets` (플레이어 소유 프리셋). **이 spec 이 먼저 커밋된다.**
> 성격: 삭제 전용. 신규 동작 0. MonoBehaviour/Editor/에셋 제거 + 씬 정리.

## 목표

로비 **프리셋** 페이지와 그 뒤의 authored 프리셋 파이프라인 전체를 제거한다.

현재 프리셋은 **기획자가 시트로 authoring 하는 읽기전용 콘텐츠**다 — `SquadPresetCollection` SO 를 `Presets` 시트 탭에서 채우고, 페이지에서 목록으로 보여주고, [적용]이 현재 스쿼드·덱을 덮어쓴다. 후속 spec 이 도입하는 **플레이어 소유 편집 가능 프리셋**과는 개념이 다르며 공존할 이유가 없다. 새 코드가 미끼 옆에서 자라지 않도록 먼저 걷어낸다.

### 검증 질문

로비에서 프리셋 버튼이 사라지고, authored 프리셋 관련 타입·에셋·시트 경로가 전부 제거된 상태에서 **컴파일이 그린이고 기존 로비/스쿼드/드림캐쳐/스탯 시트 push·import 가 모두 정상 동작하는가?**

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 삭제 (UI) | `0_page_and_views.md` | 로비 진입점 + 페이지·뷰 3종 제거, `PresetConfirmPopup` 은 `ConfirmPopup` 으로 개명 존치 |
| 1 | 삭제 (데이터·시트) | `1_data_and_sheet_path.md` | SO·에셋·`PresetApply`·시트 import/export/push 경로·런타임 refresher 제거 |
| 2 | 삭제 (레거시) | `2_dead_legacy_builders.md` | 사문화된 `SquadBuilderView`·`DreamcatcherDeckBuilderView` 제거 |

순서: 0 → 1 → 2. 각 단위가 독립 커밋이고 어느 지점에서 멈춰도 컴파일 그린이다.

## feature-wide 계약

1. **삭제만 한다.** 신규 기능·리팩터를 끼워넣지 않는다. 예외는 `PresetConfirmPopup` → `ConfirmPopup` 개명 하나이며, 그 근거는 계약 2다.
2. **`ConfirmPopup` 존치.** `PresetConfirmPopup` 은 실제로 preset 전용이 아니라 dim + 메시지 + [취소]/[확인] 범용 위젯이고(하드코딩 `"적용"` 라벨만 특수), 후속 spec 의 미저장 경고 팝업이 정확히 이 위젯을 요구한다. **개명 + 확인 라벨 파라미터화**해서 남긴다. 삭제하고 세 번째 확인 팝업을 새로 쓰지 않는다.
3. **무관한 `*Preset*` 을 건드리지 않는다.** 프로젝트에 이름만 겹치는 무관 자산이 다수 있다 — `Assets/Layer Lab/**`(2D Art Maker), `Assets/Spine/Editor/**/ImporterPresets`, `Assets/_Project/Data/Camera/CameraPreset_*.asset`, `Assets/_Project/Scripts/Data/BoardCameraPreset.cs`, `Assets/_Project/Editor/LayerLabPresetImporter.cs`. **삭제 대상은 각 단위 문서에 열거된 경로뿐이다.**
4. **`.cs` 삭제 시 `.cs.meta` 를 짝으로 삭제**하고 같은 커밋에 담는다. 에셋(`.asset`)도 동일. meta 누락은 타 머신에서 GUID 재생성 → 씬 참조 파괴를 부른다.
5. **씬 정리는 UnityMCP 로 자동화한다.** 프리셋 패널 GameObject·로비 버튼·`AllRuntimeRefresher.refresherSources` 배열 엔트리를 사용자 수작업으로 미루지 않는다. Play 검증까지가 완료.
6. **서버 시트의 `Presets` 탭은 남는다.** 클라이언트가 push 바디에서 그 탭을 빼는 것뿐이고, Apps Script 라우팅·시트 자체는 손대지 않는다(무해한 미사용 탭).
7. **은퇴 spec 표기.** `docs/spec/loadout-preset-page/README.md` 와 `docs/spec/preset-sheet-import/README.md` 상단에 은퇴 사유 + 이 spec 포인터를 한 줄 남긴다. 문서를 지우지는 않는다(이력 보존).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트(유닛/적/투사체/해저드/VFX)나 생성→렌더 경로 변경이 없다. 아웃게임 UI·Editor 툴·프로필 헬퍼만 제거하므로 `docs/reference/object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보

- `Assets/_Project/Art/LobbyIcons/icon_preset.png` — 로비 버튼 제거로 고아가 된다. 후속 spec 의 프리셋 바가 아이콘을 재활용할 수 있어 이번엔 남긴다.
- `IRuntimeRefresher` 구현체가 4개 → 3개로 줄어든다(여전히 인터페이스 규칙 충족). 더 줄어들면 인터페이스 존치 여부 재검토.
