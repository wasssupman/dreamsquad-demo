# 2. 로비 버튼 + 씬 wiring

## 목적

Outgame(로비) 화면에 "스탯 갱신" 버튼을 추가하고 `UnitStatRuntimeRefresher` 와 배선한다. 릴리즈 빌드에서는 숨긴다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/` — 버튼 View (기존 Outgame UI 패턴 따름, `OutgameMenuController` 연계)
- Outgame 씬 — 버튼 GameObject + `UnitStatRuntimeRefresher` 컴포넌트 + SerializeField 배선 (UnityMCP 로 자동화, 씬 저장 포함)
- 카탈로그 참조 배선: `DefenderCatalog.asset` / `EnemyCatalog.asset`

## 구현

- **게이트**: `Debug.isDebugBuild || Application.isEditor` 가 아닐 때 버튼 GameObject `SetActive(false)`. (릴리즈 APK 에서 미노출)
- **UX**: 버튼 탭 → 진행 중 비활성("갱신 중...") → 완료 시 결과 요약 1~2줄 표시 (매칭/실패). 요청 중복 방지 플래그.
- **UI 레이어**: 기존 Outgame UI 의 UiLayer 규칙 적용 (프로젝트 UI 스윕 컨벤션 준수).
- unity-feature-wiring 스킬 절차로 씬 배선 + Play 검증까지 완료해야 이 unit 종료. 사용자 수작업 이관 금지.

## 완료 기준

- [ ] compile 오류 없음
- [ ] 에디터 Play: 로비에서 버튼 탭 → "Matched 25, unmatched 0..." 표시, 시트 값 변경 후 재탭 → 다음 전투에서 새 수치 반영 확인 (예: 특정 적 attackRange)
- [ ] 실기기(Development Build): 동일 동작 1회 확인
- [ ] 릴리즈 모드(비-dev 빌드 또는 게이트 강제 false): 버튼 미노출 확인
