# 4 — 에셋 저작 + 폭탄맨 배선

## 목적

앞 네 단위가 연 축 위에 실제 콘텐츠를 저작한다. **코드 변경 0 이 목표**다.

## 변경 대상 (전부 에셋)

- `Assets/_Project/Data/Hazards/Blocker_BombBarrel.asset` (신규, `BlockingHazardSO`)
- `Assets/_Project/Data/Projectiles/Projectile_Barrel.asset` (신규 — 날아가는 배럴)
- `Assets/_Project/Data/Projectiles/Projectile_BarrelBlast.asset` (신규 — **폭발 해결용**)
- `Assets/_Project/Data/Projectiles/Pattern_BombMan_Barrel.asset` (신규 — 패턴은 Projectiles 폴더에 산다)
- `Assets/_Project/Data/Abilities/Ability_UnitSkill_BombMan.asset` (신규, `UnitSkillAbility`)
- `Assets/_Project/Data/Defenders/Defender_BombMan.asset` — `abilities` 에 능력 추가

## 구현

- **배럴 SO**: 모양 **1칸**(계약 6 — 통째 봉쇄 회피) · 체력 · 수명 · 폭발 피해/반경/상한 ·
  **폭발 탄 SO(`Projectile_BarrelBlast`)** · 외형 프리팹 · 설치/파괴 VFX.
  파괴 VFX 가 곧 폭발 연출이다.
- **폭발 탄 SO**: 폭발 «해결»만 담당하는 즉발 탄이라 **비주얼이 필요 없다**(연출은 파괴 VFX
  가 이미 낸다). 그래도 **반드시 존재해야** 한다 — 없으면 index 가 0 으로 떨어져 엉뚱한 탄의
  비주얼이 한 프레임 번쩍인다(unit 0 의 ⚠).
- **탄 SO**: `flightMode = BallisticBlocker` · `spawnBlocker = Blocker_BombBarrel` ·
  `arcHeight` 는 **곡사답게 높게**(평타 폭탄의 구르기 0.7 과 확실히 달라야 한 유닛의 두
  투척이 화면에서 갈린다) · 외형은 배럴이 날아가는 그림.
- **패턴 SO**: `selection = Nearest` · `scopeTileRange = 2` · `shots` 1발 · `telegraphSec` =
  곡사 비행 시간. `damage` 는 쓰지 않는다(배럴은 착탄 피해가 없다).
- **능력 SO**: `mechanics = [{ trigger: OnPlace, payload: EmitProjectilePattern,
  pattern: Pattern_BombMan_Barrel }]`.
- 폭탄맨 `abilities` 에 추가. 기존 `Ability_Bomb_BombMan`(평타)과 **공존**한다 —
  같은 구체 타입 중복이 아니므로 저작 규율 위반 아님.
- ⚠ **패턴 슬롯 순서**: 평타 패턴 슬롯이 0번이라는 계약이 있다(샷건맨 선례). 폭탄맨 평타는
  패턴을 쓰지 않으므로(하드코딩 폭탄 경로) 충돌이 없어야 하지만, bake 후 실제 슬롯 구성을
  확인한다.

## 완료 기준

- [x] 코드 변경 0 — 에셋 저작만으로 성립했다.
- [x] 에셋 lane 통과.
- [x] (Play) 폭탄맨 배치 → 2타일 안 최근접 적에게 배럴이 곡사로 날아가 그 칸에 선다 →
      적들이 때린다 → 부서지며 터진다. 적이 없으면 수명 뒤 스스로 터진다.
- [ ] ⚠ **배럴 밑에 깔린 적이 끼지 않는지** 확인(적이 서 있던 칸에 배럴이 선다).
      차단 칸은 통행 층을 구분하지 않으므로 **공격 능력이 없는 적**(`AttackState` 없는
      돌격형)은 배럴을 부술 수단이 없어 **수명이 다할 때까지 그 자리에 갇힌다**(리뷰 지적).
      1칸이고 수명이 유한해 영구 교착은 아니지만, 체감상 허용 범위인지 눈으로 판정한다 —
      ⚠ **이 「수명이 유한해」 전제는 unit 7 이 지웠다가 unit 9 가 노후화로 되돌렸다.**
      지금 상한은 시한이 아니라 `healthDecayPerSec`(무방비 12초)다. 노후화를 끄면 이 위험이
      함께 되살아난다 —
      이 spec 의 미해결 위험 1순위.

