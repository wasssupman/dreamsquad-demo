# 0. `tournament.seed` 파싱

## 목적

`/tournament/play` 응답에서 서버 토너먼트 시드를 클라가 읽는다. 실측 응답(2026-07-23 dev 서버):

```json
{ "success": true, "data": {
    "tournament": { "seed": 9128566303723636648, "status": "IN_PROGRESS", ... },
    "userTournamentState": { "tournamentEntryId": "...", "tournamentEntryAttemptId": "...", ... } } }
```

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentApi.cs`
- `Assets/_Project/Tests/EditMode/` — 기존 TournamentApi 파싱 테스트 파일에 케이스 추가 (있으면 이어 쓰고, 없으면 신설)

## 구현

1. `PlayState` 에 `public TournamentInfo tournament;` 추가. `TournamentInfo` 는 **소비 필드만** 미러링(파일 선례): `public ulong seed;`
2. seed 는 서버 uint64 — 실측값이 `long.MaxValue` 에 근접하므로 `ulong` 사용. Newtonsoft `ToObject<T>` 바인딩으로 충분(코드 변경은 클래스 선언뿐).
3. 파싱 실패/필드 부재는 기존 봉투 규약 그대로 — `tournament` 가 없어도 `PlayState` 는 유효(=null 로 남음). 시드 유무 판정은 유닛 1 의 몫.

## 완료 기준

- [x] 실측 응답 body 로 `TryParsePlay` → `state.tournament.seed == 9128566303723636648`
- [x] `tournament` 노드 없는 body(구 스키마 방어) → `state != null`, `state.tournament == null` (기존 3필드 소비 무회귀)
- [x] compile 0 error, EditMode green

확인 2026-07-23 — testrig 배치 EditMode 1289 중 1287 green(신규 파스 테스트 2 포함, 2 skip=기존 Ignored).
