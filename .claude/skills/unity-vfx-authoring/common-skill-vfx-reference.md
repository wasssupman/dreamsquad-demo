# Common Skill VFX Reference — 인벤토리 + 카탈로그

> rev 2026-09-12. **인벤토리(1절)가 정본**이다 — 실제 프리팹과 승인 상태. 카탈로그(2절)는 승인된 효과의 톤·타이밍 기록,
> 초안(3절)은 아직 프리팹이 없는 아이디어다. 새 항목은 사용자 승인 뒤에만 2절로 올린다.
> 레시피 A = 스프라이트 빌보드 · B = 절차 텍스처 + 쿼드 메시(지면 도형) · C = 벤더 사본 스트립 (`SKILL.md`).

## 1. 프리팹 인벤토리 (`Assets/_Project/VFX/`, 37개)

| 프리팹 | 역할 | 레시피 | 상태 | 비고 |
| --- | --- | --- | --- | --- |
| `SlashMark_SKELETON` | 참격 자국 — 부채꼴·띠 공용(브루저·말파이트·이쑤시개). **메시는 `ShapeMeshBuilder` 가 판정 도형(bake + 사거리 + 내 몸)에서 실시간 생성·캐시**, 텍스처는 4×1 램프(채움/테 알파·주황) | B′ | 승인 2026-09-12 · 절차화 2026-09-14(directional-attack-shape unit 7) | 다음 편집 때 접미사 제거 대상. 프리팹 메시는 빌트인 Quad 폴백 — `PlayHit(meshOverride)` 없이 재생하면 사각이 뜬다 |
| ~~`SlashMark_Band_SKELETON`~~ | 은퇴 2026-09-14 — 띠도 위 프리팹 하나(메시 교체) | — | — | 구운 텍스처·메시 4종도 함께 은퇴 |
| `DetectionMark_SKELETON` | 「발견」 표식(「!」 + 몸 플래시) | A | 승인 2026-09-08 | `ConfigureOneShot` 버스트 하한 4 주의 |
| `DamageNumberSpark_SKELETON` | 대미지 넘버 스파크 | A | 운용 중 | 오프스크린 렌더 튜닝 선례 |
| `DamageNumber_Popup` | 대미지 넘버 팝업 | A | 운용 중 | |
| `Meteor_Falling_SKELETON` / `Meteor_Burst_SKELETON` | 운석 낙하 / 착탄 | A(Burst 는 mesh) | 운용 중 | 착탄은 sim 시점(NativeQueue) |
| `Placement_SKELETON` | 배치 착지 링·퍼프 | B | 운용 중 | 퇴근 여운도 재사용 |
| `Portal_SKELETON` | 포탈 | A | 운용 중 | |
| `Tornado_SKELETON` | 회오리 장판 | A | 운용 중 | |
| `Whirlpot_Whirl_SKELETON` | 휠윈드 적(회오리) | A | 운용 중 | |
| `AreaBreath_Fire_SKELETON` | 엘리트 화염 브레스 | C(mesh·flipbook) | 운용 중 | |
| `StatusAura_Bleed/Fire/Ice/Poison` | 지속 피해 오라 4종 | C(mesh) | 운용 중 | `DotElement` 별 그림 |
| `EmpowerAura` | 강화 버프 오라 | A | 운용 중 | |
| `Heal_Applied_VFX` | 회복 적용 | A | 운용 중 | |
| `Burnout_Smoke` / `LastRun_Torchlight` | 번아웃 / 라스트런 상태 | C(WALLCOEUR·flipbook) | 운용 중 | 상태 VFX 벤더 교체(5f0d1241) |
| `BusterBeam` | 버스터즈 빔 | C(mesh) | 운용 중 | 세션형(주기 TTL) |
| `Bomb_Sphere` | 폭탄맨 구체 | A | 운용 중 | |
| `MalphiteHitEarth` | 말파이트 흙 폭발 | C(mesh) | 운용 중 | 참격 자국으로 슬롯 대체됨 — 2슬롯은 후속 |
| `ShotgunPelletFireball` / `Projectiles/vfx_Projectile_ShotgunBlast_Green` | 샷건 탄·발사 | C | 운용 중 | |
| `Projectiles/vfx_Projectile_Needle_Flame_SKELETON` | 화염 바늘 투사체 | C | 운용 중 | |
| `VFX_MachineGunFire` | 머신건 발사 | C(mesh·flipbook) | 운용 중 | |
| `InstinctWreck_Burst` / `InstinctWreck_Smolder` | 본능 붕괴 폭발 / 잔불 | C(mesh·flipbook) | 운용 중 | flipbook 「퍼프 무더기」 함정 선례 |
| `WeaponTrail_Slash` / `_Cyan` / `_Lightning` / `_Simple` | 무기 궤적 룩 4종 | C(Trail) | 운용 중 | 저작은 `docs/reference/weapon-trail-authoring.md` |

## 2. 카탈로그 (승인된 효과)

### Slash Mark (지면 참격 자국) — 부채꼴
- **상태**: 사용자 승인 2026-09-12 (directional-attack-shape rev 3). 1차 「Shuriken arc 파편 팬」은 카메라 pitch 에 눌려 폐기.
- **Visual elements**: 공격자 발밑에서 타겟 방향으로 눕는 60° 부채꼴 쿼드(절차 텍스처 `SlashMark_Sector.png`: 채움 α0.42 + 밝은 테 α0.95)
  + 잔불 자식 1개
