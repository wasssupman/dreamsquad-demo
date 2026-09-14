# 2b · `SpriteUnitView` — 반응한다 (싸우고, 죽고, 움찔한다)

## 목적

2a 의 no-op 를 채운다. 끝나면 스프라이트 유닛이 **공격 모션을 발사 주기에 맞춰 재생하고, 배치 모션이
나오고, 죽으면 death 를 재생한 뒤 사라지고, 펀치·플래시·넉업·착지·호버·저체력·반투명이 Spine 과 같이
반응하고, 투사체·캐스트 VFX 가 저작된 앵커에서 나간다.**

## 변경 대상

- 수정 `Presentation/SpriteUnitView.cs` (2a 의 no-op 12개)

## 구현

**원샷과 복귀** — 재생기에 완료 콜백이 없으므로(그 spec 의 결정) 뷰 `Update` 가 폴링한다:
`_oneShot != null && !player.IsPlaying` → `_oneShot = null; PlayLocomotion();` + `player.Speed` 원복.
`_dying && !player.IsPlaying` → `Destroy(gameObject)`. 프리뷰 뷰 `PollPlayback` 과 같은 형태(전이 최대 1프레임 지연은 의도된 대가).

**`PlayAttack(period)`** — `set.Attack` 없으면 return(Spine 이 트랙 없으면 return 하는 것과 같다).
`player.Play(attack)`; `_oneShot = attack`; 압축 = `player.Speed = Mathf.Max(1f, duration / period)` where `duration = FrameCount / Fps`
(Spine `entry.TimeScale = max(1, Duration/period)` 와 같은 식 · `period <= 0` 이면 1). 무기 궤적: `_weaponTrail?.Play(duration × EndNormalized / Speed)`.

**`PlayDeploy()`** — `_defenderExtras == null` 이면 false(적 가드 유지). `set.ResolveDeploy()` 재생 + `_oneShot`. true.

**`Kill()`** — `_dying = true`; 스케일 슬롯 원복 + 블롭 `SetFlight(1,1)`(Spine 과 같은 이유 — Kill 뒤 UpdatePosition 이 안 온다);
`set.Death == null` → `Destroy`. 아니면 `player.Play(death)`; `Speed = 1`. 완료는 Update 폴링.

**`FaceToward(worldPoint)`** — `ToView(worldPoint).x - transform.position.x` 를 `UnitFacing.ShouldFlip(…, immediate:true)` 로. 공격 모션 재생 중엔 `FaceAlongMovement` 가 막힌다(Spine `IsAttackAnimationPlaying` 게이트 = `_oneShot == set.Attack`).

**틴트 4축** — `Skeleton.GetColor/SetColor` 를 `_sr.color` 로 치환해 **그대로 복사**: `SetHoverHighlight`(RGB 저장/복원, health 흡수) ·
`SetHealthTint`(RGB, hover 중 저장값 흡수) · `FlashWhite`/`FlashRoutine`(연발 가드 `_flashActive`/`_flashRestore` 포함) ·
`SetDimmed`(A + `_blob.SetDimAlpha`; 실그림자 스윕은 없음). `_dying` 가드 동일.

**스케일 반응** — `PlayPunch`/`PunchRoutine` · `PlayLandingSquash`/`SquashRoutine` 그대로 복사(unscaled 시계, 슬롯 쓰기).

**앵커** — `ResolveCastAnchor`: `_defenderExtras == null → transform.position`; 본 추적 분기는 없다; `off = SpineCastAnchorLocalOffset; if (FacingRight) off.x = -off.x; return transform.TransformPoint(off)`.
⚠ 부호: Spine 은 `ScaleX < 0`(=오른쪽 봄)일 때 반전한다 — 같은 조건이 `FacingRight` 다. `ResolveProjectileLaunchAnchor`: 적이면 `_sr.bounds.center`, 아니면 `ResolveCastAnchor()`.

**무기 궤적** — `SpineWeaponTrailPrefab` 이 있으면 `Instantiate(prefab, transform)` + `Bind(null)`(구조물 경로).

## 완료 기준

- 스크립트 배틀(고정 스텝 하네스)에서 세트 저작 방어유닛이 공격 사건마다 attack 플립북을 재생하고 발사 주기보다 길면 압축된다(`Speed > 1` 로그).
- 사망 시 death 재생 후 GameObject 파괴, death 미저작이면 즉시 파괴.
- 드림캐쳐 부착 발동 시 펀치+플래시, 넉업 시 hop, 재배치 착지 시 스쿼시 — **브리지 변경 0** 으로 발화(기존 seam 이 베이스 멤버를 부른다).
- 투사체가 `castAnchorLocalOffset` 에서 나가고 좌우 반전 시 x 가 뒤집힌다.
- EditMode 전체 초록(2a 총계 유지).

---

2026-09-15 구현 · `9bc5e83b` — 컴파일 0 에러. Play 확인은 unit 4 로.
