# 6 — 배경 프랍 거리 기반 틸트

## 목적

퍼스펙티브 카메라(pitch 58°, FOV 40)에서 보드 깊이에 따라 프랍까지의 시선 elevation 이
26.6°~86.6°(실측, 보드 Z[−10.5..+10.5])로 변한다. 모든 배경 프랍이 단일 고정 틸트(45°)면
앞쪽(카메라 밑) 프랍은 누워 보이고(이상 67.5° 대비 −22.5°) 뒤쪽 프랍은 과하게 선다(이상 20.7° 대비 +24.3°).
프랍 위치별 elevation 으로 틸트를 보정해 보드 전역에서 "서 있는 정도"를 일관되게 만든다.

**캐릭터/적은 범위 밖** — 경로 이동 중 거리 변화로 틸트가 휘청이므로 고정 45° 유지(별개 `Billboard` 컴포넌트).

## 변경 대상

- 수정: `Assets/_Project/Scripts/Presentation/BillboardRotation.cs` — 순수 함수 `ResolveDistanceTilt` 추가
- 수정: `Assets/_Project/Scripts/Presentation/PropBillboard.cs` — `Tilted` 경로에서 스폰 시 1회 bake
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — factor/min/max serialized + static mirror (Legacy3D=factor 0)
- 수정: `Assets/_Project/Tests/EditMode/BillboardRotationTests.cs` — `ResolveDistanceTilt` 회귀

## 구현

**틸트 공식 (타입 × 거리):**
```
camPitch      = asin(−camFwd.y) [deg]                       ← 라이브 카메라 pitch (= 기준 elevation)
elev          = atan2((cam − prop).y, horizDist(cam, prop)) [deg]
effectiveTilt = clamp( PropData.tiltAngle + (elev − camPitch) × factor,  min, max )
```
- `PropData.tiltAngle` = 타입 기준각(tree 50/rock 45/flower 38) — 아트 의도 보존.
- `camPitch` = 델타 0 기준. **라이브 카메라 pitch 에서 직접 도출**(=카메라가 보는 보드 중앙 elevation). 별도 refElev 데이터 불필요.
- 중앙(elev=camPitch)에선 델타 0 → 기존 per-type 룩 그대로. 앞/뒤로만 보정.

**라이브 재계산 (카메라가 정적이 아님):** 실측 결과 카메라는 **페이즈마다 pitch 가 바뀐다**(Draft 40° ↔
Battle 58°, 같은 X/Y 피벗에서 dolly+pitch). 따라서 스폰 1회 bake 는 한 페이즈 기준으로 구워져 다른
페이즈에서 어긋난다 → **매 `LateUpdate` 라이브 재계산.** camPitch 를 라이브로 도출하므로 페이즈마다
자기보정(중앙 프랍 항상 ≈base). 프랍은 안 움직여 휘청 없음(카메라 전환 때만 매끄럽게 추종).
비용: 247개 × (asin+atan2)/프레임 = 무시 가능.

**모드:** 새 enum 미추가. 기존 `PropBillboardMode.Tilted` 경로를 거리-인지로 강화한다. 배경 프랍 7종이
이미 전부 Tilted 이고, 캐릭터는 별개 컴포넌트라 영향 없으며, `factor=0`이면 기존 고정 틸트로 폴백.

**튜닝(하드코딩 금지, 보드 전역 1벌):** `BattleBridge` serialized → static mirror.
- `propDistanceTiltFactor` 0.78 (Legacy3D 는 빌드 시 0=비활성)
- `propDistanceTiltMin` 28 / `propDistanceTiltMax` 62 (극단 클램프)
- refElev 는 데이터 아님 — `ResolveDistanceTilt` 가 카메라 pitch 에서 라이브 도출.

> 경계: `ResolveDistanceTilt` 는 camera==null 또는 factor==0 이면 baseTilt 그대로 반환(순수·테스트 가능).
> 캐릭터가 쓰는 `BillboardRotation.Compute(Facing.Tilted, …)` 동작은 불변 — 이 단위는 **각도 산출만** 추가.

## 완료 기준

- compile 통과 + `BillboardRotationTests` 그린(refElev 동치/근접 증가/원거리 감소/클램프/카메라없음 폴백).
- Play 실측: 프랍 effectiveTilt 가 위치별로 분포(근접↑ 원거리↓), 중앙(elev≈camPitch) 프랍 ≈base 유지. 카메라 포즈를 Draft(40°)↔Battle(58°) 로 바꿔도 라이브 자기보정 확인.
- 게임뷰 스크린샷: 앞쪽 프랍 "눕기" / 뒤쪽 "과기립" 완화, 보드 전역 부피감 일관. (배경 스크린샷 육안 검증)
- 캐릭터/적 틸트 불변(45° 고정) — 회귀 없음.

<!-- 완료 확인: 2026-06-29, dba00eb — EditMode 13/13, 라이브 실측(중앙=base @40°/58°, 근접·원거리 클램프), A/B 게임뷰. 사용자 승인. -->

