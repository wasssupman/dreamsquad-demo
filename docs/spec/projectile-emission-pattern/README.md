# projectile-emission-pattern — 발사 명세(패턴) 시스템 + 베지어 호밍 미사일

> 상태: **완료 2026-07-28** (units 0~6. 투트랙 리뷰 양측 APPROVE — CRITICAL 4·HIGH 1·MEDIUM 다수 수정. EditMode 1514/1512 pass·0 fail. Play e2e 실측 통과 — 폭격 낙하 위치·미사일 곡선/데미지/랜덤 타겟·인스턴스 무누적·텔레포트 미발생). **남은 것 = 체감 튜닝**(미사일 damage 40·주기 0.5s·`bezierLateral` 1.2)과 `shotCount` 3 육안·무회귀 스모크. 인계: `7_handoff_summary.md`
> 사용자 결정 3건 반영: v1 = 신규 패턴 + 융단폭격 흡수 / 텔레포트는 보스 SO 에서만 제거(코드 보존) / 랜덤 = 결정론 해시 셔플.
>
> 선행: `docs/spec/projectile-trajectory-payload/`(궤적×페이로드 2축 + 단일 라이프사이클), `docs/spec/defender-directional-volley/`(count/interval/spread 순수함수 — `VolleyMath`), `docs/spec/nightmare-catcher/`(보스 트리거 프레임워크 · 융단폭격 · 텔레포트).

## 상위 목표

투사체 스택에서 비어 있던 축인 **발사(emission)** 를 데이터로 만든다. 한 발의 명세(`ProjectileData`)는 이미 SO 인데, "누구를 겨냥해 · 몇 발 · 어떤 간격으로 쏘는가"는 코드에 흩어져 있다(`new ProjectileSpawnRequest` 조립 지점 11곳, 타겟 선택은 `NearestTargeting`/`BarrageEpicenter` 등으로 분산). 그래서 새 사격 스킬마다 arm 을 손으로 짠다.

이 spec 은 발사 명세 SO + 그것을 tick 하는 emitter 하나를 세우고, **드림캐쳐는 "패턴을 트리거"만** 하게 만든다. 확장 seam 은 **3차 베지어 호밍 궤적**으로 실증한다 — 현 `MovementKind` 는 `HomingToEntity`(직진·추적)와 `BallisticArcToPoint`(곡선·셀고정)가 배타적이라 "곡선으로 날면서 추적"을 표현할 수 없다.

콘텐츠 산출물 = 보스 나이트매어의 **텔레포트 제거 + 곡선 미사일 사격**(0.5초 간격, 맵 전체 방어유닛 중 랜덤 1기).

## 검증 질문

> 새 사격 스킬이 **C# 코드 0줄**로(패턴 SO + 탄 SO authoring 만으로) 만들어지는가? 융단폭격이 전용 arm 없이 같은 emitter 로 **값 보존**되어 도는가? 곡선 호밍 궤적이 **arm 하나**(위치 순수함수 + Move arm + view Y arm)로 붙는가? 발사 결정 로직이 `Unity.Entities` **무참조**로 유지되는가?

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터+로직 | `0_pattern_definition_logic.md` | 정의 계층(`ProjectilePatternData`·`PatternSpec`) + 로직 계층(`EmitterTick`·`PatternTargeting`·`ShotOrder`) + EditMode. **아키텍처 코드 0줄** |
| 1 | 궤적 arm | `1_bezier_homing_trajectory.md` | `Bezier3` 순수함수 + `MovementKind.BezierHomingToEntity` + view 공간 Y + EditMode ← 확장 seam 실증 |
| 2 | 아키텍처 | `2_emitter_system.md` | `EmitterInstance` 버퍼 + `ProjectileEmitterSystem`(tick → order → SpawnRequest) |
| 3 | 트리거 seam | `3_dreamcatcher_trigger_seam.md` | `DcPayloadKind.EmitProjectilePattern`(append) + bake + arm push |
| 4 | 이관 | `4_barrage_migration.md` | 융단폭격 → 패턴 재표현. 기존 `AreaBarrage` arm·`BarrageEpicenter` 흡수 |
| 5 | authoring | `5_missile_authoring.md` | 미사일 탄/패턴 SO + 보스 SO 텔레포트 mechanic 삭제 |
| 6 | 검증 | `6_play_validation.md` | Play e2e + 무회귀 + 파이프라인 커버리지 확정 |

> 순서 근거: 0(순수) → 1(궤적, 0과 독립) → 2(0을 소비) → 3(2를 소비) → 4·5(authoring) → 6. 1 은 0·2 와 병행 가능하지만 5 의 선행이다.

## Feature-wide 계약 (load-bearing)

