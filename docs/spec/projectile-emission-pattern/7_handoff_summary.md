# 7 — Handoff Summary (units 0~5 구현 완료 2026-07-28 · Play e2e 대기)

## Commit

spec `af1796bc`(0~6 작성) → `5e69dc5f`(spec-review 반영) → `afb9d83f`(궤적 열린 어휘 계약).
구현: `f787c607`(u0 정의+로직) `9e6d97fa`(u1 베지어 궤적) `e2ed719d`(u2 emitter)
`8a1fc0e3`(u3 트리거 seam) `de43db5a`(u4 융단폭격 이관) `6c963fbb`(u5 미사일 authoring).

## Implemented

- **발사 명세가 데이터가 됐다**: `ProjectilePatternData`(barrel × damage × selection × count/interval × telegraph) + `PatternSpec`(unmanaged 미러). 탄의 성질은 `ProjectileData` 소유, 패턴은 복제하지 않는다.
- **3층 분리**: 정의(`Wassup.Data`) / 로직(순수 static, `Unity.Entities` 무참조) / 아키텍처(emitter). `ShotOrder` 는 `Entity` 대신 후보 index 로 타겟을 가리킨다. 계약이 보장하는 것은 **결정 로직에 아키텍처 지식이 스며들지 않는다**는 것이지 무수정 이식이 아니다(`Unity.Mathematics`/`Collections` 의존·asmdef 동거는 남는다 — README 계약 1 참조).
- **베지어 호밍 궤적**: `MovementKind.BezierHomingToEntity` — 곡선×추적 조합 개통. 제어점 결정론 좌우 교대(`shotCount` 올리면 살포로 갈라짐), 종점만 타겟 추적, sim XZ / view Y 분담.
- **emitter 는 바인딩 클래스로 분기**: `MovementBinding.Of` → Entity/Cell/Direction 3분류. 기존 바인딩으로 분류되는 새 이동 수학은 emitter 무변경으로 발사된다.
- **트리거 seam**: `DcPayloadKind.EmitProjectilePattern`(17) — arm 이 하는 일은 값 복사 push 뿐. 발사 도중 SO/버프가 바뀌어도 시작된 버스트는 불변.
- **융단폭격 이관**: 전용 arm·`BarrageEpicenter` 제거, 같은 emitter 로 값 보존(150/r3/1.5s/RoundRobin).
- **콘텐츠**: 보스 텔레포트 제거 + 0.5초 간격 랜덤 방어유닛 곡선 미사일(damage 40 초안). **unit 5 는 코드 diff 0줄** — asset 3개 + 보스 SO 편집만.

## Key Files

- 정의: `Data/ProjectilePatternData.cs` · `Data/PatternSpec.cs` · `Data/Dreamcatcher/DcMechanic.cs`(payload 17 + `pattern` 필드)
- 로직: `Battle/Combat/Projectile/Emission/` {`EmitterRuntime`·`EmitterTick`·`ShotOrder`(+`PatternLogic`)·`PatternTargeting`·`MovementBinding`} · `Projectile/Bezier3.cs`
- 아키텍처: `Emission/ProjectileEmitterSystem.cs` · `Emission/EmitterInstance.cs` · `Emission/PatternSlot.cs`
- 편입 지점: `BossPeriodicTriggerSystem.cs`(push arm) · `BattleBridge.cs`(`BuildPatternTemplate`·bake·드레인 베지어 제어점) · `ProjectileMoveSystem.cs`(arm) · `ProjectileViewPool.cs`(view Y) · `Core/Dreamcatcher/DcApplicability.cs`
- 에셋: `Projectile_NightmareBarrage` · `Pattern_NightmareBarrage` · `Projectile_NightmareMissile` · `Pattern_NightmareMissile` · `Enemy_Boss_Nightmare`

## Verified

- **EditMode 1502 / 1500 passed / 0 failed / 2 skipped**(기존 무관 skip). 신규 30건: 스케줄 9 · 선택 규칙 8 · MovementBinding 분류 핀 1 · 베지어 9 · BuildOrder 등.
- 컴파일 클린(배치 로그 `error CS` 0건), 신규 asset 로드 에러 0.
- 검증 수단: **에디터가 Play Mode 라 MCP `run_tests` 가 거부됨** → `wassup-testrig` worktree 배치 실행으로 우회(에디터·포커스 무관). 세션 중 MCP 브리지가 끊겨 이후 검증도 배치로 진행.
- `BarrageEpicenter` 흡수 동일성은 `f787c607` 시점에 대조 테스트로 검증 후 원본 삭제(라이브 참조 0 확인).

## Notes (되돌리면 안 되는 의도)

