# 1 — 묵직 임팩트 반응 (타겟 월드에서 발생)

## 목적

카드 고스트가 유닛에 **닿는 순간**(splat 프레임) 유닛/월드가 묵직하게 반응한다:
**Spine 펀치 스케일 + 흰 플래시 + 링 충격파/버스트 + 유닛-로컬 킥 + 미세 카메라 킥 + 찰싹 SFX**.
카드는 사라지고 **유닛(3D)의 반응이 주역**(스티커 인상 제거). ECS 시뮬 변경 0.

## 변경 대상

- **수정** `Presentation/SpineUnitView.cs` — `PlayPunch()`(스케일 펄스) + `FlashWhite()`(흰 틴트 펄스) 신설. base 스케일 캡처.
- **수정** `Presentation/VfxSpawner.cs` — `SpawnCardAbsorb(Vector3 viewPos)` 신설 (**view 좌표 직접**, ToView 안 함).
- **수정** `Bridge/BattleBridge.cs` — 게이트웨이 `TryGetUnitView(Entity, out SpineUnitView)` + `SpawnCardAbsorbVfx(Vector3 viewPos)`.
- **신설** `Presentation/CameraImpactKick.cs` — Camera.main 미세 킥(LateUpdate additive-self-cancel, rig 존중).
- **수정** `Audio/SoundManager.cs` — `[SerializeField] AudioClip cardAbsorbClip` + `PlayCardAbsorb()`.
- **수정** `UI/Dreamcatcher/CardAbsorbFlightPresenter.cs` — `Action<Vector3> onImpact` 콜백, splat 순간 발화.
- **수정** `UI/Dreamcatcher/DreamcatcherHandView.cs` — onImpact 콜백이 반응 choreography 구동(게이트 경유).

## 구현

### ⚠ sim/view 좌표 함정 (load-bearing)
모든 기존 `VfxSpawner.Spawn*` 은 진입부에서 `BoardSpace.ToView` 로 **sim→view 변환**(sim 입력 기대). 그런데
임팩트 위치는 `sv.transform.position` = **이미 view 좌표**. 그대로 넘기면 이중변환([[project_boardspace_drops_sim_y]]).
→ `SpawnCardAbsorb(viewPos)` 는 **ToView 없이** view 위치에 직접 Instantiate.

### 임팩트 VFX = GA `vfx_Hit_Cylinder02` (결정 2026-07-13)
전용 프리팹 슬롯 `VfxSpawner.cardAbsorbPrefab`(+`cardAbsorbScale` 0.6) 신설. **GA(GabrielAguiar)
`vfx_Hit_Cylinder02`** — 황금 수직 에너지 기둥 + 흰 플래시 + 전기 아크/스파크(순수 PS 13개, 스크립트 0 →
스트립 불필요, fire-and-forget Instantiate+Destroy). 오프스크린 렌더로 11종 비교 후 선정([[project_offscreen_render_vfx_verify]]).
- **재사용 배제 이유**: `Rock03`(보라 링) 등은 이미 Meteor 등에서 사용 중(중복 회피). Cylinder02 는 미사용.
- 미할당 시 기존 `placementRing`+`meteorBurst` 폴백(안전). 씬 배선 = `BattleScene` VfxSpawner 슬롯 1개.

### 유닛 반응 (SpineUnitView, self-contained)
- `PlayPunch()`: `transform.localScale` 을 base(스폰 시 캡처)에서 오버슛→base 로 복귀(unscaled 코루틴).
  base 는 스폰 `s` 값. Billboard 는 rotation 소유라 scale 펄스와 독립.
- `FlashWhite()`: skel.RGB 를 흰색(1,1,1)로 세팅 후 **flash 시작 시 캡처한 원색으로 lerp 복귀**(hover/health 틴트
  상태 무관 — 현재 skel.RGB 를 캡처해 되돌리므로 `_savedTint` 로직과 충돌 없음). unscaled.

### 카메라 미세 킥 (rig 존중 — 함정 회피)
타일맵 카메라는 `BattleBridge.ApplyTilemapCameraPreset`(L2990)에서 **리빌드/페이즈 시에만** transform 절대 세팅
(매프레임 아님 — [[project_tilemap_camera_pitch_per_phase]]). `CameraImpactKick` 은 LateUpdate 에서
**직전 프레임 오프셋을 먼저 되돌리고**(self-cancel) rig base 위에 새 오프셋을 additive 로 얹는다 → rig 가 언제
base 를 갱신하든 안 싸움. 킥은 소량(~수 px 상당)·짧게(~0.15s) decay. unscaled.

### 반응 choreography (게이트 경유, 메커닉-소유)
onImpact(worldViewPos) 콜백(View 소유, host 캡처) 순서:
1. `bridge.TryGetUnitView(host, out var v)` → `v.PlayPunch(); v.FlashWhite();`
2. `bridge.SpawnCardAbsorbVfx(worldViewPos)` (링+버스트).
3. `SoundManager.Instance.PlayCardAbsorb();`
4. `EnsureCameraKick(MainCamera).Kick(strength);`
VFX/사운드/킥은 카드 메커닉이 선언·구동([[feedback_mechanic_vfx_owned_by_mechanic]]). StatusFx 분기 없음.

### presenter 훅
`FlyRoutine` 도착(splat 진입) 직전 `onImpact?.Invoke(lastWorldPos)` 1회. lastWorldPos = 마지막 provider 값(view 월드).

## 완료 기준

- [ ] compile 클린, 콘솔 에러 0.
- [ ] Play: 부착 성공 시 유닛이 **펀치+흰 플래시**, 발밑 **링/버스트**, 화면 **미세 킥**, **찰싹 SFX**(클립 할당 시).
- [ ] 링/버스트가 유닛 발밑(view 위치)에 정확히(이중변환으로 어긋나지 않음).
- [ ] 카메라 킥이 페이즈 pitch 와 안 싸움(킥 후 카메라 원위치 복귀, 페이즈 전환해도 정상).
- [ ] 취소/실패 시 반응 전무(비용 0 계약).
- [ ] ECS 시뮬 변경 0. `SoundManager.cardAbsorbClip` 미할당 시 무음(가드) — 씬 배선은 Inspector 클립 1개.

---
**확인 2026-07-13**: compile 클린. 사용자 Play 검증 통과(펀치/플래시/GA Cylinder02 임팩트/카메라 킥, 느낌 승인).
VFX 오프스크린 11종 비교 후 Cylinder02 선정. 커밋: units 1+2 통합.
