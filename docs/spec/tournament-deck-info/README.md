# tournament-deck-info

> 상태: 완료 2026-07-30 (units 0~2)
> EditMode 1633 tests green. **라이브 서버 왕복 검증은 후속 spec(히스토리 덱 보기 페이지)으로 이관** — 2026-07-30 사용자 결정. 지금은 `deckInfo` 를 화면에 그리는 곳이 없어 눈으로 확인할 대상이 없고, 그 페이지가 곧 확인 수단이다. 이관된 확인 2건은 아래 "미확인" 참조.

## 한 줄

`complete` 요청 body 를 **덱 정보 하나로 갈아끼운다** — 새로 생긴 `deckInfo` 를 채우고, 지금까지 `debug` 로 올리던 **배틀 로그 전문은 전송을 중단**한다. 그리고 결과 조회 응답의 참가별 `deckInfo` 를 파싱한다.

## 배경 (왜)

2026-07-30 서버 명세 변경 (`/v3/api-docs` 대조):

- `POST /tournament/complete/{attemptId}/{score}` 의 requestBody `TournamentResultExtraData` 에 **`deckInfo: string`** 추가 (기존 `debug` 와 나란히).
- `GET /tournament/result/tournament/{entryId}` 응답 `entries[].deckInfo: string` 추가 — *"최고 점수를 기록한 판의 덱 정보 — 해당 기록이 없는 과거 참가는 null"*.
- `TournamentEntry` 엔티티에도 컬럼이 있다 = **엔트리(참가)당 1개**이고, 서버가 최고 점수 attempt 의 것으로 갱신한다.

현재 클라는 body 에 `debug` 만 실으므로 `deckInfo` 는 항상 null 이다. **추가 필드라 기존 동작이 깨지지는 않는다** — 고장 수리가 아니라 미채움 상태를 채우는 작업이다.

**동시에 `debug` 는 내린다** (2026-07-30 사용자 결정). 여기 실리던 것은 배틀 로그 **전문**(웨이브 기록·배치 이력·드림캐쳐 오퍼 로그 전체)이고, 소비처가 정해지지 않은 채 매 판 올라가고 있었다. 서버가 실제로 필요로 하는 건 덱 구성이다. **로컬 로그 파일은 그대로다** — `BattleLogger.EndSession` 이 `GameLogs/` 에 쓰는 전문 기록은 무변경이므로 디버깅 수단은 잃지 않는다. 없어지는 것은 네트워크 전송 경로뿐이다.

## 검증 질문

1. 매치를 완주한 뒤 `GET result` 로 내 엔트리를 조회하면, 그 판에 들고 간 유닛/드림스톤/드림캐쳐 카드 id 가 `deckInfo` 에 그대로 돌아오는가?
2. 0점 마감 경로(메뉴 나가기 / 로비 reconcile)가 엉뚱한 덱을 기록하지 않는가?

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 포맷 + 순수 함수 | `0_deck_info_payload.md` | `TournamentDeckInfo` — v1 페이로드 계약 + 시리얼/디시리얼 순수 static + EditMode 테스트 |
| 1 | body 교체 | `1_complete_send_wiring.md` | body 를 `deckInfo` 단독으로. `debug`/`SnapshotJson` 전송 경로 제거 + `BattleLogger.DeckInfoJson()` 신설 |
| 2 | 수신 파싱 + 라이브 왕복 | `2_result_entry_parse.md` | `ResultEntry.deckInfo` 바인딩 + 실제 매치 완주 → curl 로 왕복 확인 |

## Feature-wide 계약

1. **포맷은 v1, id 만 담는다.** 표시명·아트·등급은 넣지 않는다.

   ```json
   {"v":1,"squad":{"units":["defender_shotgunner"],"stones":["ds_atk_unique"]},"dc":{"cards":["dc_push"]}}
   ```

   **`dc.cards` 는 플레이어가 고른 덱만**이다 — 매 판 랜덤으로 얹히는 선물 카드(Lucid 롤 Active / Rim 무의식 2장)는 제외한다. 로드아웃 비교가 이 데이터의 목적이고 선물은 노이즈다. 인게임에서 실제로 돌린 조합 덱은 로컬 로그(`dreamcatcher.deckCardIds`)에 그대로 남는다.

2. **이름의 해석기는 카탈로그 하나뿐이다.** `DefenderCatalog` / `DreamstoneCatalog` / `DreamcatcherCardCatalog` 의 `ById` 가 이름·아트·등급을 전부 준다. 로컬 덱 UI(`SquadHeaderStrip`, `DreamcatcherDeckStrip`, `SquadCharacterPageController`)가 이미 id 배열만으로 그리고 있으므로 표시 경로는 그대로 재사용된다.

   **이름을 스냅샷하지 않는 이유**: `displayName` 은 시트 구동이다(`UnitStatImportDto.displayName`, `DcSheetImportDto.displayName` 이 리플렉션 매핑으로 로그인 시 SO 에 적용). 이름을 페이로드에 굳히면 시트에서 이름을 바꾼 순간 옛 엔트리만 옛 이름으로 남아 한 화면에 두 이름이 공존한다. id 해석은 항상 현재 이름으로 통일된다.

3. **미해석 id 는 그 슬롯만 폴백한다.** id 리네임·삭제, 또는 내 빌드에 없는 신규 id 를 상대가 기록한 경우 → 카탈로그 miss → 빈 슬롯/raw id (저장 덱의 기존 실패 모드와 동일). 페이로드 전체를 버리지 않는다. `v` 필드가 필드 확장의 탈출구다.