확인 2026-08-22 · Play 에서 bake 실측: `boom=120 r=1 idx=4 life=12.0`(폭발 탄 index 가 유효하게 해석됨).
⚠ 이 로그의 `r=1`·`life=12.0` 은 **당시 값**이다. 현재는 `r=2`(unit 10) 이고 `life` 필드 자체가
은퇴했다(unit 7) — 시간 상한은 `healthDecayPerSec` 이 갖는다(unit 9).
⚠ **남은 위험은 여전히 열려 있다** — 적이 서 있던 칸에 배럴이 서므로 공격 수단 없는 적이 갇힐 수
있다. 이번 검증 구간에서는 재현되지 않았고 육안 판정이 남았다.
**외형 정정 2026-08-22(사용자 지적 「왠 폭탄이 돌덩이가 떨어지냐」)** — 초판이 기존 길막 설치물의
저작을 통째로 복사한 게 화근이었다. 그 프리팹(`BlockingHazard_Placeholder`)은 **렌더러가 없는 빈
프리팹**이고 눈에 보이던 것은 전부 딸려온 돌 VFX(`RockStack` 스폰 · `vfx_Hit_Rock03` 폭발)였다.
바로잡은 것:
  - 배럴 외형 = `_Project/Prefabs/Hazards/BlockingHazard_BombBarrel.prefab` (신규) —
    KayKit 플랫포머 팩 `bomb_B_red`(검정 몸통 + 빨간 띠) + `BlockingHazardPresenter`.
    ⚠ **자식 메시를 y +0.46 띄운다** — 피벗이 중심이라 그대로 두면 아래 절반이 판에 박힌다
    (프랍 「눕음/묻힘」 함정과 같은 뿌리). 실측 `bounds.min.y = 0.00` 으로 확인.
  - 스폰 VFX **제거**(돌무더기가 튀어나왔다). 파괴 VFX 도 제거 — 폭발 탄의 hit VFX 가 그 자리를 갖는다.
  - 폭발 hit VFX = `vfx_Hit_ExplosiveBullet01`(GA, 기존 돌 VFX 와 같은 벤더 계열).
  - 날아가는 배럴도 **같은 폭탄 메시**를 쓴다 — 던진 그 물건이 저기 서 있다는 것이 읽혀야 한다.
  - 덤: **폭탄맨 평타(`Projectile_Bomb`)의 착탄 VFX 도 돌 파편이었다.** 같은 폭발 VFX 로 교체했다.
색·크기(현재 1.15)는 저작값이라 판에서 더 키우거나 줄일 수 있다.

**2차 정정 2026-08-22(사용자 지적 「터지고 사라지지도 않고 폭발 이펙트도 깨진다 · 돌 쓰지 마라」)** —
세 증상이 서로 다른 원인이었고 셋 다 코드 결함이었다:
  1. **`BlockingHazardSO.spawnVfxPrefab` 은 죽은 저작이었다.** `SetSpawnVfxPrefab` 의 호출처가
     **0개**라 프리젠터는 항상 자기 직렬화 필드(양쪽 프리팹 모두 비어 있음)만 보고, 비면
     코드로 만든 **「떨어지는 돌」 폴백**(`CreateFallingRockParticles`)을 돌렸다. SO 에 무엇을
     저작하든 돌이 쏟아졌다. → 브리지가 `SetSpawnVfxPrefab(so.spawnVfxPrefab)` 을 넘기게 고쳤다.
     기존 방벽도 같은 이유로 저작을 무시당하고 있었다.
  2. **파괴 VFX 가 영원히 남았다.** `OnDestroyed` 가 부모 없이 `Instantiate` 만 하고 치우지
     않아 폭발 잔해가 판에 쌓였다(실측 루트 42개). 화면에서는 「터졌는데 안 사라진다」로 읽힌다.
     → 파티클 길이를 재서 `Destroy(fx, lifetime)`.
  3. **폭발 VFX 가 새까맣게 나왔다.** 두 겹의 문제였다.
     · GA 팩의 폭발 계열은 `Light01/02` 머티리얼이 **null** 이다(그 팩에서 성한 것은 Rock
       계열뿐 — 이 프로젝트가 돌 VFX 를 쓰고 있던 이유다).
     · 더 큰 문제: `vfx_Hit_ExplosiveBullet01` 은 **「폭발탄이 날아와 맞는」 연출**이라
       탄환 메시(`Bullet` / `ExplosiveBulletHit_Opaque`)가 들어 있고, **그 불투명 검은 탄환이
       화면을 채웠다.** 오프스크린 렌더로 확인. 검은 조각(탄환·`*_Opaque` 반구·왜곡)을 떼면
       남는 additive 섬광은 렌더가 거의 안 보여 쓸 수 없었다.
     → **벤더 팩을 포기하고 프로젝트 자체 폭발 `Meteor_Burst_SKELETON` 을 쓴다**(메테오 착탄이
       이미 쓰는 것 · 추적됨 · broken 0 · URP 에서 라이브 검증됨). 배럴 파괴 VFX +
       **폭탄맨 평타 착탄 VFX** 둘 다 이걸로. 인게임에서 주황빛 화염 폭발로 확인.
⚠ 스폰 VFX 를 SO 에 저작하지 않으면 **지금도** 돌이 쏟아진다 — 폴백 자체는 살아 있다.
