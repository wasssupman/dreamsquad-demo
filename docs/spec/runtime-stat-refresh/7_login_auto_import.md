# 7 — 로그인 성공 시 전체 자동 임포트 1회

## 목적

모바일 빌드에서 QA 가 버튼을 누르는 걸 잊어도 항상 최신 시트값으로 플레이하도록, **로그인 단계를 통과하는 순간 전체 임포트(8탭)를 1회 자동 실행**한다. unit 6 의 `AllRuntimeRefresher` 를 그대로 재사용한다.

선행: unit 6 (`AllRuntimeRefresher` 필요).

## 변경 대상

- `Assets/_Project/Scripts/Core/LoginAutoImport.cs` (신규)
- `Assets/_Project/Tests/EditMode/LoginAutoImportTests.cs` (신규)
- `Assets/_Project/Scenes/OutgameScene.unity` (컴포넌트 1개 배선)

## 구현

`LoginAutoImport : MonoBehaviour` — `LoginPanelView.onSignedIn` 을 구독해 리프레셔를 1회 구동한다.

1. `[SerializeField] private LoginPanelView loginPanel;` + `[SerializeField] private MonoBehaviour refresherSource;` (인터페이스 직렬화 불가 → `StatRefreshButtonView` 와 같은 캐스팅 패턴).
2. `Awake` 에서 dev 게이트 — `!Debug.isDebugBuild && !Application.isEditor` 면 구독하지 않고 컴포넌트를 끈다. 릴리즈 빌드는 dev API 를 부르지 않는다 (feature-wide "노출 게이트" 계약 승계).
3. `onSignedIn` 구독은 `OnEnable`/`OnDisable` 이 아니라 `Awake`/`OnDestroy` 쌍으로 — `OutgameMenuController:35,49` 의 기존 패턴과 맞춘다.
4. **세션당 1회.** `bool _done` 가드. `onSignedIn` 은 로그인 성공(`LoginPanelView:165`)·스킵/게스트(`:85,96`)·재방문 자동 로그인이 모두 태우는 단일 seam 이고, 계정 리셋 후 재로그인하면 다시 실행된다(의도 — 최신값 재확보).
5. **비블로킹.** 메뉴 노출을 막지 않는다. `OutgameMenuController` 의 `onSignedIn → ApplyAuthGate` 경로는 그대로 두고, 임포트는 백그라운드로 돈다. 결과는 기존 `Debug.Log` 로 남긴다.

`OutgameMenuController` / `LoginPanelView` / `StatRefreshButtonView` 는 수정하지 않는다. 새 인터페이스·매니저·싱글톤·설정 SO 를 만들지 않는다.

테스트를 위해 트리거 코어를 `internal void TriggerOnce(IRuntimeRefresher r)` 로 분리하고 fake 로 구동한다 (`DcSheetRuntimeRefresher.ApplyBodies` 선례 — 네트워크 없이 EditMode).

## 주의 (승인 시 확인)

- **비블로킹 선택의 대가**: 임포트가 착지하기 전에 전투를 시작하면 그 판은 빌드값으로 돈다. 로비 진입 직후 몇 초라 실사용상 드물고, 블로킹은 오프라인/저속망에서 로비를 최대 30초(`SheetFetcher` timeout) 잠근다고 판단해 비블로킹으로 잡았다. 로그인 화면에서 임포트 완료까지 대기시키길 원하면 이 유닛의 설계가 바뀐다.
- dev 게이트 표현이 3번째 복제된다(`DevOnlyGroup:11`, `StatRefreshButtonView:29`). 기존 선례가 이미 2곳에 인라인이라 헬퍼 추출은 하지 않는다.

## 완료 기준

- [ ] 로그인 성공 후 자동으로 8탭 임포트가 1회 실행되고 로그에 결과가 남는다.
- [ ] 스킵(게스트) 진입과 재방문 자동 로그인에서도 동일하게 1회 실행된다.
- [ ] 같은 세션에서 `onSignedIn` 이 여러 번 떠도 임포트는 1회만 (`_done` 가드).
- [ ] 계정 리셋 → 재로그인 시 다시 1회 실행된다.
- [ ] 네트워크 실패해도 로그인·로비 진입·전투 시작이 막히지 않는다 (빌드값 유지).
- [ ] 릴리즈 빌드에서 자동 임포트가 실행되지 않는다 (dev API 미호출).
- [ ] `OutgameMenuController` / `LoginPanelView` / `StatRefreshButtonView` diff 0.
- [ ] EditMode 테스트 green: fake `IRuntimeRefresher` 로 (a) onSignedIn 1회 → Refresh 1회, (b) 다중 발화 → 1회 가드.
- [ ] compile clean, 에디터 Play 에서 로그인/스킵 두 경로 검증.
