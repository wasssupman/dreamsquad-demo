# onplace-area-cc — 말파이트 지진: 배치 스킬을 규칙으로 이관하고 피해를 얹는다

> 상태: **보류** (2026-08-19) · units 0~3 미착수 — 지시 없이 재개하지 않는다.
> **선행 조치가 이미 나갔다**: 말파이트의 피해 40 은 **레거시 arm 위에서** 먼저 얹혔다
> (사용자 결정 2026-08-19 — "말파만 먼저"). `BattleBridge` 의 `StunNearby` 분기가
> `onPlaceMagnitude` 를 소비하고 `Defender_Malphite.asset` 이 40 을 저작한다.
> 커밋 `4bfba2c2` · Play 육안 확인 2026-08-20.
> 그래서 이 spec 의 남은 값은 **피해 자체가 아니라 이관**이다 — 이관 시 그 40 은
> `SelfTileAoe` payload 의 magnitude 로 옮겨 오고, 레거시 필드 4개(`onPlaceEffect`·
> `onPlaceRange`·`onPlaceMagnitude`·`onPlaceDuration`)가 0 이 된다.
> 선행/모체: `docs/spec/on-place-skill-rework/`(트리거 × 페이로드 배관 · 계약 2 **만료 조건**),
> `docs/spec/knockup-fighter-defender/`(넉업의 심 실체 = 스턴, 공중은 뷰의 해석),
> `docs/spec/boss-jjangssen/`(SelfTileAoe 폭발 뷰 선례 — `Projectile_JjangssenQuake`),
> `docs/spec/defender-ability-assets/`(방어유닛 규칙의 집 = 능력 SO).

## 상위 목표

**말파이트 배치 스킬을 규칙 경로로 옮긴다.** 「반경 2 · 스턴 3초 + 피해 40」이라는 **내용은
이미 라이브**다(위 선행 조치) — 이 spec 이 바꾸는 것은 그 스킬이 **어느 배관 위에 서 있는가**다.

지금 그 스킬은 레거시 `OnPlaceEffectType.StunNearby` + `BattleBridge` 하드 switch 위에 있고,
피해까지 그 위에 얹혔다 — **죽기로 예정된 코드가 기능을 하나 더 갖게 된 상태**다. 옮길 값어치는
그대로 남아 있다:

- 말파이트는 `StunNearby`(9)의 **유일한 소비자**다(디펜더 27종 전수 확인 — 2026-08-19).
  이관하면 레거시 arm 하나가 **실제로 죽는다.**
