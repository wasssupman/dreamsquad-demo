# 6 — IMPORT ALL 버튼 (전체 데이터 임포트)

## 목적

로비에서 버튼 한 번으로 **시트 백엔드의 전 데이터(8탭)** 를 내려받는다. 현재는 `IMPORT UNIT`(Defenders/Enemies 2탭)과 `IMPORT DREAMCATCHER`(Dc 6탭)를 따로 눌러야 하고, 둘을 한 번에 하는 경로가 없다. 기존 두 버튼은 그대로 두고 세 번째 버튼을 추가한다.

`IRuntimeRefresher` 를 fan-out 하는 composite 구현체 1개만 신설한다. `StatRefreshButtonView` 는 이미 refresher-agnostic 이라 **코드 변경 0**이다.

## 변경 대상

- `Assets/_Project/Scripts/Core/AllRuntimeRefresher.cs` (신규)
- `Assets/_Project/Tests/EditMode/AllRuntimeRefresherTests.cs` (신규)
- `Assets/_Project/Scenes/OutgameScene.unity` (버튼 1개 + 컴포넌트 배선)

## 구현

`AllRuntimeRefresher : MonoBehaviour, IRuntimeRefresher` — 자식 리프레셔들을 **동시** 실행하고 조인한다.

1. `[SerializeField] private MonoBehaviour[] refresherSources` — Unity 가 인터페이스 참조를 직렬화 못 하므로 `StatRefreshButtonView.refresherSource` 와 같은 패턴(MonoBehaviour 로 받아 캐스팅)을 쓴다.
2. `RequestInFlight` — 자체 플래그. 진행 중 재호출은 기존 리프레셔들과 동일하게 `"refresh already in progress"` 로 즉시 콜백.
3. `Refresh(onDone)` — 자식 각각의 `Refresh` 를 호출하고, 남은 개수 카운터로 조인한다. `SheetFetcher.FetchAll` 과 동일하게 콜백이 메인 스레드로 오므로 lock 불필요. **onDone 은 정확히 1회.**
4. **로그 첫 줄 = 합산 요약.** `StatRefreshButtonView` 는 `FirstLine(log)` 만 결과 라벨에 표시하므로(`StatRefreshButtonView.cs:78`), 첫 줄에 `ALL: 2/2 ok` 같은 요약을 넣고 그 아래 자식별 로그를 헤더와 함께 이어 붙인다.
5. 캐스팅 실패/빈 배열은 경고 로그 후 그 자식을 건너뛴다. 게임 진행을 막지 않는다.

테스트를 위해 fan-out 코어를 `internal void RefreshAll(IRuntimeRefresher[] children, Action<string> onDone)` 로 두고 `Refresh` 는 직렬화된 배열을 캐스팅해 그걸 부른다 — `DcSheetRuntimeRefresher.ApplyBodies` 가 "network 없이 EditMode 로 구동" 하려고 쓴 것과 같은 수법이다.

새 인터페이스·매니저·싱글톤·설정 SO 를 만들지 않는다. `IRuntimeRefresher` 는 이미 구현체 2개로 존재하므로 3번째(composite)를 붙이는 것이지 신설이 아니다.

씬 배선: 기존 두 리프레셔가 붙은 GameObject 에 `AllRuntimeRefresher` 를 추가하고 `refresherSources` 에 둘을 물린다. 버튼은 기존 두 버튼과 같은 부모에 복제하고 `idleLabel = "IMPORT ALL"`, `refresherSource = AllRuntimeRefresher` 로 설정한다.

## 완료 기준

- [x] `IMPORT ALL` 1회 클릭으로 8탭(Defenders/Enemies + Dc 6탭)이 모두 fetch 되고, 두 리프레셔의 적용 결과가 한 로그에 합쳐진다. — Play 실측 `Matched 26`(디펜더16+적10) / `Matched 68`(Dc), 양쪽 unmatched 0.
- [x] `onDone` 이 정확히 1회 호출된다(조인). 결과 라벨에 합산 요약 첫 줄이 뜬다. — `RequestInFlight` true→false + 라벨 복귀로 확인.
- [x] 한쪽이 실패해도 성공한 쪽은 적용되고 실패 사유가 로그에 남는다 (feature-wide "실패 처리: 시트별 독립" 승계). — EditMode 로 검증(실 네트워크 실패는 미유발).
- [x] 진행 중 재클릭이 중복 요청을 만들지 않는다. — EditMode 로 검증.
- [x] 릴리즈 빌드에서 세 버튼 모두 숨겨지고 개발빌드/에디터에서만 보인다 (기존 게이트 재사용 — 코드 변경 0). — 기존 버튼과 동일 코드 경로 승계, 릴리즈 빌드 실측은 미실시(unit 2 잔여와 동일).
- [x] 기존 `IMPORT UNIT` / `IMPORT DREAMCATCHER` 버튼 무회귀. — 배선(각자 refresherSource) 유지 확인. 클릭 재검증은 미실시.
- [x] `StatRefreshButtonView` diff 0.
- [x] EditMode 테스트 green: fake `IRuntimeRefresher` 2개로 (a) 조인 후 1회 콜백, (b) 로그 합침, (c) in-flight 가드, (d) 자식 1개 실패 시 나머지 적용. — 전체 817 passed/0 failed.
- [x] compile clean, 에디터 Play 에서 클릭 검증.

확인 2026-07-15 — composite fan-out/조인 + 3번째 버튼. 두 가지를 함께 처리했다: (1) **`ResetAccountButton` 이 `DreamcatcherRefreshButton`(unit 4, y=-232) 밑에 8px 차이로 90% 깔려** 렌더·레이캐스트를 모두 뺏기고 있던 회귀를 발견해 88 간격 컬럼으로 재배치(-408). (2) MCP `set_property` 로 `refresherSources` 를 물리면 **success 를 반환하고도 length 2 전부 NULL** 인 배열이 되는 함정 — 리플렉션으로 재배선 후 YAML(`247752173`/`247752175`)까지 확인. 첫 Play 검증이 아니었으면 `"no refreshers wired"` 만 뱉는 버튼이 그대로 커밋될 뻔했다.
