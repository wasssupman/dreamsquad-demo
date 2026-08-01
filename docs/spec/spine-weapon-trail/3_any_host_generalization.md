# 3 — 호스트 일반화 + 보스 적용

## 목적

궤적을 **디펜더 종속에서 떼어낸다**. "어떤 유닛 혹은 구조물에도 들어가는 기능"이 되어야 한다
(2026-08-01 사용자 지시). 그 첫 소비자가 보스다.

unit 1 에서 opt-in 을 `IDefenderSpineExtras` 에 둔 것은 "적 제외가 코드 분기 없이 성립한다"는
이점 때문이었는데, 그 이점이 이제 **제약**이 됐다. 적·보스·구조물을 넣을 길이 막힌다.

## 변경 대상

- `Assets/_Project/Scripts/Data/IDefenderSpineExtras.cs` — 궤적 필드 2개 **제거**
- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs` — 같은 필드 2개 **추가**
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — 필드 + 접근자 (적/보스가 얻는다)
- `Assets/_Project/Scripts/Presentation/WeaponTrailRig.cs` — **신설**
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 부착·타이머 로직을 rig 로 위임
- `Assets/_Project/VFX/WeaponTrail_Slash.prefab` — rig 컴포넌트 부착(Variant 상속)
- `Assets/_Project/Data/Enemies/Enemy_Boss_*.asset` — 무기 스킨 · 공격 애니 · 궤적 배선

## 구현

### 1. 필드를 공용 인터페이스로 이동

`SpineWeaponTrailPrefab` / `SpineWeaponTrailEndNormalized` 를 `ISpineUnitVisualData` 로 올린다.
`DefenderUnitData` 와 `AttackUnitData` 가 모두 구현하므로 **디펜더·적·보스가 한 번에 대상이 된다**.
게이트는 그대로 **프리팹 null = 무궤적** — 잡몹 전원은 미할당이라 영향 없다.

### 2. `WeaponTrailRig` — 리그를 자립시킨다

부착·타이머를 뷰에서 떼어 리그 프리팹 루트의 컴포넌트로 옮긴다. 이게 "어떤 호스트에도 붙는다"를
성립시키는 지점이다 — 호스트는 두 메서드만 알면 된다.

| API | 동작 |
|---|---|
| `Bind(SkeletonRenderer)` | `BoneFollower` 에 스켈레톤 주입 + `Initialize`. **null 허용** |
| `Play(float seconds)` | `StartTrail` + `seconds` 뒤 자동 정지. 연속 호출은 정지 시각을 밀 뿐 |
| `StopNow()` | 즉시 정지 |

**`Bind(null)` 이 허용되는 이유가 구조물 지원이다.** 본이 없는 호스트(스프라이트 구조물 등)는
`BoneFollower` 없는 리그 Variant 를 쓰고, Point A/B 가 부모 트랜스폼을 그대로 따라간다.
`HS_SwordMeshTrail` 은 두 Transform 만 보므로 스켈레톤 유무를 모른다.

### 3. `SpineUnitView` 는 위임만 한다

`_defenderExtras` → `_visualData` 로 읽는 곳을 바꾸고, Instantiate 후 `Bind`,
`PlayAttack` 에서 `Play(window)` 호출. 뷰에서 `_trailStopAt`·코루틴이 사라진다.
`window = Duration × endNormalized ÷ entry.TimeScale` 산식은 unit 1 그대로.

### 4. 보스 적용

| 보스 | 무기 | 공격 애니 | 룩 | endNormalized |
|---|---|---|---|---|
| 짱쎈놈 | `gear_right_c_39` (보유) | Attack2 → **Attack3** | ToonFire | 0.31 |
| 나이트메어 | **없음 → `gear_right_c_40` 추가** | Attack1 → **Attack3** | ToonWater | 0.31 |

`attackRange` 는 둘 다 이미 **2** 였다(요청값과 동일). 참고로 이 필드는 `EnemyStatDto.attackRange`
로 **시트 동기 대상**이라, 값을 바꿔야 한다면 SO 가 아니라 시트를 고쳐야 한다 —
SO 만 고치면 `LoginAutoImport` 가 되돌린다.

**애니를 Attack3 로 통일하는 근거**(순 스윙 폭 실측, Shoulder_r + Hand_r 합산):

| 애니 | 길이 | 순 스윙 폭 | 피크 |
|---|---|---|---|
| Attack3 | 0.867 | **178.8°** | 0.267 |
| Attack1 | 1.200 | 57.4° | 0.133 |
| Attack2 | 0.700 | **24.7°** | 0.233 |

Attack2 는 24.7° — 궤적이 거의 안 그려진다. Attack1 도 Attack3 의 1/3 이다.
사용자가 "궤적이 잘 보일 모션으로 바꿔도 된다"고 허락했으므로 둘 다 Attack3 로 통일한다.
**되돌릴 때는 `attackAnimation` 만 되돌리면 된다** — 궤적 배선과 독립이다.

## 완료 기준

- compile, 콘솔 에러 0
- 디펜더 7종 궤적 **회귀 없음**(필드 이동이 기존 배선을 깨지 않는다)
- 보스 2종에 궤적이 붙고, 나이트메어가 무기를 든다
- 잡몹 전원 무궤적(프리팹 null 게이트 유지)
- 실전 보드 촬영으로 보스 궤적 확인

## 검증 결과 (2026-08-01)

- **디펜더 회귀 없음** — 필드가 인터페이스를 옮겨도 7종 배선 전부 유지(7/7)
- **base 리그에 `WeaponTrailRig` 부착 → Variant 7종 전부 상속**(7/7)
- **적 데이터 경로 성립** — 보스 2종이 `AttackUnitData`(=`ISpineUnitVisualData` 만 구현)로
  리그 프리팹을 들고, `Bind`/`Play` 로 궤적이 붙는다. `IDefenderSpineExtras` 없이 동작 =
  **디펜더 종속 해제 실증**
- 나이트메어에 추가한 `gear_right_c_40` 무기가 실제로 렌더된다
- 잡몹(Kindler·Rootcaster) 미할당 = 무궤적 유지

- **실제 보드 카메라 확인 완료** — 두 보스 모두 궤적이 깨끗하게 그려진다.
  짱쎈놈 주황(ToonFire) · 나이트메어 청록(ToonWater), 아티팩트 없음

### 계측기 교훈 — 하네스 카메라로 룩을 판정하지 말 것

1차 보스 촬영은 **URP 추가 데이터가 없는 맨 `Camera`** 로 찍었고, 화면 전체에 어두운 사각
헤이즈가 꼈다. 파티클을 꺼도 남아서 "보스 스케일에서 머티리얼이 깨진다"고 오판할 뻔했다.
`CopyFrom(main)` 으로 **실제 보드 카메라를 복제**해 다시 찍으니 헤이즈가 전혀 없다 —
계측기 탓이었다. 룩 판정은 반드시 `CopyFrom(main)` 경로로 할 것.

(같은 조건의 상대 비교는 유효했다 — Simple 만 사각 경계로 깨지고 ToonWater 는 깨끗했던
관찰에 따라 나이트메어를 ToonWater 로 바꿨고, 실제 보드에서도 좋다.)

### 관측 — 보스 궤적이 크다

보스는 `spineVisualScale` 이 커서 리그가 그대로 스케일되고, 나이트메어의 호는 **약 4타일**을
훑는다. 사거리 2 와의 격차가 디펜더(궤적 ~1타일 / 사거리 1~2)보다 크다.
"보스는 크게"가 의도라면 그대로 두고, 줄이려면 보스 전용 리그 Variant 에서 Point A/B 만
좁히면 된다(Variant 는 자식 트랜스폼을 오버라이드할 수 있다).

## 비목표

- 실제 구조물 호스트 구현. `Bind(null)` 경로와 BoneFollower 없는 Variant 는 **길만 열어두고**
  소비자가 생길 때 만든다. 지금 만들면 검증할 대상이 없다

---

확인: 2026-08-01 · 커밋 `bd6f079a` `dd573654` · 사용자 Play 확인 완료.
리뷰 결함 2건(슬로우모 창 · 파티클 정렬)은 `edf4b67d` 에서 수정·재측정 완료.