- **`PatternSlot.fireCountBase` 는 영속 카운터다.** `EmitterInstance` 는 발화마다 생성·제거되는 transient 라 카운터를 0 에서 시작하면 RoundRobin 은 영원히 같은 rank(같은 방어유닛만 폭격), 셔플은 `hash(0)` 고정(같은 대상만 저격)이 된다.
- **`DcTriggerSlot.patternIndex` 는 bake 가 -1 로 명시 초기화**한다. struct default 0 은 유효 index 라 미배선 슬롯이 0번 패턴을 쏜다.
- **패턴 버퍼는 `slots` 획득 전에 부착한다.** `AddBuffer` 는 구조 변경이라 이미 잡은 `DynamicBuffer` 핸들을 무효화한다 — 루프 안에서 붙이면 `slots` 가 죽는다.
- **잠금은 `Entity` 로 저장**(index 아님). 후보 스냅샷은 프레임-로컬이라 index 를 잠그면 프레임 넘는 버스트에서 다른 유닛을 가리킨다.
- **베지어 제어점은 드레인이 산출**한다(SO 파라미터 필요, ISystem 은 SO 를 못 읽는다). 요청은 `swingIndex` 만 싣는다 — `SkyFall.dropHeight` 보충과 같은 seam.
- **폭격 barrel 은 Meteor 사본**이다. 공유 `Projectile_Meteor` 의 `flightMode 0`/`impactTileRange 0` 은 `ApplyMeteor` 가 축을 하드코딩해 방치된 값이며, 거기에 의미를 부여하면 플레이어 스킬과 소유가 얽힌다.
- **새 `DcPayloadKind` 는 `DcApplicability` 에도 등록**해야 한다(미등록 = `Unclassified` fail-closed + 전수 테스트 실패). 이번에 실제로 걸렸다.
- **빈 풀 발화 위상 전진은 의도된 semantics 차이**: 기존 arm 은 no-fire 시 `fireCount` 불변이었지만 새 구조는 push 시 선증가다. 관측자 없고 순회 공정성 무영향이며, 완주 시 write-back 은 겹침 버스트에서 시드 충돌한다.

## 투트랙 리뷰 (2026-07-28, `ff36c497` 로 반영)

두 리뷰어가 **독립적으로 같은 CRITICAL 을 잡았다.** 전부 런타임에서만 드러나는 종류라 EditMode 1502건이 통과했다 — 이 feature 의 가장 큰 교훈이다.

- **CRITICAL 발-루프 소실**: "쿼리 생성을 발-루프 밖으로" 리팩터하며 `for` 를 `if` 로 바꿔 루프 자체를 없앴다. `Advance` 가 N 을 반환하고 `burstRemaining` 을 N 차감하는데 캐리어는 1개 → `shotCount` 사문화. **순수 계층 테스트(`ZeroInterval_DumpsEntireBurstInOneFrame` = 5 반환)는 초록인데 실제로는 1발**이 나갔다 — 순수 테스트가 통합 결함을 가려준 사례.
- **CRITICAL `continue` 우회**: 위 결함의 귀결로 세 `continue` 가 인스턴스 루프를 건너뛰어 소비된 runtime 이 폐기되고 인스턴스가 영구 적재됐다. 방어유닛 0 구간에 쌓였다가 재배치 순간 일제 발사.
- **CRITICAL `patternSlots` dangling**: `slots` 를 지키려 순서를 바꿨는데 정작 그 핸들이 `AddBuffer` 2회 뒤에 쓰였다. 사용 직전 `GetBuffer` 재획득으로 수정.
- **HIGH 카드 경로 조용한 no-op**: `EmitProjectilePattern` 이 카드 bake 에 분기도 terminal else 도 없어 `patternIndex=0` 으로 붙고 "부착됨" 집계. loud 거절 추가.
- **MEDIUM 4**: SkyFall barrel 의 `arcHeight` 기본값 2가 `dropHeight` 침묵 오버라이드(template 이 SkyFall 일 때 안 싣도록) · `telegraphSec 0` 무예고 폭격 bake 경고 · 베지어 재조준 봉인 · `damage [Min]`/툴팁/죽은 코드.

리뷰 전 자체 검증으로 잡은 CRITICAL 1건(SkyFall origin → 폭격이 보스 머리 위에서 낙하, `b8ef7c37`)은 별건이다.

## 테스트 배치 (리뷰 후 보강)