4. **실점수 경로만 덱을 기록한다.** `ReportResult` 만 채우고, 0점 마감 두 경로는 빈 문자열이다. `AbandonMatch` 는 최고점 후보가 아니고, `ReconcilePending` 은 **이전 세션/하드킬의 attempt** 를 뒤늦게 닫는 것이라 지금 메모리의 덱을 붙이면 남의 판에 엉뚱한 덱을 기록한다.

5. **body 에는 있는 것만 싣는다.** `debug` 는 키 자체를 보내지 않는다 — 빈 문자열로 채우는 것도 아니다. 배틀 로그를 네트워크로 올리는 경로는 이 spec 에서 종료된다. **`deckInfo` 도 값이 없으면 키를 뺀다**(0점 마감 경로 → `{}`): 서버가 엔트리 컬럼을 최고점 가드 없이 매 complete 마다 대입한다면 빈 값이 좋은 판의 덱 기록을 덮어쓴다. 서버 문구는 가드가 있음을 시사하나 확인된 사실이 아니고, 키를 빼는 쪽이 어느 서버 동작에서도 더 나쁘지 않다. **확인은 unit 2 라이브 검증 4번**이다.

6. **로컬 배틀 로그는 기존 항목 무변경.** `EndSession` 파일 출력과 **`SnapshotJson()` 을 그대로 남긴다** (2026-07-30 사용자 결정 — 전송만 끊고 메서드는 유지). 유일한 변경은 `dreamcatcher.baseDeckCardIds` **필드 추가**(선물을 뺀 고른 덱) — 기존 `deckCardIds`(조합 덱)는 의미까지 그대로다. 로컬 로그는 "실제로 뭘 돌렸나"를, 토너먼트 기록은 "뭘 골랐나"를 답한다.

7. **역직렬화는 관대하다.** 빈 문자열/파싱 실패는 예외가 아니라 `null` 이다. 누락 노드와 리스트 **원소**(null·빈 문자열)까지 정규화해 소비처가 null 체크를 반복하지 않게 한다. **버전 게이트는 비대칭**이다 — 미래 버전(`v > Version`)은 `null`(구버전 클라가 오해석하면 없는 슬롯을 그린다), **과거 버전은 받는다**. 여기서 하한까지 막으면 `v` 를 올리는 순간 백카탈로그 전체가 "덱 정보 없음"이 되어, `v` 가 확장의 탈출구가 아니라 단절선이 된다.

8. **표시 UI 는 범위 밖이다.** 이번 spec 은 "데이터가 서버에 쌓이고 다시 읽힌다"까지다. 상대 덱을 화면에 그리는 것은 별도 spec (아래 후속 후보).

## 미확인 (후속 spec 에서 반드시 확인)

라이브 왕복을 못 돌린 상태로 종료했으므로, 히스토리 덱 보기 페이지를 만들 때 **먼저** 이 둘을 확인한다. 둘 다 서버 동작에 대한 클라의 추정이다.

1. **덱이 실제로 왕복하는가** — 완주한 판의 유닛/스톤/카드 id 가 `entries[].deckInfo` 로 그대로 돌아오는가.
2. **0점 마감이 기록을 덮어쓰지 않는가** — 좋은 판을 친 뒤 나가기(또는 다음 로비 reconcile)로 0점 complete 가 나갔을 때, 엔트리의 `deckInfo` 가 유지되는가. 계약 5 의 "키 생략"은 이 위험을 줄이는 조치일 뿐 **차단하지는 못한다** — 서버가 컬럼을 무조건 대입하면 null 로 덮인다. 덮이는 것으로 판명되면 0점 경로에서 complete 자체를 바꾸는 게 아니라 서버에 최고점 가드를 요청하는 쪽이 맞다(클라는 이미 보낼 것을 안 보내고 있다).

## 후속 후보

- **히스토리 덱 보기 페이지** (다음 spec — 2026-07-30 사용자 예고) — 토너먼트 히스토리에서 참가자의 덱을 보여주는 페이지. 진입점은 `TournamentHistoryPanel` → `TournamentDetailPopup`, 그리고 결과 화면의 `ResultScreen` 이 같은 행 모델(`LeaderboardList.Row`)을 공유한다. 표시는 로컬 덱 스트립 컴포넌트(`SquadHeaderStrip` / `DreamcatcherDeckStrip`) 재사용. **이 spec 의 `TournamentDeckInfo.Deserialize` 가 그 페이지의 입력**이다. Scope: M
- **`deckInfo` 없는 과거 엔트리 처리** — 서버가 null 로 주는 구 참가. 위 UI spec 에서 "덱 정보 없음" 표기로 흡수.
- **드림스톤 캐리인 로그가 미해석 id 를 버린다** — `GameManager.LogDreamstoneCarryIn` 은 `stoneCatalog.ById(id) == null` 이면 그 슬롯을 **기록에서 지운다**(유닛은 raw id 를 남기는 것과 비대칭). 시트에 새 스톤이 추가됐는데 로컬 SO 가 stale 한 빌드에서 플레이하면 장착한 스톤이 조용히 사라진 덱이 기록된다. 계약 3(미해석 id 는 슬롯만 폴백)을 스톤에도 적용하려면 raw id 를 남기도록 고쳐야 한다. 겸사겸사 `slotIndex` 유실(압축이라 "1번 슬롯만 장착"이 수신 측엔 0번으로 보임)도 같이 판단. Scope: S
