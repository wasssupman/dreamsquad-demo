# 2 — 말파이트 이관: 지진 = 스턴 3초 + 피해 40

## 목적

units 0·1 이 연 두 어휘의 **첫 소비자**. 말파이트 배치 스킬을 레거시 enum 에서 규칙으로 옮기고
그 김에 피해 40 을 얹는다. 이 unit 이 끝나면 `StunNearby` 의 소비자가 0이 된다.

## 변경 대상

- 신규 `Assets/_Project/Data/Abilities/Ability_Quake_Malphite.asset` (`UnitSkillAbility`)
- 신규 `Assets/_Project/Data/Projectiles/Projectile_MalphiteQuake.asset` (`ProjectileData` — 폭발 뷰)
- `Assets/_Project/Data/Defenders/Defender_Malphite.asset` — 능력 부착 + 레거시 필드 0
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — `OnPlaceRuleClause` 에 2종 배선
- Google 시트 `desc` 열 (임포트 축)

## 구현

### 능력 SO — mechanic 2개, 같은 트리거

```
Ability_Quake_Malphite
├─ mechanic[0]  trigger.kind = OnPlace(9)   payload.kind = AreaCc(26)
│                 ccKind = Stun · duration = 3 · tileRange = 2
└─ mechanic[1]  trigger.kind = OnPlace(9)   payload.kind = SelfTileAoe(2)
                  magnitude = 40 · tileRange = 2 · projectile = Projectile_MalphiteQuake
```

두 슬롯이 같은 배치 사건에 함께 발화한다(`JustDeployed` 는 엔티티 단위 1회 질문이고
슬롯 루프가 그 아래를 돈다 — `on-place-skill-rework` unit 0).

⚠ **`tileRange` 를 둘 다 2 로 맞춘다.** 갈라지면 「멈춘 적」과 「아픈 적」의 집합이 달라져
화면에서 규칙을 읽을 수 없다. 값이 하나여야 한다면 그건 후속 리팩터의 신호이지 지금 묶을 축은 아니다.

### 폭발 뷰 SO

`Projectile_JjangssenQuake` 와 **같은 형태**: `projectilePrefab` 없음(날아가지 않는다) +
`hitPrefab` 만 있는 「폭발 한 방」 슬롯. `speed 0` · `flightMode 0` · `impactTileRange` 는
슬롯이 덮으므로 뷰 값은 참고용. `hitVfxScale` 로 반경 2 를 덮는 크기를 맞춘다.

먼저 기존 폭발 VFX 프리팹을 훑어 **재사용 후보**를 찾는다. 보스 지진(`Jjangssen`)의 것을 그대로
쓰면 「보스가 온다」와 헷갈리므로, 같은 프리팹을 쓰더라도 `tintColor`/`hitVfxScale` 로 가른다.
적당한 후보가 없을 때만 `unity-vfx-authoring` 스킬로 신규 제작한다.

### 말파이트 에셋

| 필드 | 현재 | 변경 |
|---|---|---|
| `onPlaceEffect` | 9 (`StunNearby`) | **0** (`None`) |
| `onPlaceRange` | 2 | **0** |
| `onPlaceDuration` | 3 | **0** |
| `abilities` | `[]` | **`[Ability_Quake_Malphite]`** |

⚠ `knockupOnHitSec`(0.8) · `knockupVisualHeight`(1.2)는 **그대로 둔다.** 평타 넉업이 쓰고,
계약 3 에 따라 배치 스킬의 체공도 이 값을 읽는다.

⚠ 레거시 필드를 0 으로 내리지 않으면 bake 가 「둘 다 발동한다」 loud 경고를 내고 실제로 스턴이
두 번 걸린다(브리지 + arm). **경고가 뜨면 이관이 덜 끝난 것이다.**

### 문안 — 규칙이 이긴다, 그리고 시트가 최종이다

`UnitKitSummary.OnPlaceRuleClause` 에 두 절을 배선한다(미배선이면 **조용히 빈다** — 그 함수의
`default: continue` 는 의도된 fail-quiet 이고 `UnitKitSummaryTests` 전수 순회가 잡는다):

- `AreaCc` → `배치 시 주변 {tileRange}타일 광역 {CC 이름}` (「기절」이 아니라 **「스턴」** —
  `on-place-skill-rework` unit 6 의 어휘 결정)
- `SelfTileAoe` → `배치 시 주변 {tileRange}타일 광역 피해`

두 절이 모두 나오므로 요약은 "…, 배치 시 주변 2타일 광역 스턴, 배치 시 주변 2타일 광역 피해." 가
된다. **한 문장으로 접지 않는다** — 접으려면 문안이 두 mechanic 의 조합을 해석해야 하고,
그 해석은 조합이 늘 때마다 깨진다.

`desc` 는 `DefenderStatDto.desc` 로 **시트가 덮는 축**이다. 에셋만 고치면 다음 로비 진입에
되돌아간다 → 시트의 말파이트 `desc` 를 같이 고친다:
`배치 스킬: 주변 2타일 광역 스턴 3초 + 피해 40`.

## 완료 기준

- [ ] compile 0 error · bake 경고 0(레거시 공존 경고가 **뜨지 않아야** 한다)
- [ ] EditMode — `MalphiteKnockupAuthoringTests` 갱신: 배치 스킬이 능력 SO 에서 나오고
      평타 넉업 값(0.8 / 1.2)은 그대로
- [ ] Play 육안 (적 무리 위에 배치)
  - 적들이 **튀어올랐다 떨어지고**(0.8초), 데미지 숫자 40 이 뜨고, 이후 굳어 있다
  - 3초 뒤 다시 움직인다
  - 반경 2 밖 적은 멀쩡하다
- [ ] 로비에서 말파이트 카드 설명이 스턴·피해 두 줄을 모두 말한다(시트 임포트 후에도 유지)
