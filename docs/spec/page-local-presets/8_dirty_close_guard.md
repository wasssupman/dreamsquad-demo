# Unit 8 — 미저장 변경 닫기 가드

> 상태: **구현 완료 2026-07-31** — 집중 EditMode 8/8 통과 · 전체 EditMode
> 1754건 중 1752 통과/0 실패/2 기존 Ignore · Unity 컴파일 errors=0
> 범위: 스쿼드·드림캐쳐 페이지의 Close 버튼과 프리셋 드롭다운 전환

## 문제

각 페이지는 드롭다운에서 선택해 보고 있는 프리셋의 저장본과 별도 작업본을 가진다.
드롭다운 전환에는 이미 dirty 확인 팝업이 있지만, 페이지 Close 버튼은 공통
`OutgameMenuController.OnClosePanels`를 직접 호출해 작업본을 경고 없이 버린다.

## 계약

1. dirty 판정 기준은 별도 플래그가 아니라 **현재 보고 있는 프리셋의 저장본과 현재
   작업본의 `PresetDiff` 비교**다.
2. 스쿼드와 드림캐쳐 페이지 모두 clean 상태의 Close는 기존처럼 즉시 닫힌다.
3. dirty 상태의 Close는 페이지를 유지하고 다음 문구의 확인 팝업을 띄운다.
   - `저장하지 않은 변경이 있습니다.\n닫으면 변경은 사라집니다.`
   - 확인 버튼: `닫기`
4. 팝업에서 취소하면 페이지와 작업본을 그대로 유지한다.
5. 팝업에서 확인하면 기존 공통 닫기 동작을 정확히 한 번 실행한다.
6. dirty 상태인데 `confirmPopup`이 주입되지 않았으면 닫지 않고 `LogError`를 남긴다
   (기존 프리셋 전환과 같은 fail-closed 정책).
7. 프리셋 드롭다운 전환의 기존 dirty 경고 계약은 유지한다.
   - 취소: 현재 프리셋과 작업본 유지
   - 확인: 대상 프리셋의 저장본을 새 작업본으로 로드

## 구현 경계

- 씬의 Close 버튼 배선은 유지한다.
- `OutgameMenuController.OnClosePanels`가 활성 스쿼드/드림캐쳐 컨트롤러에 닫기
  요청을 위임하고, 실제 닫기는 확인 콜백으로 기존 `ClosePanels`를 호출한다.
- 테스트 모드·히스토리 등 다른 패널의 닫기 동작은 변경하지 않는다.
- 신규 UI나 의존성은 추가하지 않고 기존 `ConfirmPopup`을 재사용한다.

## 검증

- [x] 스쿼드 clean/dirty Close EditMode 회귀
- [x] 드림캐쳐 clean/dirty Close EditMode 회귀
- [x] dirty + 팝업 미주입 fail-closed 회귀
- [x] 공통 메뉴 Close 라우팅 회귀
- [x] 프리셋 관련 집중 EditMode 테스트 — 8/8
- [x] 전체 EditMode 테스트 1회 — 1754건 중 1752 통과/0 실패/2 기존 Ignore
- [x] Unity 컴파일 errors=0

## 완료 내역

- 두 페이지 컨트롤러에 저장본/작업본 기반 `RequestClose` 가드를 추가했다.
- 공통 `OnClosePanels`가 활성 스쿼드/드림캐쳐 페이지에만 닫기 요청을 위임한다.
- 씬·프리팹·팝업 레이아웃 변경 없이 기존 `ConfirmPopup`을 재사용했다.
- 스쿼드 드롭다운 확인 이동과 드림캐쳐 드롭다운 취소 유지도 함께 회귀로 고정했다.
