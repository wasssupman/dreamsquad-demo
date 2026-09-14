# 2a · `SpriteUnitView` — 선다 (뜨고, 걷고, 픽킹된다)

## 목적

`UnitView` 의 두 번째 구현체를 세우고 `TrySpawn` 이 세트 유무로 백엔드를 고르게 한다.
이 단위가 끝나면 세트를 저작한 유닛이 **판에 뜨고, 이동하고, 정렬되고, 체력바·그림자·픽킹이 붙는다.**
공격·사망·반응은 2b — 여기서는 안전한 no-op 로 둔다(컴파일 안전 분할).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/SpriteUnitView.cs`
- 수정 `Presentation/SpriteFlipbookPlayer.cs` — `public float Speed { get; set; } = 1f;` · `Update` 에서 `dt * Speed`
- 수정 `Presentation/SpineUnitPool.cs` — `TrySpawn` 분기

## 구현

**`TrySpawn` 분기** (기존 29행 조기 반환 재구성):
```
var set = visualData?.SpriteMotions;
if (set != null && !set.HasIdle) { Debug.LogWarning($"'{name}': 스프라이트 세트에 idle 이 없다 — 쿼드 폴백", set); set = null; }
if (set == null && (visualData == null || visualData.SpineSkeletonDataAsset == null)) return false;
… view = set != null ? (UnitView)go.AddComponent<SpriteUnitView>() : go.AddComponent<SpineUnitView>();
```
`SpriteUnitView.Spawn(visualData, defenderExtras, set, entity, worldPos)` — concrete 호출.

**`Spawn`** — `SpineUnitView.Spawn` 과 같은 순서: `_baseScale`(`SpineVisualScale × CharacterVisualScale`) → 스케일/위치 →
`SpriteRenderer` 추가 + `SpriteFlipbookPlayer` 추가(`timeDomain = Battle`, `playOnEnable = false`) →
`Billboard.Setup(Tilted, CharacterBillboardTilt)` → `BlobShadow.Attach(… 2 × BodyRadiusTiles × TileToWorld …, live:true)` →
`_battleScale` pull → `PlayLocomotion()`. 정렬은 **`_sr.sortingOrder` 직접** — 렌더러가 하나라 스윕이 없고 블롭 제외 가드도 불필요.
`flipX` 초기값 = `set.SheetFacesRight`(리그 규약과 같이 「기본은 왼쪽을 본다」).

**복사하는 멤버** (Spine 참조 0 — 그대로): `UpdatePosition`(이동 측정→로코모션→hop→위치) · `ApplyRenderPosition`(`ToView + SpineVisualOffset + lift`) ·
`ApplyLift`/`ApplyRenderScale`(스케일 단일 지점 — `_baseScale·_flightScale·_punchScale·_squash`) · `SetFlightHeight` · `PlayKnockupHop`/`AdvanceHop`/`CurrentHopOffset` ·
`SetFlightView` · `TryGetScreenRect`(8코너 투영, `_sr.bounds`) · `ApproxWorldHeight`(`_sr.bounds.size.y`) · `UpdateWalkTimeScale` 의 `_moving` 히스테리시스.

**로코모션** — `_moving` 이 바뀌면 `player.Play(set.ResolveLocomotion(_moving))`. 같은 플립북이면 재시작하지 않는다(이름 비교 대신 참조 비교).
걷기 배율: `player.Speed = (_moving && 로코모션 재생 중) ? _walkFactor : 1f` — Spine `ApplyTimeScale` 과 같은 규칙. 배틀 슬로우모는 재생기가 도메인 클럭으로 이미 따른다 → `SetAnimationTimeScale` 은 `_battleScale` 저장만(hop·이동 측정용).

**facing** — `FaceAlongMovement` 가 `UnitFacing.ShouldFlip(dx, FacingRight, immediate:false, ref _accum)`.
`FacingRight => set.SheetFacesRight ? !_sr.flipX : _sr.flipX` · 뒤집기 = `_sr.flipX = (dx >= 0) != set.SheetFacesRight`.

**2b 까지 no-op 인 멤버**: `PlayAttack` · `PlayDeploy`(false) · `Kill`(즉시 `Destroy`) · `FaceToward` · 4 틴트 · `PlayPunch` · `PlayLandingSquash` · `FlashWhite` ·
`ResolveCastAnchor`/`ResolveProjectileLaunchAnchor`(`transform.position`). 컴파일은 통과하고 유닛은 서 있다.

## 완료 기준

- 세트 저작 유닛이 스폰되면 hierarchy 에 `SpineDef_*`/`SpineEnemy_*` 가 `SpriteUnitView` 로 뜨고 idle 이 돈다(빌보드 틸트·블롭 포함).
- 이동 유닛(적)은 walk 시트가 있으면 이동 중 walk, 정지 시 idle · 방향 전환에 `flipX` 가 따라온다.
- 오버헤드 체력바가 붙고, 드림캐쳐 드래그 픽킹(`TryPickDefenderAtScreen`)이 그 유닛을 잡는다 — **브리지 변경 0 으로**.
- 세트 있으나 idle 비면 경고 + 쿼드 폴백. 세트 없는 유닛은 종전과 동일(Spine).
- `SpriteFlipbookPlayerTests` 기존 초록 + `Speed` 2건.
