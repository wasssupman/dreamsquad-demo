# 5. Handoff — map-play-feel (유닛 0·1·2 완료)

## Commit

- `eaa299ed` unit 0 — 합류+비대칭 맵 Hook 추가
- `c8c00c7d` unit 1 — 기존 풀 5장 15폭 이하로 축소
- `b661d1b6` unit 2 — 개발 확인용 맵 인덱스 강제 (모바일 런타임)

(main 커밋, 미푸시. HEAD 기준 `0f490915`.)

## Implemented

- **Hook 맵**(13×12): 레인 러시 13 / 롱 25(비율 1.92) + 합류점 (9,5) + 공유 꼬리 5칸. 풀 유일의 비대칭+합류 맵. `Deck_Hook`(waveSeed 20260806)와 풀 6번째 등록.
- **풀 6장 전부 폭 ≤15**: Serpent 15×11, Coil·Twin·Spiral·Zig 15×12, Hook 13×12. 축소 시 꺾임 추가로 레인 길이 ±10% 보존(웨이브 시간 기반 난이도 드리프트 방지).
- **Zig 재이식**: 타 세션 12×10판(레인 −40%)을 커밋본 형태(180° 회전대칭 L자)로 15×12 재작성 → 레인 20/20 복원.
- **개발 맵 강제**: `DevMapOverride`(PlayerPrefs) + `BattleBridge` 우선순위 훅 + START GAME 위 스테퍼 UI. 모바일 개발빌드 런타임, 릴리스 자동 숨김. **임시 도구(정리되면 제거 예정).**

## Key Files

- `Assets/_Project/Data/Maps/MapDocument_Hook.asset`, `MapDocument_{Serpent,Coil,Twin,Spiral,Zig}.asset`, `MapDocumentPool.asset`
- `Assets/_Project/Scripts/Data/Decks/Deck_Hook.asset`
- `Assets/_Project/Scripts/Core/DevMapOverride.cs` — static holder, 우선순위 seam
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:875` — override 분기(9줄)
- `Assets/_Project/Scripts/UI/DevMapOverridePanel.cs` — 스테퍼 로직
- `Assets/_Project/Scenes/OutgameScene.unity` — `MenuButtons` 아래 패널

## Verified

- EditMode 1266 중 1264 green / 0 fail (testrig 배치, 축소본 포함)
- 6장 디스크 재파싱: 2×2 0·스폰 전원 도달·forest·레인 길이·파생값 불일치 0
- 패널 로직 reflection 전 케이스(순환/wrap/OFF/이름) + 씬 무결성(손실 0, 패널 8개만 순증가)
- 사용자 Play 확인 완료 (2026-07-24): override 로 맵 진입·렌더·pathing, 겹침 해소

## Notes

- **우선순위**: `DevMapOverride.HasIndex > fixedMapSeed > 토너먼트 시드 > 폴백 0`. 서버 API 파싱/리포팅 무변경, override 만 이김.
- **풀 6장 = seed%6**. Hook 강제는 override 스테퍼(권장) 또는 `fixedMapSeed=5`.
- **BattleBridge diff 9줄뿐** — 다른 세션 오염 없음(HEAD 0f490915 의 FrameBoard 는 이미 커밋됨). testrig(bc482780)가 뒤처져 그 커밋을 몰라 컴파일 에러났던 것.
- **커밋 위생**: 워킹트리에 타 세션 WIP 36개 dirty. 명시 pathspec 으로만 스테이징(add -u 금지 — 4차 사고 교훈).

## Follow-up

- **유닛 3 (`3_play_feel_tuning.md`)**: Hook 이 다른 맵과 다른 배치 순서를 강제하는지 체감 판정 — **미완**. 이번 사용자 확인은 "맵 진입/UI 동작"이지 "비대칭·합류 체감 판정"이 아니다.
- 개발 override UI 는 임시 — product 정리 시 제거 요청 대기.
- 후속 후보(README): 웨이브별 스폰 리듬, 진짜 분기/X-교차, 레인 분리 거리 축, 명당 리터치, 이펙트타일/포탈 오버레이.
- 라벨 초기 문구 `MAP?`(노랑) / OFF 버튼(회색) 구분.
