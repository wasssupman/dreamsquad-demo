# 2 — 캐릭터 레이어 틸트

## 목적

캐릭터 레이어(유닛: 적/디펜더, Spine·Quad)를 카메라 틸트 θ 에 맞춰 φ 만큼 세운다.
현재 `tilemapBillboardTilt = 0` 핀을 풀고 XY 보드에 맞는 값/부호로 설정한다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`tilemapBillboardTilt` serialized 필드, 라인 ~82)
- 검증: `Assets/_Project/Scripts/Presentation/Billboard.cs` (unit 0), `SpineUnitView.cs`

## 구현

- `tilemapBillboardTilt` 시작값을 **+35** 로 (≈ θ50×0.7, 양수).
  - 기하: 스프라이트 front normal=−Z. `Rx(φ)` 후 normal=`(0, sinφ, −cosφ)`. full-face=카메라 θ(=50).
    부피감(윗면 노출) 위해 θ보다 얕게 → φ≈θ×0.7. φ=θ면 완전 빌보드(평면), φ=0이면 바닥 카드.
  - 부호/값은 실측 튜닝. "서 있음 vs 누운 카드 vs 완전평면" 은 (카메라 θ) 대비 φ 가 결정.
  - 시작점에서 안 맞으면 ±5° 단위로 조정. 목표 룩: 발은 셀에 고정, 상단이 살짝 보이는 부피감.
- Billboard 가 Spawn 시 이 값을 주입받음(unit 0 계약). 캐릭터는 **단일 레이어 단일 각도**(레이어 분리의 캐릭터쪽).
- Quad 유닛(Swift 등)도 같은 캐릭터 틸트를 받도록(unit 4 에서 Quad→Billboard 수렴 후 일관). 이 단위는 Spine 우선 검증.

> 하드코딩 금지: 값은 serialized 필드에서. 코드 상수로 박지 않는다.
> 정렬 회귀 주의: 틸트는 transform.rotation 만 — `_simWorld` 기반 sortingOrder 불변. 틸트 후 행 정렬 깨지면 보고.

## 완료 기준

- Tilemap Play 스크린샷: 캐릭터가 **서 있게** 보임(바닥 카드 아님), 발이 셀에 접지, 떠 있지 않음.
- 걷는(이동) 유닛이 틸트로 휘청이지 않음(고정 틸트라 안정).
- 좌우 이동 시 ScaleX 반전 정상(틸트와 독립 채널 확인).
- Legacy3D 불변(35° 유지).
- 사용자 시각 확인 후 θ/φ 최종값 확정.
