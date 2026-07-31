# 2 — 가독성 튜닝 + 근접 로스터

## 목적

unit 1 판정이 **"보이지만 참격으로 읽히지 않는다"** 로 나왔다. 이 unit 은 그걸 읽히게 만들고,
통과하면 근접 로스터로 확대한다. **코드 변경 0 을 목표로 한다** — 필요한 손잡이는 unit 0/1 에서
전부 authoring 으로 빼뒀다(프리셋 SO · 리그 프리팹 · 유닛 SO).

## 변경 대상

- `Assets/_Project/VFX/WeaponTrailPreset_{Look}.asset` — 룩 5종 (벤더 프리셋 복사 + 오버라이드)
- `Assets/_Project/VFX/WeaponTrail_Slash.prefab` — base 리그, Point A/B 오프셋
- `Assets/_Project/VFX/WeaponTrail_Slash_{Look}.prefab` — 룩별 **Prefab Variant**
- `Assets/_Project/Data/Defenders/{Bruiser,Guardian}.asset` — 프리팹 배선

## 룩 세트와 교체 방법 (2026-08-01 사용자 요청)

**룩은 벤더 것을 그대로 쓴다.** 새로 칠하지 않는다 — 우리가 덮는 필드는 색이 아니라
정렬·포인트 계산·수명뿐이고, 전부 기술적 필수이거나 스케일 맞춤이다.

```
WeaponTrailPreset_{Look}.asset      벤더 프리셋 복사본 + 오버라이드 3+2
WeaponTrail_Slash.prefab            base 리그 (BoneFollower + Point A/B) · 룩 = ToonBlue
WeaponTrail_Slash_{Look}.prefab     Variant — preset 참조만 오버라이드
        ↓
DefenderUnitData.weaponTrailPrefab  유닛별로 골라 끼움
```

교체 비용 = **SO 필드 하나**. 유닛마다 다른 룩도 가능하다.
Point A/B 튜닝은 base 에만 있고 Variant 가 상속하므로 리그 조정은 한 곳에서 끝난다.

| 룩 | 벤더 출처 | 머티리얼 | 칼밑 파티클 |
|---|---|---|---|
| **ToonFire** (현재 기본값) | Slash toon fire | Path7Slash | `SlashToonFire.prefab` |
| ToonBlue (최초 제안) | Slash toon blue | Path14Slash | 없음 |
| ToonWater | Slash toon water | Path6Slash | `SlashToonWater.prefab` |
| Lightning | Slash lightning | Path4Slash | `SlashLightningTrails.prefab` |
| Simple (원소 중립) | Slash simple | Path0Slash | 없음 |

파티클은 프리셋의 `pointAEffectPrefabs` 가 **벤더 `Slash*.prefab` 을 그대로** 참조한다
(중첩 트레일 없이 순수 ParticleSystem 임을 확인). 벤더 프리팹을 활용하는 지점이 여기다.

**벤더 프리셋을 직접 참조할 수 없는 이유** — 복사본이 필요한 근거:
`sortingOrder 0`(유닛 뒤에 깔림) · `recalculatePointsOnAwake true`(스켈레톤 바운드를 잡음) ·
`startActive`(공격과 무관하게 켜짐). 셋 다 취향이 아니라 동작 결함이다.

## 구현

### 튜닝 순서 (효과 큰 것부터)

unit 1 실측 기준선: 리본 폭 **0.46 월드**, 수명 **0.2초**, 머티리얼 레이어 **1개**(toon blue),
유닛 화면 높이 **~60px**(1280×720).

| # | 손잡이 | 현재 → 방향 | 근거 |
|---|---|---|---|
| 1 | **크기** | 폭 0.46 → 1 타일 안팎 | 유닛 대비 너무 얇아 얼룩으로 보인다. Point B 를 밖으로 |
| 2 | **대비** | toon blue → 보드와 분리되는 색 | 청록·갈색 보드 위 옅은 파랑이 먹힌다. 프리셋 20종에서 고른다 |
| 3 | **수명** | 0.2 → 0.3 전후 | 이 스케일에서 0.2초는 호가 남기 전에 사라진다 |
| 4 | **레이어** | 1 → 2 (밝은 코어 추가) | 작은 크기에서 형태보다 **밝기**가 먼저 읽힌다. 드로우콜 +1/트레일 |

