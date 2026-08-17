# 8 — 미사일 1발 = 적 1기 (임자 있는 낙하탄)

## 목적

unit 1 의 fan-out 은 **칸당 1발**로 접혀 있었다. 그래서 적 3기가 한 칸에 뭉치면 미사일이
1발만 떨어져 **발수가 적 수와 어긋났다**. 원 지시는 「N타일 이내의 **모든 적**을 1:1 타격하는
미사일이 발사되고 하늘이 떨어지는 느낌」이고, 그 느낌의 핵심이 발수다.

이 unit 은 접기를 없애고 **적당 1발**로 되돌린다 — 아처·레인저 투사체와 같은 관념
(1발 = 1대상).

## 왜 접었었나, 그리고 그 원인을 어디서 없앴나

접은 이유는 과피해였다. `SkyFall` 은 `ResolveProjectileAxes` 에서 `TileAoe` 와 1:1 로
묶여 있어(`BattleBridge.cs:5539`) `impactTileRange 0` 이어도 **그 칸 «범위»** 를 때린다.
즉 착탄은 «누구를 겨눴는지» 를 모른다. 같은 칸에 2발을 떨어뜨리면 두 적이 서로의 폭발에
함께 맞아 각자 160 을 받았다(실측).

**원인이 발사 쪽이 아니라 착탄 쪽에 있었다.** 그래서 이번엔 접는 대신 착탄이 겨냥을 알게
한다 — 각 발이 `target`(임자)을 싣고, `TileAoe` 팔은 target 이 지정된 탄이면 **그 적만**
후보로 삼는다. 결과:

| | 접기(unit 1) | 임자 게이트(unit 8) |
|---|---|---|
| 발수 | 칸 수 | **적 수** |
| 적당 피해 | 저작값 | 저작값 (동일) |
| 칸 벗어난 적 | 회피 | **회피** (동일 — tile 판정 그대로) |
| 겨냥 안 된 적이 그 칸에 걸어들어옴 | 맞는다 | 안 맞는다(자기 미사일이 따로 있다) |

기각한 대안: **`SingleSplash` 로 바꾸기** — target 에 무조건 적용되므로 낙하 2초 동안 적이
걸어나가면 «빈 땅에 떨어졌는데 맞는다». **`SkyFallOnEntity` 추적 신설** — 어긋남은 없지만
회피가 원리적으로 사라져 캐논이 세지고, 이동 수학·분류·emitter Entity 경로 시차를 다 신설해야
한다. 임자 게이트는 **오늘의 밸런스를 그대로 두고** 발수만 고친다.

## 무회귀 근거 (감사 2026-08-16)

게이트가 `target != Entity.Null` 로 켜지므로, 기존 `TileAoe` 발사가 target 을 싣고 있으면
그 광역이 조용히 단일 대상으로 쪼그라든다. 전수 확인했고 **전부 비어 있다**:

- `AttackSystem.cs:294`(SkyFall 착지) · `:1212`(ballistic 평타, `projRef.payload`) — target 없음
- `HealthThresholdSystem.cs:272`(진동갑주 `SelfTileAoe`) — target 없음
- `BuildPatternTemplate`(보스 barrage·패턴 전부) — 「타겟 의존 필드(target/impact/swingIndex)는
  비운 채 남긴다」가 명문화돼 있음
- `BattleBridge` 의 `target = e` 2곳은 `EnemyCcEvent`·`DotApplyEvent` 로 투사체 요청이 아님

⚠ **TileAoe 요청에 target 을 싣는 새 발사처를 만들 때는** 「광역이 단일 대상이 된다」가
의도인지 먼저 확인할 것. 코드에도 같은 경고를 남겼다.

## 변경 대상