- **순수 계층** — `EmitterTickTests`(9) · `PatternTargetingTests`(9) · `Bezier3Tests`(9). World 없이 돈다.
- **emitter 통합** — `ProjectileEmitterIntegrationTests`(6). `SimulationSystemGroup` 에 **arm(BossPeriodicTriggerSystem) + emitter 를 함께** 넣어 실제 트리거 발화로 구동한다 — 테스트가 push 를 직접 하면 시드 규약이 두 곳에 복제돼 arm 이 규약을 바꿔도 초록으로 남는다.
- **bake 경로** — `PatternBakeTests`(4). `BattleBridge` 는 `[ExecuteAlways]` 가 없어 EditMode `AddComponent` 로 Awake 가 돌지 않고, bake 가 요구하는 상태는 `_world`/`_em` 둘뿐이라 reflection 주입으로 충분하다(`BattleBridgeDraftMapTests` 와 같은 레시피). **bake 를 순수 함수로 추출해 테스트하지 말 것** — dangling 버퍼 핸들 류는 `EntityManager` 를 만지는 쪽에만 있어서, 추출하면 한 번도 깨진 적 없는 절반에 초록불이 켜진다.
- **픽스처 함정**: `TickTrigger` 는 주기만큼 dt 를 한 번에 주므로 **버스트 tick 에도 그 dt 가 적용된다**(lag spike 사양). `interval > 0` 패턴은 한 프레임에 전량이 나가므로, 버스트 진행을 관찰하려면 `FireOnceThenDetach`(작은 dt 누적 + 슬롯 분리)를 쓴다.

## Play e2e (2026-07-28 실측)

진입: `TestModeContext.Set(WavePlan_BossTest, preset)` → `SceneTransition.Go(Battle)` → `bridge.StartBattle()`. **페이즈가 Placement 에 머물면 웨이브가 돌지 않는다** — `StartBattle` 을 직접 불러야 한다. 방어유닛 후보가 필요해 `DefenderUnitTag`+`LocalTransform`+`Health`+`FactionTag`+`IncomingDamage` 를 붙인 **더미 엔티티**를 walkable 셀에 세웠다(`TryBeginDefenderDeployment` 는 Placement 게이트·pool 제약으로 거절된다).

| 항목 | 실측 |
|---|---|
| **폭격 낙하 위치** (`b8ef7c37`) | `sky@3.0,3.0 impact=3.0,3.0` · `sky@13.0,1.0 impact=13.0,1.0` — 위치 = 착탄 셀 = 타겟 셀. **수정 전이라면 시전자 위치로 갈렸다** |
| 미사일 데미지 | `dmg40` (패턴 spec 값) |
| 곡선 궤적 | `o=0.0,6.0 c1=5.0,5.4` / `c1=3.0,5.6` — 제어점이 직선을 벗어나고 **발마다 다르다**(swingIndex). 스크린샷에서 트레일이 휘는 것 육안 확인 |
| 랜덤 타겟 | `distinctTargets=2`, 더미 6기 전부 누적 피해 5만~9만 |
| 인스턴스 누적 | `inst0` 전 구간 유지 (CRITICAL 2 수정 검증) |
| 텔레포트 미발생 | 보스 2기가 HP 60%·20% 까지 내려가 경계 70/40/10% 를 여러 번 통과했는데 위치 `@0.0,6.0` 고정 |
| bake | `slots3 pats2` · 레지스트리 `[0]Barrage+pfb [1]Missile+pfb` |
| 투사체 라이프사이클 | 보스 2기 기준 `bez4 / views4 / arrived0 / badTarget0 / flightTime 0.50~1.86s` — 좌초·유령 없음 |

**관측 함정 2건**: ① MCP 왕복 지연(15~30s) 때문에 단발 프로브는 창을 놓친다 — `EditorApplication.update` 콜백 + `SessionState` 축적, 스크린샷은 **조건 기반**(미사일 2발 이상 비행 프레임만)으로 잡아야 한다. ② 한때 `bez60` 이 관측됐는데 누수가 아니라 **보스 다수 누적**(더미가 999999 HP 라 죽지 않아 매치가 끝나지 않고 보스가 쌓임)이었다 — 프로브에 보스 수를 함께 찍어야 오진을 피한다.

## Follow-up

- **`shotCount` 3 실환경 육안 미실시** — 통합 테스트(`Burst_ProducesOneCarrierPerShot_ThenRetires`)가 캐리어 3개를 고정하므로 회귀는 덮이지만, "세 발이 각각 다른 곡선으로 갈라지는" 시각은 확인하지 않았다. 패턴 SO 의 `shotCount` 를 3 으로 올려 Play 하면 바로 보인다.
- **무회귀 수동 스모크 미실시** — 홈잉/머신건/폭탄/곡사/플레이어 Meteor 각 1회. EditMode 1514 는 통과했고 축 매핑은 append-only 라 위험은 낮다.
- **신규 `.cs` 3개의 `.meta` 미생성**(브리지 끊김 시점에 커밋): `EmitterInstance`·`PatternSlot`·`ProjectileEmitterSystem`. 브리지 복구 후 Unity 가 생성한 meta 를 별도 커밋. 씬/asset 참조가 없어 GUID 재생성 위험은 없다.
- **미사일 damage 40 · 주기 0.5s · `bezierLateral` 1.2** 는 체감 튜닝 대상(전부 SO).
- README 후속 후보의 범용성 갭 4개(무타겟 / host 독립 / 서브 발사 / non-Damage)는 `docs/spec/README.md` Follow-up Backlog 등록 대기.
