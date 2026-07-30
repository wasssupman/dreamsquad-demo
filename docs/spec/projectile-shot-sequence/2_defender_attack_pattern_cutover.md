# unit 2 — defender attack pattern 이관

## 목적

방향 공격의 한 번의 RESOLVE를 공용 `EmitterInstance` 한 개로 번역한다. 샷건너와
머신거너의 발수·각도·개별 interval을 `ProjectilePatternData`로 옮기고, 기존
`VolleyFireState`/`VolleyMath` 이중 스케줄 경로를 제거한다.

이 unit은 공격 트리거와 두 defender 데이터 이관까지만 담당한다. 화면에서 보이는 공통
발사점 투영은 unit 3 범위다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Abilities/DirectionalVolleyAbility.cs`
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/EmitterTick.cs`
- `Assets/_Project/Scripts/Bridge/{BattleBridge,BattleBridge.Dreamcatcher}.cs`
- `Assets/_Project/Data/Projectiles/Pattern_Defender_{MachineGunner,Shotgunner}.asset`
- `Assets/_Project/Data/Abilities/Ability_Volley_{MachineGunner,Shotgunner}.asset`
- `Assets/_Project/Data/Defenders/Defender_Shotgunner.asset`
- legacy `VolleyFireState`·`VolleyMath`와 관련 EditMode 테스트

## 구현

- `DirectionalVolleyAbility`는 발수·균일 간격·총 확산각을 직접 소유하지 않고
  `ProjectilePatternData pattern`을 참조한다. `RequiresFacing` 계약은 유지한다.
- Bridge는 ability의 pattern과 defender projectile가 같은 barrel인지 검증하고,
  정상일 때 `PatternSlot` 1개와 빈 `EmitterInstance` 버퍼를 스폰 시 사전 부착한다.
  잘못된 참조·shot sequence·binding 조합은 경고 후 패턴 부착을 거절한다.
- 방향탄 RESOLVE는 실효 damage, sim 원점, `DeployedFacing`, `attackRange * tileSize`,
  projectile payload·bounce·priority/heavy 값을 template에 스냅샷한다.
- 패턴을 보유한 defender는 직접 request를 만들지 않고 `EmitterInstance` 한 개를 push한다.
  `ProjectileEmitterSystem`이 같은 sim frame에 첫 탄을 carrier로 만들고 이후 탄을 완주한다.
- facing 방향 공격은 START 시 레인 witness로 발사 허가를 확정한다. `hitDelaySec` 동안 witness가
  죽거나 레인 밖으로 이동해도 targetless 궤적은 고정 `DeployedFacing`으로 RESOLVE한다.
  witness가 없는 RESOLVE에서는 타깃 의존 CC·게이트와 AttackN 카운터/payload를 실행하지 않는다.
- 머신거너는 같은 방향 공격 보호를 공유하지만 `hitDelaySec=0`이라 START 프레임에 바로 첫 탄을
  만든다. 폭탄맨은 `BombLauncherState` blind-fire 분기에서 적 유무와 무관하게 즉시 발사하므로
  이 RESOLVE 보정 대상이 아니다.
- 다음 공격 대기는 shot step의 interval 합만큼 기존 남은 cooldown에 더한다. 진행 중인
  sequence는 레인 소멸·CC와 무관하게 완주하고 host 사망 시 중단한다.
- 샷건너는 10발을 `-30°..+30°`에서 균등 분할하지 않고 중심 밀집+불규칙 외곽으로
  배치한다. `5발 즉발 → 0.025초 뒤 3발 → 0.025초 뒤 2발`의 마이크로 클러스터로
  총 0.05초 안에 전개하고, pellet speed는 10→14로 높인다. 탄당 damage는 6,
  `attackRange=4`이므로 각 request의 `maxDistance=4*tileSize`다.
- 머신거너는 10발, 각도 0, 0.1초 간격과 기존 projectile·damage·cooldown을 유지한다.
- Dreamcatcher의 FacingVolley 런타임 판별은 일시적인 `DeployedFacing`이 아니라 영구
  `PatternSlot` 보유 여부를 사용한다.

## 완료 기준

- Unity 컴파일 오류가 없다.
- 순수 테스트가 가변 step interval 총시간 계산을 검증한다.
- AttackSystem+Emitter 통합 테스트가 trigger당 instance 1개, trigger frame 첫 탄,
  10발 순서·각도·간격, 실효 damage 스냅샷, 4타일 maxDistance를 검증한다.
- 레인이 비거나 CC가 생겨도 시작된 sequence가 완주하고, 다음 트리거는 마지막 탄 이후
  cooldown을 기다린다.
- 샷건 START 후 첫 RESOLVE 전에 witness가 사망하거나 레인 밖으로 이동해도 첫 클러스터와
  나머지 sequence가 발사된다.
- 비방향 단발 projectile와 기존 boss pattern 테스트가 회귀 없이 통과한다.
- ECS 리뷰에서 버퍼 사전 부착, Combat 소유 쓰기, Burst/unmanaged, 시스템 순서 위반이 없다.