- `Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — TileAoe 팔 후보 루프에 임자 게이트
- `Scripts/Battle/Combat/Projectile/Emission/ProjectileEmitterSystem.cs` — Cell fan-out 접기 제거
  + `target` 적재 + 정렬/시차 재구성 + `SubCellOffset`
- `Assets/_Project/Tests/PlayMode/OnPlaceSkyStrikeTest.cs` — 발수 단언 추가

## 구현 — 순서와 시차

후보마다 `(row-major 셀 rank, pool index)` 로 정렬한다. rank 만으로는 같은 칸이 동순위라
청크 순서가 다시 새어 들어오므로 pool index 로 안정 tie-break 한다.

시차 slot 은 **칸** 이 센다. 같은 칸의 2번째 발부터는 그 slot **안** 을 채운다
(`stagger * (1 - 1/(j+1))` — 항상 slot 미만):

```
flightTime = telegraph + cellSlot*stagger + (j==0 ? 0 : stagger*(1 - 1/(j+1)))
```

전역 순번으로 밀지 않는 이유: 뭉친 웨이브에서 폭격 길이가 적 수만큼 늘어나 앞뒤 착탄 간격이
저작값과 무관해진다. 쓸어가는 길이는 «칸 수» 가 정해야 하고 그래야 상한도 예측된다
(반경 2 = 최대 25칸 = 2.32초 @ stagger 0.08).

같은 칸의 여분 발은 황금각 서브셀 오프셋으로 비켜 떨어뜨린다 — 안 하면 정확히 겹쳐 한 발로
보여서 발수를 늘린 목적이 사라진다. 반경 `0.28 * tileSize < 0.5` 라 **착탄 칸이 바뀌지
않는다**: 바뀌면 그 발의 임자가 tile 판정에서 탈락해 헛방이 된다.

## 완료 기준

- [x] compile 0 error (2026-08-16)
- [x] PlayMode `OnPlaceSkyStrikeTest` **5/5 green** (2026-08-16, 18.6s)
  - **같은 칸 2기 → 각자 정확히 저작 피해**(160 아님) — unit 1 리뷰가 잡은 과피해 회귀 핀,
    절대 약화 금지
  - **같은 칸 2기 → 스폰된 투사체 2발**(1발 아님) — 이 unit 의 핵심 단언
  - 반경 밖 적 무피해 · 비행 적 무피해(층 게이트) · 시차 무회귀
- [x] EditMode 기존 투사체 핀 **63/63 green** (2026-08-16, 2.1s) — `ProjectileSystemTests` ·
      `ProjectileEmitterIntegrationTests` · `ProjectileRetargetAndBounceTests` ·
      `PathHitRehitCooldownTests` · `PatternScopeTests` · `PatternBakeTests`.
      그중 `TileAoe_Payload_Damages_Every_Enemy_In_Impact_Range` 가 임자 게이트의 무회귀
      증거다 — target 없는 기존 광역은 그대로 **칸 전원**을 때린다.

### ⚠ 이 테스트는 «판 위 남의 공격원» 을 같이 재고 있었다 (2026-08-16)

처음 돌렸을 때 5개가 전부 빨갰다. **제품 결함이 아니었다** — 제품 코드 3개를 직전 커밋으로
되돌려도 같은 5개가 같은 값(40·100·20)으로 실패했다. 통제 실험으로 가른 pre-existing 부패다.

깨진 전제는 「판 위 적은 내 더미뿐 · 투사체는 내 미사일뿐」이었다:

- **본능 구조물이 더미를 쏜다**(`Structures spawned: 5`) → +20 이 얹혀 80 이 100 으로,
  스코프 **밖** 더미가 20~40 을 받아 「반경 게이트가 죽었다」로 오진. `MakeCannon` 이
  캐논 **자기** 평타는 `attackRange 0` 으로 막아 뒀지만 **남의** 공격원은 아무도 안 막았다.
- **웨이브 적이 스코프에 들어와 자기 미사일을 받는다**(`Wave 1 queued (6 spawns)`) →
  더미 2기에 3발이 세어져 「여분이 샌다」로 오진.

고친 방식: 배치 전 AttackState 전량 제거(그 시점엔 구조물뿐) + 발수는 **임자가 우리 더미인
탄만** 센다. 교훈 — **배틀 씬을 띄우는 PlayMode 핀에서 «절대 피해량» 을 재면 안 된다.**
콘텐츠(구조물·기믹·웨이브)가 붙을 때마다 조용히 의미가 바뀌고, 어느 날 남의 변경 때문에
빨개져서 «내 코드가 깼나» 를 먼저 의심하게 만든다.
- [x] Play 육안: 적이 뭉친 칸에 미사일이 **겹치지 않고 적 수만큼** 떨어진다 (사용자 확인 2026-08-16)
- [ ] Play 육안: 착탄 additive 이펙트가 과하지 않은지 — 발수가 늘면 그만큼 늘어난다

### 실측 스냅샷 (라이브 배틀, 2026-08-16)

배치된 캐논 엔티티: `trig[1] OnPlace->EmitProjectilePattern(patIdx=0)` · `PatternSlot=1`.
bake 체인이 정상이라는 확인이다.

⚠ 스냅샷을 배치 **직후가 아닌 시점**에 찍으면 `Emitter=0 · 캐리어=0 · 투사체=0` 으로 보인다 —
OnPlace 버스트는 한 번에 끝나고 캐리어는 같은 프레임에 드레인되기 때문이다. 이 값들로
「안 쏜다」를 판정하지 말 것. 발사 여부는 **배치 프레임**에서만 관측된다.
