# 1 — Ability 에셋 7개 저작 + 유닛 배선 (data only)

## 목적

기존 7유닛의 능력 flat 값을 ability 에셋으로 복제 저작하고 각 유닛 `abilities` 에 배선한다.
**코드는 아직 flat 을 읽으므로 동작 불변** — cut-over(unit 2) 시점에 데이터가 이미 제자리에
있어 "능력 잃은 커밋"이 생기지 않는다.

## 변경 대상 (전부 `.asset` + `.meta` 짝)

`Assets/_Project/Data/Abilities/` (신규 폴더):

| 에셋 | 타입 | 값 출처 (현 flat 값 그대로) |
|---|---|---|
| `Ability_Volley_MachineGunner` | DirectionalVolley | shotCount 10 · interval 0.1 · spread 0 |
| `Ability_Hazard_BlockingCaster` | HazardCast | Defender_BlockingCaster 의 hazard 8필드 |
| `Ability_Hazard_FireCaster` | HazardCast | Defender_FireCaster 동일 |
| `Ability_Hazard_IceCaster` | HazardCast | Defender_IceCaster 동일 |
| `Ability_Hazard_PoisonCaster` | HazardCast | Defender_PoisonCaster 동일 |
| `Ability_Shield_ShieldShuttle` | ShieldCast | cooldown 4 · amount 150 · count 2 · MinHealth |
| `Ability_Bomb_BombMan` | BombThrow | landing 3 · travel 1 · fuse 1 · aoe 1 · cap 3 · arc 0.7 · dmg 60 · sleep 2.5 · stun 1.5 |

+ 유닛 에셋 7개의 `abilities:` 배선 (`Defender_MachineGunner` / 캐스터 4종 / `Defender_ShieldShuttle` / `Defender_BombMan`).

## 구현

- `id` 슬러그 = 에셋명 스네이크(`volley_machine_gunner` 등) — 시트 매칭키(계약 5).
- 값은 **현 flat 값의 기계적 복제** — 이 단위에서 밸런스 변경 금지(등가성 계약 6 의 전제).
- zoneHazard/blockingHazard 참조 guid 는 유닛 에셋의 기존 참조를 그대로 옮긴다.
- 저작은 YAML 직접 작성(본 세션 선례) 또는 UnityMCP `manage_scriptable_object`. `.meta` guid 신규 발급.
- flat 필드는 이 단위에서 **건드리지 않는다**(양쪽 공존) — 정리는 unit 2.

## 완료 기준

- [ ] import 클린(참조 guid 전부 해석, 콘솔 0).
- [ ] 각 유닛 인스펙터에서 abilities[0] 이 해당 ability 에셋을 가리킴.
- [ ] 동작 불변(코드 무변경) — Play 확인 불요, compile/import 만.
