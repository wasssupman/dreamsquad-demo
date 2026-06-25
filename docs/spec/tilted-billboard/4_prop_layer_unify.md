# 4 — Quad·Prop 수렴 + 프랍 레이어 per-data 틸트

> 상태: **4a(Quad 수렴) 완료** · 4b(프랍 per-data 틸트)는 프랍이 Tilemap 모드에 도입된 뒤로 이관(아래).

## 목적

빌보드를 단일 `Billboard` 로 수렴 완료하고, **배경 프랍 레이어**에 캐릭터와 **독립된 틸트각**을
부여한다(구조적 레이어 분리 요구). 풀/나무/구조물이 서로 다른 각도를 가질 수 있다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` (셰이더 페이싱 → `Billboard` transform 틸트)
- 수정: `Assets/_Project/Scripts/Presentation/PropBillboard.cs` (회전 로직 → `Billboard` 위임 또는 흡수)
- 수정: `Assets/_Project/Scripts/Data/PropData.cs` (`tiltAngle` 필드 추가; 기존 `billboardMode` 와 함께)
- 검증: 프랍 스폰 경로(PropData 사용처)에서 Billboard mode/tiltAngle 주입

## 구현

QuadUnitView:
- 빌보드 셰이더 자동 페이싱 제거(또는 unlit 정적 머티리얼로 교체). `Billboard` 컴포넌트 부착, `mode=Tilted`, `tiltAngle=캐릭터 틸트`(unit 2 와 동일 값) → Spine 과 quad 룩 일관.
- 피벗 주의: Unity 기본 Quad 메시는 center-pivot. 발 기준 틸트가 되려면 메시를 위로 half-height offset 하거나 자식으로 감싸 원점을 발에 맞춘다. (Spine 은 이미 발 원점)

PropBillboard / PropData:
- `PropData.tiltAngle` (float) 추가 — per-prop 틸트각. 풀=작게(예 38), 나무=중간(47), 구조물=크게(52) 감각(데이터값, 실측 튜닝).
- PropBillboard 의 Full/YAxis 분기는 `Billboard` 의 동일 모드로 흡수. `PropBillboardMode` → 통합 `BillboardMode` 매핑(또는 enum 일원화).
- 프랍 Configure 시 `Billboard.mode = data.billboardMode 매핑`, `tiltAngle = data.tiltAngle` 주입.

> 레이어 분리 결과: 캐릭터(유닛)는 `tilemapBillboardTilt` 단일값, 프랍은 `PropData.tiltAngle` per-SO.
> 같은 `Billboard` 컴포넌트가 레이어별로 다른 각을 받는다 — 컴포넌트는 각도 정책을 모른다(주입만).
> 하드코딩 금지: 모든 각도 serialized/SO. 상속 2단계 이내(Billboard 단일 MonoBehaviour).

## 진행 메모 (2026-06-25)

- **4a 완료**: `QuadUnitView` 가 Tilemap 모드에서 셰이더 빌보드(`Billboard_Unlit`, full camera-facing) 대신
  **object-space URP/Unlit + Billboard(Tilted) + 발 피벗 자식**을 사용 → Spine 과 동일 메커니즘.
  Legacy3D 는 기존 셰이더 경로 유지(미변경). Play 검증: quad 5기 모두 Visual 자식(y0.5)/Billboard/rotX45/URP-Unlit/tex OK.
- **4b 이관**: Tilemap 모드엔 아직 배경 프랍이 없다(Legacy environment 가 gating 비어 노출되는 건 별개).
  `PropData.tiltAngle` + PropBillboard 수렴은 **terrain/배경-프랍 신규 spec** 에서 프랍 도입과 함께 진행.

## 완료 기준

- compile 통과, 콘솔 에러 없음.
- Tilemap Play 스크린샷: 캐릭터·Quad유닛(Swift)·배경 프랍이 모두 일관된 틸트 룩.
- 프랍 카테고리(풀/나무/구조물)가 서로 다른 각도로 보임(per-data 동작 확인).
- 블롭 그림자(unit 3)가 프랍에도 적용(원하는 프랍 한정).
- Legacy3D 불변. PropBillboard 기존 기능 회귀 없음.
