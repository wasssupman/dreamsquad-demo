# unit 0 — 라이브 풀을 Duel 한 장으로 (스테퍼만 예외)

## 목적

스테퍼(`DevMapOverridePanel`)로 지정하지 않는 모든 진입에서 **항상 Duel** 이 뜬다.

## 변경 대상

- `Assets/_Project/Data/Maps/MapDocumentPool.asset` — **이 파일 하나**
- (파생) 스테퍼 슬롯 번호를 상수로 박아 둔 PlayMode 테스트 5개 파일

## 구현

`entries` 에 Duel 만 남기고 기존 6장을 `devEntries` **앞쪽**에 옮긴다.

```
entries:    [Duel + Deck_Duel]
devEntries: [Serpent, Coil, Twin, Spiral, Zig, Hook,   ← 이전 라이브 6장
             Test, MovementLab, SiegeTest,             ← 기존 dev
             Ford, Isle, Tutorial]
```

**코드는 한 줄도 안 바뀐다.** `BattleBridge` 의 시드 3분기(디버그 시드 · 토너먼트 시드 ·
폴백0)가 전부 `MapPoolSelect` 를 지나고, 그 함수는 이미 `count <= 1` 이면 0을 돌려준다.
스테퍼 분기는 그 앞에 있고 `Count + DevCount` 범위를 그대로 해석한다.

### 파생 비용 — 슬롯 번호를 상수로 박은 테스트

스테퍼 슬롯 = `entries.Count + devIndex`. `Count` 가 6 → 1 이 되어 열 개의 상수가 한꺼번에
밀렸다. **숫자를 갱신하는 대신 축을 바꿨다**(사용자 지시 2026-08-17): 테스트는 이제
`BattleBridgeTestAccess.MapSlot("Duel")` 로 **이름**으로 고른다.

밀린 숫자를 고치는 것보다 이게 중요한 이유: 슬롯이 밀린 테스트는 **빨간불이 아니라 조용히
다른 판을 잰다.** `MapSlot` 은 이름이 풀에 없으면 실패하므로 그 실패 모드가 사라진다.

| 파일 | 무엇으로 바뀌었나 |
|---|---|
| `StructureLivePlayTest` | `MapSlot("Test"/"SiegeTest"/"Coil")` |
| `WaypointRoutingLiveTest` | `[Values("Coil","Zig")]` · `[Values("Duel","Ford","Isle")]` · `MapSlot("MovementLab"/"Tutorial")` |
| `SpawnGuideMatchesWalkTest` | `MapSlot("Duel")` · `MapSlot("Coil")` |
| `MapDocumentPoolDevEntriesTests` | `pool.Count` 6→1 + Duel 조회를 entries 까지 확장 (여긴 풀 구조 자체가 논점이라 의도된 의존) |

### 씬을 띄우는 테스트는 자기 판을 선언한다

라이브 맵이 바뀌자 판을 **선언하지 않은** 계측·메커니즘 테스트가 조용히 Duel 에서 돌아 4개가
깨졌다(Duel 엔 본능 포탑 4기가 있어 «반격할 게 없다» 같은 전제가 거짓이 된다).
→ `BattleBridgeTestAccess.PinMap()`(기본 `Serpent`) + `RestoreMap` 을 `DreamcatcherGateE2ETest` ·
`OnPlaceStunNearbyTest` · `OnPlaceTauntNearbyTest` · `WhirlpotLiveRepro` 에 붙였다.

같은 이유로 **계측형 3개는 삭제했다**(사용자 결정) — `InstinctNearestTargetMeasureTest` ·
`BossLullabyLiveTest` · `MapCrowdClearanceTest`. 랜덤 매치 시드 위에서 emergent 타이밍
대소를 단언해 통과/실패가 운이었고, 원래 목적(설계 질문에 숫자로 답하기)은 이미 스펙에
기록돼 있다. ⚠ `MapCrowdClearanceTest` 는 **군집 통과 교착의 유일한 회귀 가드**였다 —
되살릴 땐 결정론적 픽스처로.

## 완료 기준

- [x] EditMode(Assets) 173/173 · EditMode(코어) 2,352/2,352(skip 3 = 기존 Ignored) — 2026-08-17
- [x] PlayMode 169건 — **19 실패 → 13 실패** (2026-08-17). 남은 13건 전부 무관 근거 확보:
      문서화 사전 실패 8 + 신규 분류 5(`docs/spec/README.md` 의 «PlayMode 사전 실패» 절).
      맵 고정으로 4건(`DreamcatcherGate`·`OnPlaceStun`·`Whirlpot` 2건), 계측형 삭제로 2건 해소
- [ ] Play: 로비 → 전투 진입(스테퍼 OFF)이 Duel. 콘솔 `map pool index=0/1(+dev 12)`
- [ ] Play: 스테퍼로 다른 슬롯 지정 → 그 맵이 뜬다
