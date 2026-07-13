# 4. 드림캐쳐 로비 버튼 + 버튼 일반화

## 목적

로비(OutgameScene)에 "IMPORT UNIT"·"IMPORT DREAMCATCHER" 두 버튼을 두어, 빌드(dev)에서 시트 유닛/드림캐쳐 밸런스를 각각 갱신한다. 버튼 로직이 두 리프레셔로 갈리므로 인터페이스로 일반화한다.

## 인터페이스 (구현체 2개 = 추출 정당, 제약 8 충족)

`Assets/_Project/Scripts/Core/IRuntimeRefresher.cs` (신설):
```
public interface IRuntimeRefresher {
    bool RequestInFlight { get; }
    void Refresh(System.Action<string> onDone);
}
```
`UnitStatRuntimeRefresher`·`DcSheetRuntimeRefresher` 둘 다 이미 이 시그니처를 가짐 → `: IRuntimeRefresher` 만 추가.

## 변경 대상

- `Assets/_Project/Scripts/Core/IRuntimeRefresher.cs` 신설, 두 리프레셔에 인터페이스 부착.
- `Assets/_Project/Scripts/UI/Outgame/StatRefreshButtonView.cs` 일반화:
  - `[SerializeField] UnitStatRuntimeRefresher refresher` → `[SerializeField] MonoBehaviour refresherSource` (인터페이스 직렬화 불가 → Mono 참조 후 캐스트). Awake 에서 `_refresher = refresherSource as IRuntimeRefresher`, null 이면 경고+비활성.
  - `IdleLabel` const → `[SerializeField] string idleLabel = "IMPORT UNIT"`. "REFRESHING..." 는 유지.
  - dev 게이트(`Debug.isDebugBuild || Application.isEditor`) 그대로.
- `OutgameScene.unity` 배선:
  - 기존 유닛 버튼: `refresherSource` = UnitStatRuntimeRefresher 재지정, idleLabel="IMPORT UNIT".
  - 신규 GameObject: `DcSheetRuntimeRefresher` 컴포넌트(cardCatalog/activeCards/awakeningConfig 배선) + 버튼 복제, `refresherSource`=DcSheetRuntimeRefresher, idleLabel="IMPORT DREAMCATCHER".

## 완료 기준

- [x] compile 0 error.
- [x] OutgameScene Play: DreamcatcherRefreshButton 활성 노출(dev 게이트 통과) + onClick → OnClick → Refresh 전 경로 완주(`[StatRefresh]` 콜백). YAML 배선 검증: 두 refresherSource 올바른 컴포넌트(IMPORT UNIT→UnitStatRuntimeRefresher, IMPORT DREAMCATCHER→DcSheetRuntimeRefresher), cardCatalog/activeCards(6)/awakeningConfig non-null.
- [x] 릴리즈 게이트: `Debug.isDebugBuild || Application.isEditor` (기존 유닛 버튼과 동일 코드 경로) — 결정: dev 빌드/에디터만 노출.
- [ ] (선택) 갱신 후 전투 진입 새 수치 반영 smoke — 코어 apply 는 editor 왕복·EditMode 로 검증됨, 미실시.

확인 2026-07-13 — 버튼 일반화(IRuntimeRefresher) + 2버튼 씬 배선 + Play 클릭 경로 완주. resultLabel 은 두 버튼 공유(dev 툴, 최근 클릭 결과 표시).
