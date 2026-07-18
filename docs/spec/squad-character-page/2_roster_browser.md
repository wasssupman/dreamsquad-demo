# 2 — 리스트 브라우저 그리드 (SquadRosterBrowser)

## 목적

우 2/3 하단 브라우저. 유닛을 스크롤 그리드로 노출 — 셀 = 포트레이트 + 등급 프레임색 + 이름 + 선택 하이라이트 + "편성중" 뱃지. 셀 탭 → `EntrySelected(id)` 이벤트(상세 갱신은 unit 4 오케스트레이터가 연결). 설계 계약상 스톤 모드(unit 3)가 **같은 그리드**를 재사용하므로, 셀은 제네릭 엔트리(id/스프라이트/프레임색/라벨)로 짓고 unit 2는 유닛으로 채운다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadRosterBrowser.cs` (`Wassup.UI`)

## 구현

`SquadRosterBrowser : MonoBehaviour`
- SerializeField: `TMP_FontAsset font`, 셀/그리드 크기 상수(cellSize, spacing). 스크롤/그리드는 `SquadBuilderView` 피커의 검증된 구조(ScrollRect + Viewport(Mask) + Grid(GridLayoutGroup + ContentSizeFitter 세로))를 이 컴포넌트 RectTransform 하위에 절차적 생성.
- `event Action<string> EntrySelected;`
- `void ShowUnits(IReadOnlyList<DefenderUnitData> units)` — 유닛→엔트리(id, portrait, 등급색 프레임, displayName) 매핑 후 셀 재생성.
- `void SetSelected(string id)` — 이전 선택 해제 + 새 셀 하이라이트(스케일 up + 오버레이).
- `void SetBadged(ISet<string> ids)` — "편성중"(유닛) / "장착중"(스톤, unit 3) 뱃지 갱신. 셀 재생성 없이 토글.
- 셀 탭 → `EntrySelected?.Invoke(id)`.
- 내부: 엔트리 struct `{id, sprite, frameColor, label}`, 셀 참조 리스트 + `id→index` 맵. unit 3은 `ShowStones(...)`만 추가(엔트리 매핑 다름, 셀 기계 재사용).

## 완료 기준

- [x] 컴파일 클린(신규 .cs 2개 → scope=all refresh, 에러 0).
- [x] `ShowUnits` 셀 생성 + `SetSelected`(스케일+오버레이) / `SetBadged`(뱃지 토글) / 탭→`EntrySelected` 바인딩 완성. null-safe.
- [x] 등급 프레임색 → `UnitRarityStyle.Frame` 로 **추출**(unit 1도 이 공용 헬퍼 사용). 스크롤/그리드 = 피커 검증 구조.
- [x] 시각 검증 — Play 오버레이 프리뷰(2026-07-18)로 그리드(17유닛 6열)·등급 프레임색·한글 이름·"편성중" 초록 뱃지(3개) 정상 확인. 선택 하이라이트/스크롤 동작.

> 구현 2026-07-18 · 커밋 대기 (컴파일 클린 + Play 프리뷰 시각 검증 통과).
