# unit 9 — 담당 구역 = 소환사의 공격범위 (박스 3개 → 1개)

## 목적

**소환사의 `attackRange` 하나가 소환물이 커버하는 구역을 결정한다** (사용자 결정 2026-08-10).

지금은 «담당 구역»을 말하는 박스가 **셋으로 갈라져** 있고 서로 중심도 반경도 다르다:

| 무엇 | 중심 | 반경 |
|---|---|---|
| 배치 프리뷰 (`BattleBridge:5622`) | walk 스냅 셀 | `SummonPatrolAbility.leashTileRadius` |
| 소환 발동 게이트 (`AttackSystem:397`) | **소환사 셀** | 같은 값 |
| 실제 순찰 구역 (`PatrolAnchor`) | walk 스냅 셀 | 같은 값 |

중심이 어긋난 것은 우연이 아니라 계약 4(«거점은 소환사 셀이 **아니라** 최근접 walk 셀»)의 귀결이었다. 그 계약은 «방어유닛 셀은 걸을 수 없다»는 전제 위에 서 있었는데, **`traversal-layers` 가 그 전제를 없앴다** — 순찰병은 `Ground|Path` 로 저작돼 배치지를 걷는다. 전제가 사라졌으므로 세 박스를 하나로 접는다.

실측(Coil, leash 4): 프리뷰가 그리는 81칸 중 소환물이 실제로 밟는 곳은 앵커 주변 몇 칸이다. 플레이어가 읽는 약속과 유닛의 행동이 다르다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Abilities/SummonPatrolAbility.cs` — `leashTileRadius` 제거
- `Assets/_Project/Scripts/Battle/Combat/SummonerState.cs` — `leashTileRadius` 제거
- `Assets/_Project/Scripts/Battle/Combat/PatrolSpawnRequest.cs` — `leashTileRadius` → `coverTileRadius`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 게이트/요청 반경을 `RangeToTiles(attack.range)` 로
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 프리뷰 특수 분기 삭제 · `SummonerState` bake · 스폰 드레인 · 앵커 스냅
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — 재배치 시 앵커 = 새 소환사 셀
- `Assets/_Project/Data/Defenders/Defender_Summoner.asset` — `attackRange` 저작
- `Assets/_Project/Tests/EditMode/` — 회귀 2건

## 구현

### 출처 하나, 소비처 셋

```
출처   Defender_Summoner.attackRange
반경   GridMath.RangeToTiles(attackRange)
중심   소환사 셀
```

이 값을 **① 배치 프리뷰 ② 소환 발동 게이트 ③ 순찰 구역** 셋이 함께 본다.

**프리뷰는 특수 분기를 삭제해서** 맞춘다 — 추가가 아니라 제거다. 소환사가 다른 방어유닛과 같은 `SetPlacementRange(center, tileRange)` 로 떨어지면, "이 유닛의 공격범위" 라는 화면 언어가 소환사에게도 그대로 성립한다. unit 5 가 만든 leash 프리뷰는 여기서 은퇴한다.

`SummonPatrolAbility.leashTileRadius` 는 **중복 출처라서 지운다**. 능력 에셋은 «누구를 소환하나»만 소유하고 «얼마나 넓게 커버하나»는 유닛 스탯이다.

### 중심과 집은 다른 칸이다

초안은 `PatrolAnchor.cell` 하나가 박스 중심이자 대기 위치를 겸하게 뒀다. **틀렸다** — 소환물이 소환사와 같은 칸에 스폰돼 겹쳐 섰고, 플레이어에겐 "소환사에 박혀 안 움직인다"로 읽혔다(사용자 지적 2026-08-10). 두 값은 제약이 다르다:

| | 무엇 | 제약 |
|---|---|---|
| `cell` | 박스 중심 = 소환사 셀 | 통행 가능일 **필요 없음** (플레이어가 손가락 올린 칸) |
| `homeCell` | 대기·복귀·스폰 칸 | 반드시 **통행 가능** (순찰병이 실제로 서는 칸) |

```
집 = 소환사 셀을 제외한, 구역 안 최근접 통행 가능 칸   (통상 = 인접 칸)
   = 소환사 셀                                        (주변에 설 칸이 없을 때만)
```

`StepDir` 도 둘을 나눠 받는다 — 구역 판정·사격 위치 수집은 **중심**, "여기 서 있으면 정지"·복귀 목적지는 **집**.

층 대조가 필요한 이유는 **순찰병의 통행 층이 저작 값**이기 때문이다. `Path` 전용으로 저작된 소환물은 배치지에 설 수 없고, 그대로 두면 «절대 설 수 없는 칸을 향해 영원히 전진»하는 계약 4의 원래 실패가 재현된다. 판정은 셀 층 ∩ 유닛 통행 층 — `traversal-layers` 의 정의식 그대로이고 새 술어를 만들지 않는다. 선정은 `BattleBridge.TryGetPatrolHomeCell` 단독 소유.

### ⚠ 담당 구역 ≤ 순찰병 사거리면 순찰병은 움직이지 않는다

박스 반경 4 · 순찰병 사거리 4 로 실측하면 **박스 안 적 31케이스 전부 정지**한다(사거리 1이면 26/31 이동). 집에 선 채로 박스 안 모든 적이 이미 사거리 안이라 다가갈 이유가 없다 — 원거리 유닛의 정상 동작이고 버그가 아니다.

**순찰병이 마중 나가는 그림을 원하면 순찰병 사거리 < 담당 구역**이어야 한다. 이 관계는 코드가 강제하지 않는다(강제하면 원거리 소환물을 못 만든다) — 저작 규칙으로 남긴다.

### 실효 교전 반경

계약 5(«실효 = 구역 + 순찰병 사거리»)는 형태 그대로 유지된다. 값만 `소환사 attackRange + 순찰병 attackRange` 로 바뀐다.

## 완료 기준

- [x] compile 에러 0 · **EditMode 2044 중 2041 통과 · 실패 0**(스킵 3은 기존 `[Ignore]`) · **PlayMode `PatrolDefenderPlayTest` 2/2**
- [x] 신규 5건: ① 반경이 소환사 `attackRange` 에서 파생(EditMode) ② 정지 기준은 집이지 중심이 아니다 ③ 중심에 서 있으면 집으로 물러난다 ④ 집 ≠ 소환사 셀 · 스폰 위치 = 집(PlayMode) ⑤ `Path` 전용이면 통행 가능 칸으로 퇴화
- [x] `leashTileRadius` 문자열이 코드에서 0건
- [x] 라이브 실측: 배치 프리뷰 최대 체비셰프 = `attackRange` · 퇴화 분기 동작
- [x] Play(육안): 소환물이 소환사 **옆** 칸에 스폰된다
- [x] 소환사 **재배치** 시 구역·집이 따라온다 — 육안 대신 **PlayMode 테스트로 고정**했다(`TryBeginDefenderRelocation` 실경로). 중심 = 새 소환사 셀 · 집 ≠ 그 셀 · 집이 새 구역 안
- [x] 데이터 결정: **시트 정리 완료** (2026-08-10 사용자) — 소환사 `attackRange` **4** / 순찰병 **1**. 사거리 < 담당 구역이라 순찰병이 마중 나가는 구성이고, 에셋과도 일치한다(드리프트 없음)

---

**완료 기준 확인**: 2026-08-11 · 커밋 `515e5f00` · 완료 기준 전부 충족. 사용자 Play 확인(스폰 위치·이동) + 재배치 축 PlayMode 고정 + 시트 저작 정리.
