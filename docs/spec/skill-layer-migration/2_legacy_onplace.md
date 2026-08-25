# 2 — 레거시 `OnPlaceEffectType` 9종

## 목적

**두 번째 어휘를 죽인다.** `BattleBridge.cs:5393~5590` 의 200줄 if/else 체인과
`OnPlaceEffectType` enum 11종, `DefenderUnitData` 의 flat 필드 7개가 여기서 사라진다.

`on-place-skill-rework` 계약 2 가 예약해 둔 「레거시 전량 이관」이 바로 이 작업이다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs:5393~5590` — 체인 철거
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — enum + flat 필드
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — 문안 case 10개
- 에셋 12개 재저작

## 구현

1. ~~**그물이 절대 선행이다.** PlayMode 커버리지가 **9종 중 3종**뿐이다.~~
   **stale — 2026-08-26 실측으로 정정.** 그 사이에 그물이 붙었다:

   | 값 | 종류 | PlayMode 그물 | 라이브 저작 |
   |---|---|---|---|
   | 1 | `SlowPulse` | **없음** | **0개** |
   | 2 | `BoostNearbyDefenders` | `OnPlaceBoostNearbyTest` | 1 |
   | 3 | `BindNearby` | `OnPlaceBindNearbyTest` | 1 |
   | 4 | `MeleeBurst` | `OnPlaceRuleTriggerTest`(간접) | 1 |
   | 5 | `ForwardProjectile` | `OnPlaceForwardProjectileTest` | **4** |
   | 6 | `GainCost` | `OnPlaceGainCostTest` | 1 |
   | 7 | `ReduceSkillCooldown` | `OnPlaceReduceSkillCooldownTest` | 1 |
   | 8 | `ApplyStackNearby` | `OnPlaceApplyStackNearbyTest` | 1 |
   | 9 | `StunNearby` | `OnPlaceStunNearbyTest` | 1 |
   | 10 | `DotNearby` | `OnPlaceDotNearbyTest` | 1 |

   **선행 작업이 「그물 6개 신설」에서 「`MeleeBurst` 의 직접 그물 1개」로 줄었다.**
   그리고 **`SlowPulse` 는 이전 대상이 아니라 삭제 대상**이다 — 그물도 없고 라이브
   저작자도 0이라 옮길 것 자체가 없다(옮기면 아무도 안 쓰는 concrete 가 생긴다, 제약 8).
2. ~~**신규로 필요한 payload kind 는 `AreaCc{DcCcKind}` 하나 정도**다.~~
   **stale — 2026-08-26 체인 전수 대조로 정정.** 하나가 아니라 셋이고, 하나는 성격이 다르다.

   | 값 | 유닛 | 오늘 하는 일 | 목표 | 신규 어휘 |
   |---|---|---|---|---|
   | 1 `SlowPulse` | — | (3과 **같은 분기**) | **삭제** | — |
   | 4 `MeleeBurst` | bruiser | 적에 즉발 피해 | `SelfTileAoe` | 없음 ✅ |
   | 5 `ForwardProjectile` | 4기 | **통로 스윕 피해** | `EmitProjectilePattern` | 없음, 단 ⚠아래 |
   | 6 `GainCost` | scout | 코스트 획득 | MetaIntent(이미 있음) | payload 1 |
   | 7 `ReduceSkillCooldown` | ranger | 쿨다운 감소 | MetaIntent(이미 있음) | payload 1 |
   | 2 `BoostNearbyDefenders` | guardian | **아군**에 DamageMul(TTL) | 스탯 오라 | ↓ |
   | 3 `BindNearby` | archer | **적**에 MoveSpeedMul(TTL) | 스탯 오라 | ↓ |
   | 8 `ApplyStackNearby` | slasher | 적에 스택 도포 | 광역 스택 | payload 1 |
   | 9 `StunNearby` | malphite | 적에 Stun + 피해 + **넉업 연출** | 광역 CC | payload 1 |
   | 10 `DotNearby` | busters | 적에 DoT + **빔 연출** + 쿨다운 밀기 | 광역 DoT | payload 1 |

   **오라 둘은 기존 concrete 를 일반화하면 공짜다.** `AllySpeedAuraSkill`(Id 2)이 이미
   `ApplyStatModifier` 를 `Selector = MoveSpeedMul` 로 방출한다 — **selector 축이 이미 있고
   concrete 가 그것과 「아군」을 하드코딩했을 뿐**이다. 스탯 축과 대상 진영을 params 로
   올리면 한 concrete 가 셋을 덮는다(보스 채찍 + 가디언 + 궁수).

## ⚠ 결정이 필요한 둘 (2026-08-26)

**① `ForwardProjectile` 은 기계적 이전이 아니다.** 오늘 이 arm 은 탄을 안 쏜다 —
`ApplyForwardOnPlaceProjectile` 이 폭 1.2타일 통로를 훑어 **즉발 피해**를 준다.
`EmitProjectilePattern` 으로 옮기면 **진짜 투사체가 날아가는 다른 메커니즘**이 되고,
라이브 4기(머신거너·마크스맨·피어서·스나이퍼)의 체감이 바뀐다. 샷건맨이 이미 그 형태다.

- (a) 패턴으로 재저작 — 스펙의 「에셋 12개 재저작」이 뜻하는 바로 그것. **밸런스 변경**이다.
- (b) 통로 스윕을 그대로 보존하는 payload 를 신설 — 어휘가 하나 늘지만 무회귀.

**② 뷰 부작용 둘을 어디에 두나.** `StunNearby` 의 넉업 홉과 `DotNearby` 의 빔은
브리지가 **직접** 재생한다(큐 안 거침). 규칙 경로로 옮기면 `PlayVisual` 의도를 거쳐야 하고,
`DotNearby` 는 추가로 **host 공격 쿨다운을 밀어** 조사 중 평타를 막는다 — 그건 연출이 아니라
규칙이라 의도 어휘에 없다(`SimIntentKind` 에 「자기 공격 잠금」이 없다).

## 작업 분할 (한 unit 이 너무 크다)

| 슬라이스 | 내용 | 신규 어휘 | 상태 |
|---|---|---|---|
| **2a** | `SlowPulse` 삭제 · `MeleeBurst` → `SelfTileAoe` | 없음 | 착수 가능 |
| **2b** | 오라 일반화 → `BoostNearbyDefenders` · `BindNearby` | payload 1 | 설계 확정됨 |
| **2c** | `GainCost` · `ReduceSkillCooldown` (ECS 무관) | payload 2 | 착수 가능 |
| **2d** | `ApplyStackNearby` | payload 1 | |
| **2e** | `StunNearby` · `DotNearby` | payload 2 + ⚠② | **결정 대기** |
| **2f** | `ForwardProjectile` | ⚠① | **결정 대기** |
| **2g** | 철거(enum · flat 필드 · 체인 · 문안) | — | 위 전부 뒤 |
3. **Mono 도메인 arm 2개를 판정대로 처리한다** — `GainCost`(`:5564`, `CostRuntime`) ·
   `ReduceSkillCooldown`(`:5571`, `skillRuntime`)은 **ECS 를 전혀 안 만진다.**
   토대 unit 0 이 「Mono 계열 intent」 또는 「예외」로 판정한 것을 따른다.
4. **`onPlacePush*` 3필드 + `ApplyOnPlacePush` 를 같이 뗀다** — **에셋 소비자 0**이 확인됐다
   (`Assets/_Project/Data` 전수 grep, nonzero 0건). 샷건맨이 마지막 소비자였고 규칙 경로로
   갈아탔다. 무비용 철거다.
5. **시트는 안전하다** — `Data/StatImport/UnitStatImportDto.cs` 에 onPlace 필드가 **0건**이라
   flat 필드 7개 삭제가 시트 왕복을 깨지 않는다.
   ⚠ 단 에셋 12개를 재저작하는 동안 **로그인 시트 임포트가 끼어들어 무관한 유닛 스탯이
   딸려 커밋될 수 있다.** 커밋 전 `git diff --stat` 로 확인한다.

## 완료 기준

- [ ] `MeleeBurst` 직접 그물 1개 신설 (나머지 8종은 이미 있다 — 위 표)
- [ ] `SlowPulse` 는 **삭제**한다(라이브 저작 0 · concrete 신설 금지)
- [ ] `OnPlaceEffectType` enum 과 `BattleBridge.cs:5393~5590` 체인이 **삭제**됐다
- [ ] `DefenderUnitData` flat 필드 7개 + `onPlacePush*` 3필드 + `ApplyOnPlacePush` 삭제
- [ ] `UnitKitSummary` 문안이 저작 SO 를 읽는다
- [ ] 에셋 12개 재저작 커밋에 **무관한 유닛 스탯 변경이 섞이지 않았다**
- [ ] Assets lane + PlayMode 배치 스킬 테스트 초록 + Play 육안(9종)