1. **3층 분리** (CLAUDE.md 제약 10). ① 정의 계층 `Wassup.Data` — `UnityEngine` 만 참조, `Entities`/`Battle` 타입 0(`DcMechanic.cs` 선례). ② 로직 계층 — plain 값 in/out 순수 static, 아키텍처 타입 0. ③ 아키텍처 계층 — 후보 수집·엔티티 생성·컴포넌트 쓰기. **아키텍처 스왑은 ③만 다시 쓴다.**
   - 기계 검증: unit 0 신규 파일에 `using Unity.Entities` 0건. **이 검증이 보장하는 것은 "ECS 타입 무참조"까지다** — ①②는 여전히 `Unity.Mathematics`·`Unity.Collections`(Burst 호환 컨테이너)와 `MovementKind` enum 에 의존하고, ECS 시스템과 같은 asmdef·네임스페이스에 산다. Mono 이식 시 **컨테이너 타입 교체와 파일 이동이 필요하다**(모듈 추출은 이 spec 의 스코프 밖 — `feedback_spec_as_history_not_package`). 계약이 주장하는 것은 "무수정 이식"이 아니라 **결정 로직에 아키텍처 지식이 스며들지 않는다**는 것이다.
2. **`ShotOrder` 는 `Entity` 를 모른다.** 타겟을 "후보 배열의 index" 로 가리키고, 아키텍처가 자기 배열(ECS `NativeArray<Entity>` / Mono `List<Transform>`)에서 해석한다. `ThreatTable.Leader(entries, alive)` 가 이미 쓰는 관용구 — aliveness 는 caller 가 parallel 배열로 넘기고 순수함수는 lookup 을 만지지 않는다.
3. **탄의 성질은 `ProjectileData` 가 소유하고, 패턴 SO 는 복제하지 않는다.** 새 효과(bounce·관통·CC 부여 등)를 데이터로 여는 추가 위치는 `ProjectileData` 필드 하나 + Hit/Move arm 의 inert 가드다. 패턴 SO 에는 selection·count·interval 만 둔다. 같은 값이 두 곳에 생기면 우선순위 판단이 매번 필요해진다.
4. **트리거 = 사건, 패턴 = 그 한 번의 전개.** 반복 주기는 트리거 소유다(`PeriodicTimer(0.5s) × 패턴(1발)`). 패턴의 `shotCount`/`shotIntervalSec` 는 **한 번의 발사 안의 연발**을 뜻한다. 두 표현이 겹치지 않게 이 경계를 지킨다.
5. **신규 라이프사이클 0.** emitter 는 기존 `ProjectileSpawnRequest` → 브리지 드레인 → `ProjectileState` → Move/Hit → 파괴 경로에 요청만 넣는다. 신규 시스템 1개(`ProjectileEmitterSystem`) 외에 **드레인·NativeQueue 채널·투사체 태그 신설 금지**(projectile-trajectory-payload 계약 2 상속).
6. **결정론.** 타겟 선택은 seeded RNG 아닌 index 기반이며, **안정 키(row-major 셀 rank)로 정렬한 순위에서 뽑는다** — ECS 청크 순서에 의존하면 같은 index 가 프레임마다 다른 대상을 가리킨다(`BarrageEpicenter` 가 이미 그래서 rank 를 계산한다). 셔플은 `fireCount` 해시 → rank 매핑.
7. **진영은 host 에서 도출한다.** 패턴 SO 에 faction 필드를 두지 않는다. host 가 적이면 후보 풀 = 방어유닛(`targetFaction=Defender`), 반대도 성립 — 채찍질 arm 의 `hostIsEnemy/hostIsDefender` 판정 선례.
8. **인스턴스 시작 시 `PatternSpec` 값 스냅샷.** 발사 도중 SO·버프가 바뀌어도 7번째 탄이 1번째와 달라지지 않는다(defender volley 의 template 스냅샷 보증을 구조에서 얻는다).
9. **view 공간 Y.** `BoardSpace.ToView` 는 sim-Y 를 drop 한다(평면 타일맵 보드). sim 은 XZ 곡선만 굴리고 3축의 Y 성분은 `ProjectileViewPool` 이 view 에서 더한다 — `BallisticArc`/`SkyFall` 이 이미 그 패턴이다. **sim-Y 에 높이를 실으면 화면에 안 보인다.**
10. **SO 해석은 브리지(드레인)가 유일 seam.** ISystem 은 SO 를 읽을 수 없으므로, 궤적 파라미터 중 요청에 실리지 않는 값(`dropHeight`·베지어 `lateral`/`forwardBias`)은 `BattleBridge.SpawnProjectile` 이 `dataIndex` → `ProjectileData` 로 해석해 채운다 — 기존 `SkyFall` 선례. 덕분에 발사 주체(AttackSystem·emitter·캐스트)는 SO 를 몰라도 되고, 요청 struct 가 궤적마다 비대해지지 않는다.
11. **궤적은 열린 어휘다 — 베지어는 산출물이 아니라 실증이다.** 새 이동 수학이 붙는 표준 레시피 = `MovementKind` append + 위치 순수함수 + Move arm + view-Y arm (projectile-trajectory-payload 계약 8, 현 어휘 5종이 전부 이 형태). 패턴 계층은 이동 수학을 모르고(barrel 소유, 계약 3), **emitter 는 개별 `MovementKind` 가 아니라 타겟 바인딩 클래스(entity-bound / cell-bound / direction-bound)로 분기한다** — 발사 시점에 궤적이 요구하는 것은 이 셋뿐이다. 따라서 기존 바인딩을 재사용하는 새 수학(나선 추적, 사인 스트레이프, 오비트 등)은 **emitter 변경 0**, 엔진 레시피 비용만 낸다.
12. **무회귀가 완료 기준.** defender 다연발(`VolleyFireState`)·`AttackSystem` RESOLVE 경로·기존 5 궤적 × 3 페이로드는 **무접촉**. 융단폭격은 값(150 데미지 / r3 / 텔레그래프 1.5s / round-robin)이 보존돼야 한다.

