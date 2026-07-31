# 5 — 씬 배선 + Play 검증 + handoff

## 목적

OutgameScene 에 배선하고 실제 서버 데이터로 e2e 를 확인한다. 배선을 사용자 수작업으로 미루지 않는다.

> **완료 2026-07-30.** 씬 배선 + 실서버 Play e2e 확인. 아래 "실측 결과" 참조.

## 변경 대상

- `Assets/_Project/Scenes/OutgameScene.unity`
- `docs/spec/tournament-history-deck-view/5_handoff_summary.md` (신규)

## 구현

**배선**

- `TournamentHistoryPanel` 에 카탈로그 3종(`DefenderCatalog` / `DreamstoneCatalog` / `DreamcatcherCardCatalog`) `SerializeField` 할당. 팝업은 패널이 지연 생성하며 카탈로그를 주입한다.
- 은퇴한 `TournamentDetailPopup` 이 씬에 오브젝트로 남아 있으면 함께 제거한다.
- 로비 히스토리 버튼 → 패널 경로(`OutgameMenuController.RaiseExclusive`)는 기존 그대로.

> 씬 저장은 사용자 미저장 WIP 를 통째로 베이크한다. 내 delta 만 격리해 커밋한다(스냅샷 → `git checkout HEAD -- OutgameScene.unity` → 내 변경만 재적용 → 커밋 → 복원). 커밋 후 씬 WIP 잔존을 사용자에게 고지한다.

## Play 검증

1. 로그인 → 로비 → 히스토리 버튼 → **최근 토너먼트가 선택된 채** 랭킹이 채워진다. 목록 행에 `yyyy.MM.dd HH:mm` 이 병기된다.
2. 다른 토너먼트를 고르면 우측만 갈아끼워진다. 빠르게 여러 번 눌러도 **엉뚱한 랭킹이 남지 않는다**(epoch 가드).
3. 내 행에서 덱보기 → 그 판에 들고 간 유닛/스톤/카드가 보인다.
4. 다른 참가자 행에서 덱보기 → 그 사람 덱이 보인다. `deckInfo` 가 없는 참가자(구 엔트리)는 "덱 정보가 없습니다".
5. 탭 전환, 항목 선택, 닫기/재열기.
6. 목록이 빈 계정(또는 강제로 빈 응답)에서 안내 문구.

콘솔에 예외 0. 실패한 조회가 있어도 로비가 막히지 않는다.

## 라이브 왕복 검증 (`tournament-deck-info` 이관분)

앞 spec 이 남긴 미확인 2건을 **이 화면으로** 닫는다. 위 3번이 그 확인 수단이다.

1. **왕복이 성립하는가** — 한 판 완주 후 히스토리에서 내 덱보기를 연다. 그 판의 로컬 로그(`GameLogs/session-*.json`)의 `squad.unitIds` / `dreamstones[].id` / `dreamcatcher.baseDeckCardIds` 와 화면이 일치해야 한다. `baseDeckCardIds` 는 **선물 카드가 빠진 고른 덱**이다(`deckCardIds` 는 선물 포함).
2. ~~**0점 마감이 덮어쓰지 않는가**~~ — **폐기 (2026-07-31)**: 엔트리 하나 = 판 하나라 덮어쓸 대상이 없다. 나가기로 끝낸 판은 자기 엔트리에 자기 덱을 남긴다(`tournament-deck-info` unit 4).

판정:

- 1 실패 → 서버에 덱이 안 쌓인 것이다. UI 문제가 아니므로 `complete` 요청 body 부터 되짚는다.
- 2 실패 → 클라는 이미 값 없으면 키를 안 보낸다. 클라를 더 고치는 게 아니라 **서버에 최고점 가드를 요청**하고, README 에 "최근 판의 덱이 아닐 수 있음"을 계약으로 남긴다.
- 통과 → `docs/spec/tournament-deck-info/README.md` 의 "미확인" 섹션을 결과로 갱신해 이관을 종료한다.

## 실측 결과 (2026-07-30, dev 서버 · 계정 `wassup`)

- 히스토리 **22건** 로드, `2026.07.30 22:57` 부터 **최신순** 정렬, 첫 항목 자동 선택.
- 랭킹 **5슬롯**(`maxEntryCount`), 덱보기 버튼 5개, 내 행 하이라이트.
- 내 덱보기 → **스쿼드 탭 셀 11개**(유닛 7 + 드림스톤 4), **드림캐쳐 탭 셀 10개**. 전부 카탈로그로 해석됨(한글 표시명·포트레이트·카드 아트).
- **선물 카드 분리가 라이브에서 확인됨** — 같은 판의 로컬 로그 `deckCardIds` 는 12장(저장 10 + `active_meteor`/`active_rapid_fire`)인데 화면은 10장. `baseDeckCardIds` 가 의도대로 동작.
- 콘솔 에러 0.

**Play 에서만 드러난 결함 3건** (EditMode 는 전부 통과했었다):

1. **`createdTime` 이 epoch 밀리초** — 날짜 칸이 항상 비어 있었고 최신순 정렬도 무효였다. 계약 4 참조. 이게 unit 0 의 "최근 자동 포커스"를 조용히 무너뜨리고 있었다.
2. **중첩 캔버스의 `overrideSorting` 이 활성화 시점에 풀린다** — 비활성 상태에서 `BuildCanvas` 가 세팅해도 `SetActive(true)` 후 `false/2500` 으로 돌아온다. `Show()` 에서 활성화 뒤 다시 박는다.
3. **모달 배경 알파 0.98 이면 뒤 텍스트가 읽힌다** — 어두운 패널 위 흰 글자라 2% 도 눈에 띈다. 팝업은 1.0 으로.

## 완료 기준

- [x] 씬 배선 완료 (카탈로그 3종 할당. 은퇴한 `TournamentDetailPopup` 은 씬/프리팹 참조가 원래 0이라 제거할 오브젝트가 없었다 — 런타임 생성이었다)
- [x] 위 Play 검증 통과, 콘솔 예외 0 (6번 '빈 목록 안내'는 실계정에 22건이 있어 라이브로는 못 봤다 — EditMode 로 고정)
- [x] EditMode 1658 tests green (이 spec 신규 25건 포함)
- [x] 코드 리뷰 1회 (프레젠테이션/Mono 변경이므로 일반 리뷰 — ECS 리뷰 대상 아님)
- [x] 라이브 왕복 검증 — **왕복 성립 확인**. 0점 덮어쓰기는 **미확인**(나가기 판을 따로 만들지 않았다)
- [x] `docs/spec/tournament-deck-info/README.md` 의 "미확인" 갱신 (1번 닫힘, 2번 존치)
- [x] `5_handoff_summary.md` 작성
