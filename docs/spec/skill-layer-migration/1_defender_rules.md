# 1 — 방어유닛 규칙 5행

## 목적

이미 `DcMechanic` 규칙 경로를 타는 방어유닛 능력 5개를 concrete 로 옮긴다.
가장 싼 가족이다 — 어휘가 이미 같고 bake 도 이미 진영 중립이다.

## 변경 대상

- `Assets/_Project/Data/Abilities/` — SkyStrike · Taunt · AreaShield · OnPlaceBlast · BombMan
- `Assets/_Project/Scripts/Skills/Concrete/`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeUnitMechanics` 가 `skillId` 를 굽는다

## 구현

1. 5행 전부 `DcTriggerKind.OnPlace` 다(`DefenderTriggerArmed` 가 그것만 연다).
2. **`BakeUnitMechanics(hostIsEnemy:…)` 는 이미 진영 중립이다** — 적/방어유닛 공용이고
   `hostIsEnemy` 가 `BuildPatternTemplate` 의 `targetFaction` 을 파생시킨다.
   ⚠ 빠뜨리면 **방어유닛이 쏜 패턴이 방어유닛을 때린다.** concrete 이전 후에도 이 파생이
   유지되는지 확인한다 — 토대 unit 2b 의 `Opponents(caster)` 가 그 자리를 대신해야 한다.
3. **`EmitProjectilePattern` arm 은 concrete 로 옮긴다.** 오늘 `AttackSystem` 과
   `BossPeriodicTriggerSystem` **두 곳에 사본**이 있다(`skill-fire-dispatch` 계약 5 가
   「세 번째를 만들지 말라」고 경고).
   ⚠ **둘은 같은 코드가 아니다**(2026-08-25 실측). 겹치는 것은 `EmitterInstance` 구성 +
   `EmitterTick.Begin` + 카운터 전진 4줄뿐이고, 조준은 서로 다른 데서 온다 — arm 은
   배치 방향/최근접이고 RESOLVE 는 9단 타겟팅 결과다. 그래서 이 unit 이 없앨 수 있는
   사본은 **arm 쪽 하나**이고, RESOLVE 쪽은 `AttackN` seam 이전 때 합류한다.
4. 이 가족이 끝나면 **보스 스킬과 방어유닛 스킬이 같은 `ISkill` 목록에 섞인다** —
   README 부수 질문이 여기서 처음 참이 된다.

## 진행 상태 (2026-08-25)

**5행 중 1행이 이미 이전됐다** — 의도한 게 아니라 구조 덕이다.

`Ability_AreaShield_ShieldShuttle` 은 `OnPlace × GrantShield` 인데, **`OnPlace` 슬롯도
`BossPeriodicTriggerSystem` 이 소비한다**(payload arm 을 주기와 공유하는 것이 그 시스템에
얹은 이유다 — 브리지에 실행부를 두면 `EmitProjectilePattern` 의 세 번째 사본이 된다).
그래서 unit 0 이 `GrantShield` 를 이전할 때 이 행도 같이 넘어갔고, `AbilityAreaShieldTest` 가
초록인 것이 그 증거다.

**함의**: 이 spec 의 이전 단위는 **payload** 이지 트리거나 가족이 아니다. 한 payload 를
옮기면 그것을 쓰는 모든 행이 같이 간다 — census 의 「행」은 검수 단위이고 작업 단위가 아니다.

남은 4행:

| 에셋 | payload | 상태 |
|---|---|---|
| `Ability_Taunt_Bastion` | `AreaTaunt` | **이전됨** (2026-08-26) |
| `Ability_SkyStrike_Cannon` | `EmitProjectilePattern` | **이전됨** (2026-08-25) |
| `Ability_OnPlaceBlast_Shotgunner` | 〃 | 〃 |
| `Ability_UnitSkill_BombMan` | 〃 | 〃 |

→ `EmitProjectilePattern` 하나를 옮기니 **이 3행 + unit 0 이월 2행 = 5행**이 한 번에 갔다.
그것이 unit 0 이 이 payload 를 여기로 넘긴 이유다. `AreaTaunt` 까지 끝나 **unit 1 은 5/5**.

## `EmitProjectilePattern` 이전에서 나온 것 (2026-08-25)

**이 payload 는 arm 을 지금 지웠다** — 다른 이전과 다른 점이다(나머지는 unit 8 철거까지
arm 이 남는다). 이유: 조준 규칙(`OnPlaceFireAim`)이 도메인이 호출해야 하는 순수 함수인데
`Wassup.Skills` 는 Battle 을 참조하지 않아 **옮길 수밖에 없었고**, 옮기고 나니
`[BurstCompile]` arm 이 그 managed 규칙을 부를 수 없다. 규칙을 두 벌로 두는 것보다
arm 을 먼저 지우는 쪽이 옳다. → `Wassup.Skills.SkillAim`(규칙·상수·부등호 무변경,
`SkillAimTests` 가 그 무회귀를 잡는다).

**카드 bake 가 여태 `skillId` 를 하나도 굽지 않았다.** 그래서 arm 을 지우려면 카드 경로도
같이 열어야 했다 — 다만 **이 payload 만** 열었다. 전체 맵을 부르면 이미 이전된 payload 를
저작한 **라이브 카드 9장**(`SelfTileAoe` 7 · `AreaSleep` 1 · 이 payload 1)이 한 커밋에서
전부 새 경로로 넘어간다. ⚠ **이건 unit 8 의 전제이기도 하다** — 카드 경로를 열지 않은 채
arm 을 철거하면 그 9장이 조용히 죽는다.

**조준 후보의 자는 유클리드다.** 셀 체비셰프로 고르면 대각선 끝 칸의 적이 「후보」이면서
사거리 밖이라(3칸 → 실거리 4.24 > 3.0), 조준은 성립하고 탄은 도중에 소멸해 **발사 연출만
나가고 아무도 안 맞는다**. 포트의 `RangeMetric.Euclidean` 이 그 자를 소유한다.

**`MatchTraversalLayers` 는 선언만 돼 있고 구현이 없었다**(포트 flag ↔ 어댑터 미구현).
이 payload 가 첫 소비자라 여기서 채웠다 — 안 채웠으면 근접 유닛이 하늘의 적을 겨누고
그 탄은 게이트에 막혀 아무도 못 맞혔을 것이다. 투트랙 리뷰 양쪽이 못 잡은 자리다.

**`SimEntityId` 가 없는 시전자는 스킬 레이어에서 자기 자신도 못 찾는다** — 어댑터의 핸들
역변환이 그 값으로 풀을 스캔하기 때문이다. 감지도 되고 concrete 도 불리는데 모든 질의가
빈손이라 **완전한 침묵**이 된다(이 unit 의 그물이 실제로 그 상태에 빠졌다).
`BuildCaster` 가 이제 loud warn 을 낸다.

**발사 명세는 같은 프레임에 나가야 한다.** 은퇴한 arm 이 `[UpdateAfter]` 로 갖고 있던
계약이라, seam 셋에 `[UpdateBefore(ProjectileEmitterSystem)]` 를 옮겨 걸었다. 안 걸면
정렬이 빌드마다 달라져 1프레임 지연이 오락가락한다.

## `AreaTaunt` 이전에서 나온 것 (2026-08-26)

`AreaTauntSkill`(Id 8). arm 은 **남긴다** — 발사 명세와 달리 Burst/managed 충돌이 없고,
카드가 이 payload 를 저작하지도 않는다(라이브 카드 kind 목록에 23 이 없다). 철거는 unit 8.

**자가 다르다.** 도발은 체비셰프이고 발사 명세는 유클리드다. 같은 unit 안에서 갈리는 이유는
게임 규칙이다 — 도발은 「몇 칸 안」이라 대각선이 같은 거리로 세고, 발사는 **탄이 실제로
날아가는 거리**라 대각선이 더 멀다. 그물이 이 차이를 각자 한 줄로 못박는다.

**게이트를 복제하지 않는다.** 보스 면역·공격 수단 부재·도달 불가는 어그로 시스템 소유라
concrete 가 판정하지 않는다. concrete 가 소유하는 것은 「누구를 부르나」(반경·상대 진영·
이번 프레임 합법 후보)까지고, 통행 층만 여기 있다 — 그건 「불려온 뒤」가 아니라
「부를 수 있나」라서 후보 조건이 맞다.

## ⚠ 손으로 만든 테스트 더미는 `SimEntityId` 가 없다 (2026-08-26)

PlayMode e2e 헬퍼 다수가 `em.CreateEntity()` 로 적 더미를 짓고 이 컴포넌트를 안 붙인다.
예전 arm 은 `Entity` 를 직접 들고 다녀서 상관없었지만, **스킬 레이어는 그 값으로 핸들을
역변환**하므로 미발급 더미는 조준·도발의 대상이 될 수 없다.

이게 조용하지 않게 두 곳을 고쳤다:

- 어댑터 `Collect` 가 **역변환 불가 후보를 후보에서 뺀다.** 안 빼면 concrete 가 받는 것은
  「없음」이 아니라 **월드 원점에 선 유령**이다(`Position` 이 부재를 0 으로 접는다).
  실제로 그 유령이 샷건맨 부채꼴의 조준을 훔쳐 **등 뒤를 쏘게** 만들었다.
- `BuildCaster` 가 미발급 시전자에 loud warn 을 낸다.

테스트 쪽은 `BattleBridgeTestAccess.AttachSimEntityId` 로 **라이브와 같은 발급기**를 쓴다 —
값을 임의로 정하면 번호가 겹쳐 역변환이 엉뚱한 엔티티를 돌려준다.

**Boss\* 그물이 여태 초록이던 이유**: 그쪽은 실제 스폰 경로(`SpawnUnit`)를 타서 ID 가
발급된다. 손으로 만든 더미를 쓰는 그물만 이 함정을 밟는다 — 다음 unit 들이 payload 를
옮길 때 같은 증상을 만나면 여기를 먼저 볼 것.

## 완료 기준

- [x] 5행 중 4행이 concrete 로 존재한다 (`GrantShield` 1 + `EmitProjectilePattern` 3)
- [x] `EmitProjectilePattern` 의 **arm 사본이 사라졌다** (RESOLVE 사본은 `AttackN` seam 몫)
- [x] 조준 후보가 «상대 진영» 에서 나온다 — 방어유닛이 쏜 패턴이 방어유닛을 조준하지 않는다
      (`EmitPatternSkillTests` + `ProjectileEmitterIntegrationTests.EnemyHost_TargetsDefenders…`)
- [x] `ISkill` 레지스트리에 적 스킬과 방어유닛 스킬이 함께 있다 (7종)
- [x] EditMode 2700/2702 초록 (남은 2건은 이 작업과 무관한 선행 실패 — 아래)
- [x] `AreaTaunt` 1행 (`AreaTauntSkillTests` 8 + `OnPlaceTauntNearbyTest` 4 초록)
- [x] PlayMode: 도발 4 · 부채꼴 2 · 폭탄맨 · 스카이스트라이크 · 라우팅 2 초록
- [ ] Play 스모크(육안)

⚠ **테스트 격리 선행 문제**: `SkillLayerRoutingTest` 는 **단독으로는 초록**인데 다른
PlayMode 클래스와 묶어 돌리면 `Tween's OnComplete callback was ignored` 로 빨개진다.
앞선 테스트가 씬 파괴 후에 완료되는 tween 을 흘리고, 이 클래스만 `LogAssert` 를 안 눌러서
**그 오류를 대신 받는다**. 이 spec 의 결함이 아니다 — 다만 이 클래스가 남의 누수를 잡는
카나리아 역할을 하고 있다.

⚠ **선행 실패 2건**(내 변경을 stash 한 상태에서도 동일하게 빨감):
`DreamcatcherCardAssetTextTests.CardAssets_UseStructuredSummaryWhenDataExists`(boomerang) ·
`UnitKitCatalogTests.CatalogDescriptions_UseThreeFixedSections`(bomb_man). 문안/요약 계열이라
시트↔SO 드리프트가 유력하다. 이 spec 의 작업이 아니다.