- **Typical palette**: 웜 오렌지-화이트, 알파 블렌드(가산 아님 — 밝은 바닥에서도 형태 유지)
- **Timing**: 0.5s one-shot, 크기 커브 상수 1 · `hitDelaySec` 뒤 RESOLVE 시점 재생, 방향은 **재생 시점에 재측정**
- **Particle types**: mesh 파티클 1(`SlashMark_SectorQuad`, 꼭짓점 원점·+Y 전방·`alignment Local`·`startRotation3D x=90°`) + sprite 잔불
- **Project tone**: 「몸은 옆을 보는데 판정은 위」를 메꾸는 **유일한 시각 보증자**(정적 가이드 없음). 크기 = `attackVfxScale`(1.6),
  회전 = `attackVfxFacesTarget`, 원점 = `attackVfxAtAttacker`(발밑). 각 = 저작 `attackShape.angleDeg` 와 1:1
- **Suggested MaxParticles**: 8
- **sound_cue_hint**: 기존 `attackSfxClip` 그대로

### Slash Mark — Band(띠) 변형
- **상태**: 사용자 승인 2026-09-12 (「이쑤시개는 rect 로 확정」)
- **Visual elements**: 사각 쿼드(`SlashMark_Band.png` 테두리 텍스처, `SlashMark_BandQuad_w1_l3` x ±1/6·+Y 1) × `attackVfxScale 3` → 폭 1·길이 3
  = 판정 상자의 점-대상 코어(폭 = 저작 `width`, 길이 = 사거리 + 내 몸)
- **Timing**: 0.5s one-shot, 크기 커브 상수 1(첫 프레임부터 판정 크기)
- **Particle types**: mesh 파티클 1, 잔불 없음
- **Project tone**: 「찌르는 창」 — 부채꼴과 같은 슬롯·같은 규칙(재생 시점 방향 재측정), 형만 다르다
- **Suggested MaxParticles**: 4

### Detection Mark (「발견」 표식)
- **상태**: 사용자 승인 2026-09-08 (enemy-detection-range unit 9)
- **Visual elements**: head 「!」 pop, body flash ring
- **Typical palette**: alert yellow-orange (1.00, 0.72, 0.10), white core, dark rim
- **Timing**: 0.14s pop overshoot(×1.22) into 0.30s hold, 0.55s total fade; ring 0.30~0.38s expand
- **Particle types**: sprite
- **Reference games**: Metal Gear alert(!), Arknights 교전 진입 분위기 참고
- **Project tone**: 관습 기호(「!」)와 기존 오라 어휘(링)를 겹쳐 밀집 전투에서 둘 중 하나는 읽히게. 스폰 예고 라인의 빨강(1, 0.16, 0.12)과
  색을 분리 — 예고는 「올 것」, 표식은 「이미 봤다」. 글리프는 흰 코어 + 어두운 림을 텍스처에 굽고 StartColor 로 틴트. 링만 가산.
  ⚠ 원샷 경로(`ConfigureOneShot`)가 버스트 최소 4 를 강제 — shape 를 켜면 「!」가 넷으로 보인다.
- **Suggested MaxParticles**: 20 (Bang 8 + BodyFlash 12)
- **sound_cue_hint**: short alert blip, no tail

### Meteor
- **상태**: 운용 중(`Meteor_Falling/Burst_SKELETON`). 경고링은 직접 호출, 착탄은 sim 시점 NativeQueue — 투사체 파이프라인으로 수렴.
- **Typical palette**: 오렌지-화이트 코어, 어두운 잔재
- **Project tone**: 「자리에 떨어지는 것」(칸 반폭 0.5, 몸 없음) — 표기 반경은 `CenteredRingRadius` 와 같은 값

### Portal / Tornado / Whirlwind(휠윈드)
- **상태**: 운용 중(`Portal_SKELETON`, `Tornado_SKELETON`, `Whirlpot_Whirl_SKELETON`). 얇은 링 + 느린 회전 입자, 상한 50.
- **Project tone**: 휠윈드는 **전방위가 정체성** — 방향 도형을 저작하지 않는다(사용자 결정 2026-09-12).

### Status Aura (지속 피해 오라 4종) / Burnout / LastRun
- **상태**: 운용 중. WALLCOEUR·PixPlays 사본(레시피 C). `DotElement`(Bleed·Fire·Ice·Poison) 별 그림, origin 은 보지 않는다.
- **Project tone**: 오라는 유닛에 부착되는 지속 루프 — 상한 200, `Loop=true`, 정리 주체는 `StatusFx` 프레젠터.

## 3. 초안 (프리팹 없음 — 필요해질 때 승인 후 승격)

- **Fireball / Ice Shard / Lightning Bolt / Poison Drip**: 원거리 탄·착탄 아이디어. 현재 투사체 룩은 벤더 사본(`Projectiles/`)으로 충당.
- **Shield Aura / Heal Glow**: 실드는 `ShieldGrantedEventsSingleton` 원샷 VFX 로, 회복은 `Heal_Applied_VFX` 로 이미 다른 이름으로 운용.
- **Teleport Portal**: 입구/출구 같은 skeleton + 색 분기 아이디어. `Portal_SKELETON` 이 대체.
