# unit 17 — 배치 스킬 발동 순간의 카메라 셰이크 (말파이트·캐논·샷건맨)

## 목적

unit 16 이 연 셰이크 채널의 **첫 새 호출처**. 배치 스킬이 발동하는 순간 화면이 한 번 울려,
"방금 뭔가 큰 게 터졌다" 가 손가락 밑이 아니라 주변시로도 읽히게 한다.

대상 3종은 배치 스킬이 **평소 못 하는 일**을 하는 유닛이다(`on-place-skill-rework` 의 재설계
기준) — 그래서 사건의 무게가 화면에 실릴 값어치가 있다.

| 유닛 | 배치 스킬 | 구현 어휘 |
|---|---|---|
| 말파이트 | 반경 2 · 3초 광역 정지 | 레거시 `onPlaceEffect = StunNearby` |
| 캐논 | 반경 2 안 적마다 미사일 1발(융단폭격) | 규칙 `UnitSkillAbility{OnPlace × SkyStrike}` |
| 샷건맨 | 배치 방향으로 산탄 | 규칙 `UnitSkillAbility{OnPlace × ...}` |

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `onPlaceShakeStrength` / `onPlaceShakeDuration`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `FireOnPlaceCameraShake` + 두 seam 배선
- `Assets/_Project/Data/Defenders/Defender_{Malphite,Cannon,Shotgunner}.asset`
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` — 셰이크 진폭 천장 인상(아래 「완료 기준」)
- `Assets/_Project/Tests/EditModeAssets/OnPlaceShakeAuthoringTests.cs`

## 구현

**파이프라인을 묻지 않는다.** 배치 스킬은 지금 두 어휘로 구현돼 있다 — 레거시
`onPlaceEffect` enum 과 `abilities` 의 규칙(`UnitSkillAbility{trigger × payload}`). 실행 지점은
다르지만 **발동이 확정되는 순간은 브리지의 두 seam 하나로 모인다**:

| 경로 | seam |
|---|---|
| 탭 배치 | `TriggerOnPlaceAndSynergy` |
| D&D · 재배치 재무장 | `TriggerDeploymentOnPlaceSkill` |

둘 다 `ApplyOnPlaceEffect`(레거시)와 `MarkJustDeployedForRules`(규칙 arm)를 나란히 부른다.
셰이크를 그 옆에 두면 **어느 어휘로 만든 스킬이든 같은 대접**을 받고, 앞으로 배치 스킬을
어느 쪽으로 만들든 배선이 늘지 않는다. 스킬 지연(`placementSkillDelay`)은 호출자(배치
컨트롤러)가 이미 이 seam 호출 자체를 늦추므로 셰이크가 공짜로 따라간다.

**저작은 유닛이, 느낌은 카메라가.** 유닛은 「내가 얼마나 크게·얼마나 짧게 울리나」(0~1 세기와
초)만 갖고, 진폭·주파수는 `CameraDirectionConfig` 소유다(unit 16 계약). 두 축은 따로 튜닝된다 —
전체가 너무 흔들리면 config 를, 특정 유닛만 과하면 그 유닛을 만진다.

**게이트는 세기 하나**(`strength <= 0` 이면 호출 자체를 건너뛴다). 저작을 비운 유닛은 흔들지
않는다 — 배치할 때마다 흔들리면 스킬의 신호가 죽는다.

⚠ **「배치 스킬이 있는 유닛」이 기준이 아니다.** 레거시 `onPlaceEffect` 를 가진 디펜더만 12기고
(궁수도 `BindNearby` 를 갖는다), 그중 셰이크를 저작한 건 3기뿐이다. 기준은 **선별 목록** —
`on-place-skill-rework` 의 재설계 기준대로 「평소 못 하는 일을 하는」 스킬만 화면을 흔든다.
전부에 달면 «배치 = 항상 흔들림» 이 되어 구분이 사라진다.

**저작값(2026-08-21 시작점, 체감 튜닝 대상)**

| 유닛 | 세기 | 길이 | 노린 그림 |
|---|---|---|---|
| 말파이트 | 1.0 | 0.35s | 땅이 꺼지는 한 방 — 3초 정지의 무게 |
| 캐논 | 0.75 | 0.5s | 여러 발이 쏟아지는 동안 낮게 계속 |
| 샷건맨 | 0.6 | 0.18s | 짧고 날카로운 한 방 |

## 완료 기준

- 저작 계약 EditMode(Assets 레인): 3종이 세기·길이를 **둘 다** 저작한다(반쪽 저작 = 조용히
  안 울림), 세기는 0~1, 배치 스킬 없는 유닛(궁수)은 0.
- 값 자체는 못박지 않는다 — 체감 튜닝 대상이다(밸런스 리터럴 금지).
- Play: 말파이트·캐논·샷건맨 배치 시 화면이 울리고, 궁수 배치 시에는 울리지 않는다.
  탭 배치와 드래그 배치 **양쪽**에서 울린다(seam 이 둘이다).

확인 완료: 2026-08-21 사용자 Play 확인 («괜찮다»). 단 최초 체감은 «약하다» 였고, 원인은
유닛 저작이 아니라 **카메라 config 의 진폭 상한**이었다(말파이트가 이미 세기 1.0 = 천장).
`shakeMaxPosAmp 0.04 → 0.25` · `shakeMaxRotAmp 0.12 → 0.9` 로 천장을 올려 해결 —
경위는 `16_shake_channel_independence.md` 의 「후기」 참조. **유닛 세기를 먼저 의심하지 말 것**:
천장이 낮으면 유닛 값을 아무리 올려도 1.0 에서 막힌다.
