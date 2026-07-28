# 1 · 최종 구현 — 손가락=고리 + 보드 스프링 follow + 안정 하이라이트

## 목적

드래그 배치 프리뷰 키링: 고리=손가락(공중), 유닛=보드에 서서 무게추처럼 스프링으로 뒤따라오며 흔들림, 배치 하이라이트=마우스 고정.

## 변경 대상

- `Assets/_Project/Scripts/Data/DragSwaySettings.cs`
- `Assets/_Project/Data/Config/DragSwaySettings.asset`
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

### DragSwaySettings 파라미터

`ropeLength`(2.0, 고리 아래 매달리는 길이) · `maxAngle`(8, 유닛 기울임/흔들림 각) · `spring`(100, 추종 강성/탄성) · `damping`(2.5, 감쇠·바운스) · `maxSpeed`(12, 추종 속도상한→빠른 스와이프 튐 방지, 0=무제한) · `cordWidth`(0.14, sub-pixel 이면 컬링) · `cordColor` · `ringRadius`(0.18) · `charmDrop`(0, 자동정렬 위 미세조정).

### 계층 (`TryBuildKeyringPreview`)

`root`(scale 1) 아래:
- `ring`: 로컬 원 LineRenderer 루프 + `Billboard(Tilted)`, 공유 머티리얼.
- `cordLine`: `LineRenderer(useWorldSpace, positionCount=2)`, `widthMultiplier=cordWidth*scale`.
- `endNode`(`Billboard`) → `swingPivot` → `spineChild`(SkeletonAnimation, scale). 머리를 endNode 에 `localBounds` 자동정렬(`spineChild.localPosition = -center.x*scale, -max.y*scale`). `unitHeight = localBounds.size.y*scale` 저장.
공유 머티리얼 1개(`Sprites/Default`), `OnDestroy` 파괴.

### 배치 (`TryComputeRingUnit`, camUp 기반)

손가락 ray 와 `boardPlane` 교차점 `pBoard`. **수직 오프셋은 camUp(화면 세로)** — 고리는 손가락 ray 위, 발은 고리보다 camUp*totalDrop 아래이면서 보드평면 위가 되도록 s 를 ray-plane 으로 해:
```
totalDrop = unitHeight + ropeLength*visualScale;
s = -(Dot(N, camPos - camUp*totalDrop) + boardPlane.distance) / Dot(N, rayDir);
ringW = camPos + rayDir*s;               // 고리 = 손가락 위
feet  = ringW - camUp*totalDrop;         // 보드 위
unitTargetW = feet + N*previewHeight;
```
(월드-up 으로 올리면 기울어진 카메라에서 화면상 안 올라가 고리·유닛 겹침.)

### `Update()` (`unscaledDeltaTime`)

```
// 무게추 스프링 + 속도상한(탄성 유지, 초기 튐만 제한). 워밍업 금지.
accel = (target - pos)*spring - vel*damping; vel += accel*dt;
if (vel.magnitude > maxSpeed) vel *= maxSpeed/vel.magnitude;
pos += vel*dt;                                   // pos=_unitPosWorld(스윙), target=_unitTargetWorld

ring.position = _ringWorld;                       // 고리=손가락(UpdateDrag 에서 설정)
head = _unitPosWorld + camUp*unitHeight; endNode.position = head;
swingPivot.localRotation Z = clamp(줄 방향각, ±maxAngle);  // 머리 중심 기울임
cordLine: 고리→머리 2점;
UpdateHoverAtTarget();                             // 하이라이트=_fingerBoardWorld(손가락) 칸
```

`UpdateDrag`: `TryComputeRingUnit` 로 `_ringWorld`/`_unitTargetWorld`/`_fingerBoardWorld` 갱신(마우스 즉시 추종). 첫 프레임 `_unitPosWorld=target` 초기화. 오프보드면 preview 숨김.
`UpdateHoverAtTarget`: `BoardSpace.ToSim(_fingerBoardWorld)` → cell → `SetHover`. **스윙하는 `_unitPosWorld` 도, 매달린 `_unitTargetWorld` 도 아님** → 배치 칸이 안정적이면서 손가락에 붙는다(README 계약 3).
`CleanupSession`: `_posInit/_onBoard/_unitVelWorld` 리셋.

## 완료 기준

- [x] compile 클린·콘솔 에러 0.
- [x] 좌표 수학 검증(실카메라): 고리 손가락 0px, 머리 고리보다 화면상 80px 아래, 발 보드Y.
- [x] 스프링 탄성 유지 + 워밍업 스냅 제거 + maxSpeed 튐 제한.
- [x] 하이라이트가 마우스 고정(유닛 스윙 무관) → 배치 정확.
- [ ] 라이브 게임뷰 최종 육안(사용자) — feel 확정.

---
확인: 2026-07-05 사용자 feel 확정("좋다"/"마무리"). 커밋 `08ac035` 외 세션 내 반복 커밋. 라이브 보드 검증은 리컴파일마다 Play 재시작으로 MCP 자동재현 불가 → 좌표 수학은 실카메라+합성 평면으로 검증, feel 은 사용자 라이브 판단.