각 단계마다 **실전 보드 프레이밍**(배틀 카메라 pose 복제, 1280×720)으로 판정한다.
unit 0 의 정면 직교 클로즈업으로 판정하면 또 틀린다 — 그 뷰는 전부 커 보인다.

### 가독성 상한

근접 사거리는 1 타일이다. 궤적이 3 타일을 덮으면 "안 맞는 걸 벤 것처럼" 보인다.
**1~1.5 타일이 상한 후보** — 키우는 방향이지만 무제한은 아니다.
unit 0 문서의 "과장 = 가독성 부채" 경고는 클로즈업 기준이라 이 스케일에선 완화된다.

### 로스터

**가디언(`Defender_Guardian`) · 파이터(`Defender_Bruiser`) 2종에만 적용한다**
(2026-08-01 사용자 결정). 나머지는 미할당으로 남긴다 — 프리팹 null 이 곧 무궤적이라 별도 조치 불요.

참고로 근접(투사체 없음, `attackRange ≤ 2`) 후보는 더 있었다 — Bastion · Malphite · Slasher ·
ShieldShuttle · TooMuchTalker. 지금 넣지 않는 이유는 기술적 제약이 아니라 **범위 결정**이다.
확대는 두 유닛의 체감이 확인된 뒤에 별건으로 다룬다(후속 후보).
Ranger 는 사거리 2 지만 투사체를 쏘므로 애초에 대상이 아니다.

## 검증 결과 (2026-08-01)

### 확인됨

- **머티리얼 5종 A/B 비교** — 같은 리본에 머티리얼만 갈아끼워 동일 프레이밍 촬영.
  가독성 순위 `ToonFire > Cyan > Simple > Lightning > Red`. Red(Path18Slash)는 어두운 코어가
  얼룩으로 보여 탈락. 청록 보드에서는 **따뜻한 색이 분리된다**는 게 핵심 관찰
- **크기·수명 튜닝** — 폭 0.46 → **0.82 월드**, 수명 0.2 → **0.35초**. 실전 프레이밍에서 확실히 커짐
- **배선** — 파이터·가디언 두 유닛 모두 `WeaponTrailPreset_ToonFire` + 파티클 1개 부착 확인
- **게이트** — Archer/Cannon/Slasher 미할당(무궤적) 확인. 적 유닛도 리그 없음(`trail=NULL`) 관측
- 프리팹 5종 무결성(missing script 0), Variant 타입·프리셋 오버라이드 정상

### 미확인

- **파티클 포함 최종 룩의 실전 보드 컷** — Play 가 반복 종료돼 못 찍었다. 머티리얼 비교는
  파티클 없이 한 것이라, ToonFire 의 최종 인상은 아직 육안 확인 전이다
- 재이월 4건(카메라 이동 중 박제 · `LateUpdate` 순서 · 레이어 회수 · 드로우콜 실측) 그대로 남음

## 완료 기준

- **실전 보드 프레이밍에서 궤적이 공격 동작으로 읽힌다** — 사용자 육안 확인이 최종 판정
- 사거리(1타일) 대비 과장돼 보이지 않는다
- 근접 로스터 전원 궤적 · **원거리 유닛 무궤적 관측**(unit 1 미검증분)
- 코드 변경 0 (필요해지면 그 사실 자체를 기록하고 범위를 재검토)

### unit 1 에서 재이월된 검증

- **카메라 이동 중 박제**: 궤적 수명 안에 `CameraDirector` 가 카메라를 움직일 때 어긋나는지
- **`LateUpdate` 실행 순서**: `BoneFollower` ↔ `HS_SwordMeshTrail` 1프레임 지연이 보이는지
- **레이어 회수**: 유닛 사망 · 매치 종료 후 프레임을 넘겨 `Generated Mesh Trail` 잔존 0
- **드로우콜**: 레이어 수 × 동시 공격 근접 유닛 수. 레이어를 2로 올리면 여기서 확인
