# 3 · OutgameScene 와이어링 + Play 검증

## 목적

hello/world 캐릭터 GameObject 에 `LobbyKeyringDrag` 를 부착하고 SO 를 할당,
Play 에서 검증 질문에 답한다. 필요 시 SO 라이브 튜닝으로 feel 을 확정한다.

## 변경 대상

- 수정: `Assets/_Project/Scenes/OutgameScene.unity` —
  `MenuCanvas/LobbyCharacters` 하위 hello·world 오브젝트
- 수정(필요 시): `Assets/_Project/Data/Config/LobbyKeyringSettings.asset` 튜닝값

## 구현

UnityMCP 로 자동 와이어링 (사용자 수작업 금지):

1. hello / world GameObject 에 `LobbyKeyringDrag` AddComponent,
   `settings` 에 `LobbyKeyringSettings.asset` 할당.
2. 캐릭터 Image 의 `raycastTarget` 이 켜져 있는지 확인 (클릭 리액션이 이미
   동작하므로 켜져 있을 것 — 드래그도 같은 raycast 경로).
3. Play 진입 (로그인 게이트 통과 상태) 후 검증:
   - [ ] 캐릭터 스와이프 → 고리+줄+매달린 캐릭터, 손가락 따라 스윙 (탄성 지연)
   - [ ] 빠른 스와이프에도 캐릭터가 튀어나가지 않음 (maxSpeed)
   - [ ] 공중에서 놓기 → 가속 낙하 → 작은 바운스 1회 → 바닥 정지
   - [ ] 착지 후 hello 는 그 자리에서 로밍 재개, world 는 idle
   - [ ] 로밍 범위 밖에 떨어진 hello 가 다음 목적지 추첨으로 자연 복귀
   - [ ] 낙하 중 재잡기 동작
   - [ ] 드래그 중/직후 클릭 리액션 미발화, 단순 클릭 리액션은 기존대로 동작
   - [ ] 드래그 중 다른 캐릭터 클릭 리액션은 정상 (락 공존)
   - [ ] 콘솔 에러/워닝 0
4. feel 어색 시 SO 값 라이브 튜닝 (에셋 편집 즉시 반영) 후 확정값 저장.

## 완료 기준

- 위 체크리스트 전 항목 통과 (사용자 확인 포함).
- 씬 저장 후 diff 가 캐릭터 2개의 컴포넌트 추가 범위를 넘지 않는다.

확인 2026-07-07 — 사용자 Play 통과 확인. 커밋 `249ae848` (씬의 무관 변경은 헝크
선별로 제외, 워크트리 보존).