## 파이프라인 커버리지

투사체 아키타입(`docs/reference/object-pipeline-map.md` §투사체) 대조 — 정거장 신설 0, 스폰 진입점만 +1:

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` | **+`Data/ProjectilePatternData.cs`**(발사 명세). `ProjectileFlightMode` 에 `BezierHoming`·`SkyFall` append → `ResolveProjectileAxes` 매핑(SkyFall 은 지금껏 `ApplyMeteor` 하드코딩으로만 존재 — spec-review C1) |
| 스폰 진입점 | RESOLVE / 폭탄 / 캐스트 드레인 3곳 | **+4번째 = `ProjectileEmitterSystem`**. 기존 3곳과 동형(`ProjectileRequestCarrier` 캐리어 → 브리지 드레인이 스폰 후 파괴) |
| ECS 컴포넌트 (Combat) | `Projectile/` ProjectileState·Tag·SpawnRequest | +`Emission/EmitterInstance`·`Emission/PatternSlot`(둘 다 패턴 host 전용 버퍼 — 일반 유닛 무과세) · `ProjectileState` 에 베지어 제어점 2필드 |
| 시뮬 시스템 | `ProjectileMoveSystem`(궤적) · `ProjectileHitSystem`(페이로드) | Move 에 `BezierHomingToEntity` arm 1개. Hit 무변경 |
| 이벤트 큐 | `ProjectileHitEventsSingleton` | 무변경 — **신규 채널 0** |
| View/Pool | `Presentation/ProjectileViewPool.cs` | view Y switch 에 베지어 arm 1개 |
| 씬 wiring | BattleBridge `_projectileViewPool` | **무변경** — 신규 씬 배선 0 |

## 후속 후보 (스코프 밖)

> **범용 투사체 매니저까지의 거리 (spec-review 2026-07-28 판정):** v1 은 "타겟형 사격 매니저"다 — 발사 스케줄 × 타겟 선택 × 궤적/페이로드 위임까지. 범용을 자처하려면 아래 처음 4개(무타겟 / host 독립 / 서브 발사 / non-Damage)가 어휘에 들어와야 한다. 뼈대(순수 결정 계층 + ShotOrder + 기존 라이프사이클 재사용)는 그 확장을 수용하는 형태로 설계됐다.

- **무타겟 패턴 (`PatternSelectionRule.None` + 방향/셀 지정)** [M] · fan/ring·고정 방향 발사는 대상이 없다 — 지금 구조는 후보 선택이 전제라 표현 불가. `None` rule + origin 기준 방향 셋(shape 축과 함께). Directional 탄의 `maxDistance` 출처 정의도 여기.
- **host 독립 발사 (detached emitter)** [M] · 사망 유언 barrage·bridge-cast(플레이어 스킬) 패턴 발사. host 엔티티가 죽으면 `EmitterInstance` 버퍼도 죽는다 — ownerless 캐리어 엔티티에 버퍼를 실어 fire-and-forget. 첫 소비자 생길 때.
- **서브 발사 (착탄 → 자식 패턴, cluster)** [L] · 범용 매니저의 리트머스. `ProjectileData` 에 `onImpactPattern` 참조 + 착탄 지점을 origin 으로 detached 발사 — host 독립 발사가 선행 조건.
- **defender 패턴 개통 시 안정 키 필요** [S] · (리뷰 발견) `PatternTargeting` 의 tie-break 는 같은 셀에 후보가 둘 이상이면 스냅샷 index 로 갈린다. 방어유닛은 타일 고정이라 유일하지만 **적 후보는 자유 이동이라 셀 공유가 상시**다 — `hostIsDefender` 경로를 여는 순간 리플레이 결정론이 깨진다. 후보 배열과 나란히 안정 키(entity index 등)를 넘겨 2차 tie-break 로 쓰면 된다. 현재는 bake 가 `hostIsEnemy: true` 고정이라 미도달이며, 한계는 `DuplicateCells_SelectionIsCellStable_ButNotEntityStable` 테스트가 문서화한다.
- **베지어 궤적의 재조준 개통** [M] · (리뷰 발견) 지금은 봉인돼 있다. 베지어는 `t = elapsed/flightTime` 로 진행하므로 t≈1 에서 재조준하면 새 타겟으로 **순간이동 후 즉시 착탄**한다(`HomingToEntity` 는 `speed*dt` 라 무해). 열려면 재조준 시점에 현재 위치를 새 `origin` 으로 삼아 제어점을 재산출해야 하는데, 그 파라미터(`bezierLateral`/`ForwardBias`)가 SO 에 있어 ISystem 이 읽지 못한다 → `ProjectileState` 에 두 값을 싣거나 재조준을 드레인으로 우회하는 설계가 선행이다.
- **미지원 payload kind 의 무경고 통과(카드 bake)** [S] · (리뷰 발견) 카드 bake 의 payload `if/else if` 체인에 terminal `else` 가 없어, 배선되지 않은 kind 가 조용히 슬롯으로 붙고 "부착됨"으로 집계된다(설명 텍스트는 공란). 이번엔 `EmitProjectilePattern` 만 loud 거절로 막았다 — 일반 해법은 체인 끝에 미지원 kind 거절을 두는 것이며, 기존 kind 전수 점검이 필요해 별건으로 분리한다.
- **패턴 탄의 재조준/바운스 opt-in** [S] · (구현 후 리뷰 발견 2026-07-28) `retargetTileRange`·`bounce*` 는 `ProjectileSpawnRequest`/`ProjectileState` 필드일 뿐 `ProjectileData` 에 없다 — 지금은 `AttackSystem`·카드 경로가 코드로 채우는 값이라, **패턴 발사 투사체는 이 성질을 데이터로 켤 수 없다**(미사일은 대상이 먼저 죽으면 그대로 소멸). 계약 3 을 지키려면 `ProjectileData` 에 필드를 얹고 `BuildPatternTemplate` 이 복사한다. 미사일 체감에서 "죽은 표적에 쏜 탄이 낭비된다"가 걸리면 착수.
- **non-Damage 패턴 (Stat/Stack/Heal outputs)** [S] · emitter 캐리어는 outputs 버퍼를 싣지 않아 `state.damage` 폴백(Damage-only) 고정. 슬로우탄/도트탄 패턴이 필요하면 template 조립에 outputs 스테이징 추가.
- **`PayloadKind` → 해결 시점 / 효과 분리** [M] · 현 `PayloadKind` 는 두 개념을 겹쳐 든다: 해결 **시점**(점 도달 / 비행 중 경로 스윕 / 착탄 셀)과 **효과**(splash / pierce / tileAoe). bounce·retarget·priority·heavy 는 이미 `0=inert` 직교 필드인데 이 셋만 enum 에 묶여 있다. 승격하면 "곡선 호밍 + 관통 + 착탄 splash" 가 데이터로 열린다. 계약 3 이 가리키는 다음 단계 — 소비자는 그런 조합을 요구하는 첫 콘텐츠. `ProjectileHitSystem` 해결 분기 재편이라 기존 발사 지점 11곳이 회귀 표면이 되므로 별도 spec.
- **fan/ring shape** [S] · `VolleyMath.SpreadDirection` 재사용 → 패턴 필드 1개 + 호출 1줄. Directional 탄과 페어링될 때 의미가 생긴다.
- **selection rule 확장** [S] · Nearest / LowestHp / Frontmost — 기존 순수함수(`NearestTargeting`·`LowestHealthTargeting`·`FrontmostTargeting`) 를 rule 로 노출. 소비자 생길 때.
- **사거리 내 범위(scope)** [S] · v1 은 맵 전체 고정. host 사거리 기준 후보 제한이 필요한 콘텐츠가 생기면 필드 1개.
- **defender 다연발 emitter 수렴** [M] · `VolleyFireState` → `EmitterInstance`. `AttackSystem` RESOLVE 경로를 건드리므로 방어유닛 전체 공격이 회귀 표면(2026-07-28 사용자 판단으로 v1 제외).
- **`SelfBlink` 재사용** · 텔레포트 코드(`BlinkMath`·`BlinkApplySystem`·`BlinkRequestEventsSingleton`·테스트 7건)는 inert 로 남는다. 다른 적/보스가 순간이동을 요구하면 SO mechanic 1건으로 부활한다.
- **미사일 데미지 실측** · 초안 40(0.5초 간격 = 초당 2발 · 사거리 무제한). SO 값이라 Play 중 조정.
