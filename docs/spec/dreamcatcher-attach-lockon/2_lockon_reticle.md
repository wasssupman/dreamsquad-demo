# 2 · Lock-On Reticle (레이어 C)

## 목적

화살표 팁 최근접 **유효** 유닛에 리티클 단 하나를 스냅해 **위치/락**을 표시한다(정체는 콜아웃 unit 3 담당). full(3/3) 유닛엔 invalid 폼으로 무효를 **형태로** 알린다. 지금의 유닛 전체 빨강 RGB 틴트(밀집 때 안 통함·밝힘 불가)를 제거하고 리티클로 대체.

## 변경 대상

- **신규** `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusReticle.cs` — 리티클 프레젠터(애셋-프리 UI 쿼드, `DreamcatcherTargetArrow` 기법 준용)
- `DreamcatcherHandView.cs` — 리티클 `Create`·소유
- `DreamcatcherCardDragSlot.cs` — `UpdateUnitHover`에서 락온 유닛 산출(+히스테리시스) → 리티클 `LockOn`/`Release`
- `SpineUnitView.SetHoverHighlight` 호출부 — **전체 빨강 틴트 제거**

## 구현

- **리티클 = 4코너 브래킷 + 가는 링**(오버레이 최상위, 콜아웃 아래). `focusConfig` 색·두께·`reticlePadding`. 크기는 `reticleMinScreenSize` 로 **손끝 반경 초과** 보장(occlusion 중 코너 노출, 계약 #7).
- `LockOn(Rect rect, bool valid)`: 위치·크기를 유닛 렉트+padding 으로 잡고 **스냅 스프링**(`reticleSnapSpring`/`Damp`, `unscaledDeltaTime`)으로 이징, 첫 락온에 `reticlePopScale` 팝. `valid=false`(full)면 **invalid 폼**(`reticleInvalidColor` + 코너 틈 벌어짐/X) 로 그린다.
- `Release()`: 페이드아웃·축소 후 숨김. 항상 최대 1개.
- **정체 히스테리시스(계약 #4, UX H2)**: `UpdateUnitHover` 에서 매프레임 최근접 pick 을 그대로 쓰지 말고, **현재 락온 entity 를 우선 유지**하고 새 후보가 `lockSwitchHysteresisPx` 이상 우세할 때만 전환. 위치 스프링과 **별개**(위치는 부드럽게, 정체는 래칭).
- `UpdateUnitHover`: `bridge.TryPickDefenderAtScreen` → 히스테리시스 통과 entity 확정 → `SpineUnitView.TryGetScreenRect` 렉트 → attachable 여부(unit 5 스냅샷)로 `reticle.LockOn(rect, valid)`, 없으면 `Release()`. 부착 조준 모드(Defender)에서만.
- **빨강 틴트 제거(계약 #6)**: `SetDefenderHoverHighlight(entity, true, UnitHoverTint)` 의 전체 빨강 repaint 를 제거. Spine R/G/B 곱셈 틴트론 밝힘 불가하고 소비자가 이 경로뿐이라 안전. 락온 신호는 리티클+콜아웃.

## 완료 기준

- 밀집 배치에서 락온 리티클이 유닛 위치에 붙고 화면에 항상 최대 1개. 유닛 사이를 훑을 때 **정체가 홱홱 안 바뀜**(히스테리시스), 위치는 스프링으로 매끄럽게.
- full(3/3) 유닛엔 invalid 폼(색+형태). 유닛에서 벗어나면 해제.
- 전체 빨강 틴트가 사라지고 리티클과 이중 표시 안 됨. edge(화면 끝) 유닛도 클램프/누락 처리.
- `Close`/`ForceClose`/`OnDisable`/`OnPhaseChanged` 에서 하드 클리어(계약 #10). 콘솔 클린.