- `on-place-skill-rework` README 계약 2의 만료 조건("다음 on-place 작업이 레거시 이관을 선행
  조건으로 삼는다")을 **처음으로 이행**하는 작업이다. 전량이 아니라 **소비자 1개짜리 arm 하나**를
  떼어내는 첫 절개이며, 그 과정에서 다음 이관(Bruiser·Archer)이 쓸 어휘 2개가 개통된다.

## 검증 질문

> ① **「스턴 3초 + 피해 40」이 페이로드 2개의 조합으로 표현되는가?** 두 사건이 각자 독립된
> 어휘로 남아야 다음 유닛이 하나씩 집어 쓸 수 있다 — Bruiser(피해만) = `SelfTileAoe`,
> Archer(둔화만) = `AreaCc`. 한 payload 에 겸직시키면 그 재사용이 닫힌다.
> ② **배치 순간 화면만 보고 말할 수 있는가?** — "땅이 흔들려 적들이 튀어올랐고, 아팠고,
> 한동안 굳어 있었다."

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 어휘 | `0_area_cc_payload.md` | `DcPayloadKind.AreaCc`(26) 신설 — 반경 내 적 전원에 CC. arm · 층 게이트 · 넉업 연출 |
| 1 | 어휘 | `1_selftileaoe_onplace.md` | 기존 `SelfTileAoe`(2)를 배치/주기 트리거에서도 쓸 수 있게 arm 개통 (캐리어 스테이징 헬퍼 공유) |
| 2 | 콘텐츠 | `2_malphite_quake_assets.md` | 말파이트 이관 — 능력 SO(mechanics 2) + 폭발 뷰 SO + 레거시 필드 0 + 문안·시트 |
| 3 | 철거 | `3_legacy_stun_arm_removal.md` | `StunNearby` arm 삭제(소비자 0) + PlayMode 테스트를 규칙 경로로 |
| 4 | 인계 | `4_handoff_summary.md` | 커밋·계약·실측 요약 (구현 종료 시 작성) |

> 순서 근거: 0·1 은 각자 단독으로 아무 동작도 안 한다(어휘만 연다 — `on-place-skill-rework`
> units 0·1 선례). 2 가 그 둘의 첫 소비자이고, 3 은 2 가 끝나야 소비자가 0이 된다.

## Feature-wide 계약 (load-bearing)

1. **조합은 데이터다.** 「스턴」과 「피해」는 별개 사건이므로 mechanic 2개(= 슬롯 2개)로 선언한다.
   같은 `OnPlace` 트리거를 공유하며 같은 프레임에 둘 다 발화한다. 한 payload 가 두 축을 겸직하면
   `SelfTileAoe` 와 「범위 피해」가 중복돼 **같은 일을 하는 payload 가 둘**이 된다.
2. **신규 payload 는 `AreaCc` 하나.** `on-place-skill-rework` 계약 3(기존 어휘 먼저)을 잇는다 —
   피해는 신설하지 않고 `SelfTileAoe`(2)를 재사용한다. `AreaSleep`(16)을 일반화하지 **않는** 이유는
   그쪽이 「가까운 M명 cap + 내가 때릴 대상 제외」라는 bespoke 선별기를 갖고 있어서다(마메모 자장가).
   `AreaCc` 는 **반경 안 전원**이고 cap 이 없다.
3. **띄움 길이는 유닛의 성질, CC 길이는 payload 의 성질.** 체공은 `DefenderCcData`
   (`knockupOnHitSec` · `knockupVisualHeight`)에서 읽고 `min(체공, CC 지속)` 으로 자른다 —
   지금 브리지가 하는 것과 같은 규칙이고 **저작 필드를 새로 만들지 않는다**(제약 8).
   3초 내내 떠 있으면 지진이 아니라 무중력이다(`on-place-skill-rework` unit 5 의 판단 그대로).
4. **층 게이트는 arm 이 직접 건다.** 시스템에서는 브리지 헬퍼(`CanDefenderTargetMover`)를 못 쓰므로
   `AttackState.targetTraversalLayers` × `PathFollowState.traversalLayers` 로 같은 판정을 한다
   (`AreaTaunt` arm 선례 — 빼면 근접 유닛이 하늘의 적을 스턴시킨다).
5. **후보 0이면 캐리어를 만들지 않는다.** `SelfTileAoe` 는 폭발을 `ProjectileSpawnRequest`
   캐리어로 표현하는데, 브리지 드레인은 `_running` 아래에 있다 — 전투 시작 전(배치 페이즈)에
   놓으면 요청이 **큐에 남아 나중에 터진다**(캐논 실측, `on-place-skill-rework` 후속 후보).
   반경 안 후보를 먼저 세고 0이면 스테이징 자체를 건너뛴다.
6. **enum 멤버는 지우지 않는다 — arm 만 지운다.** `OnPlaceEffectType` 은 에셋이 int 로
   직렬화하므로 중간 값을 빼면 `DotNearby`(10)가 밀려 Busters 가 다른 스킬을 쓴다.
   `StunNearby`(9)는 `SlowPulse` 처럼 **사장 표기**로 남기고 분기만 삭제한다.
7. **문안은 시트까지 고쳐야 산다.** `desc` 는 시트 임포트 축(`DefenderStatDto.desc`)이라
   에셋만 고치면 로비 진입 임포트가 되돌린다. `UnitKitSummary` 는 **규칙을 먼저** 보므로
   새 payload 2종의 절을 배선하지 않으면 설명이 **조용히 빈다**.
8. **테스트는 경로가 아니라 증상을 본다.** `OnPlaceStunNearbyTest` 의 단언(반경 2 안 3초 정지 ·
   밖 무영향 · 해제 후 재이동)은 **그대로 유지**하고 피해 40 단언만 더한다. 경로가 바뀌었는데
   단언이 초록이면 이관이 성공한 것이다.

## 파이프라인 커버리지 — 지진 폭발 뷰 (투사체 아키타입)

`docs/reference/object-pipeline-map.md` §투사체 대조. **정거장 신설 0 · 스폰 진입점 신설 0**:

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` | **+`Projectile_MalphiteQuake`** (`Projectile_JjangssenQuake` 형태 — `projectilePrefab` 없음, `hitPrefab` 만) |
| 스폰 진입점 | RESOLVE / 폭탄 / 캐스트 드레인 / emitter / **캐리어 스테이징** | **무변경** — 기존 캐리어 경로 재사용(진동갑주 선례) |
| ECS 컴포넌트 | `ProjectileSpawnRequest`·`ProjectileRequestCarrier` | **무변경** |
| 시뮬 시스템 | `ProjectileMoveSystem`·`HitSystem` | **무변경** — `flightTime 0` 즉발 TileAoe |
| 이벤트 큐 | `EnemyCcEventsSingleton`·`KnockupVisualEventsSingleton` | **무변경 — 신규 채널 0** (둘 다 기존 채널) |
| View/Pool | `ProjectileViewPool` | **무변경** — hitPrefab 원샷 |
| 씬 wiring | — | **무변경** |

CC 는 투사체가 아니라 상태라 이 표의 대상이 아니다.

## 후속 후보 (스코프 밖)

- **레거시 배치 효과 잔여 이관** [M] · 이 spec 이 `StunNearby` 하나를 뗀다. 남은 8개
  (`MeleeBurst`=Bruiser · `BindNearby`=Archer · `BoostNearbyDefenders`=Guardian ·
  `GainCost`=Scout · `ReduceSkillCooldown`=Ranger · `ApplyStackNearby`=Slasher ·
  `DotNearby`=Busters · `ForwardProjectile`=전방 관통 4종)가 남는다. Bruiser 는 unit 1 이 연
  `SelfTileAoe` 를, Archer 는 unit 0 이 연 `AreaCc{Slow}` 를 **코드 0줄로** 쓸 수 있다.
- **배치 페이즈 발동 정책** [M] · 계약 5 는 캐리어 잔류만 막는다. 「적 0마리 배치가 시도를
  소진한다」는 정책 자체는 그대로다(`on-place-skill-rework` 후속 후보).
- **지진 화면 흔들림** [S] · 카메라 킥은 `CameraDirector` 채널이 이미 있다. 이번엔 사건만
  만들고 연출로 사건을 부풀리지 않는다(unit 4 교훈).
- **`AreaCc` 의 다른 소비자** [S] · arm 이 `PeriodicTimer` 와도 공유되므로 보스의 주기적 광역
  CC 가 저작만으로 열린다. 이번 spec 은 그 에셋을 만들지 않는다.
