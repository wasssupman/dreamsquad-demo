# 0 — 프랍 per-data 틸트 토대 (4b)

## 목적

`tilted-billboard/4_prop_layer_unify.md` 에서 이관된 **4b**: 프랍이 캐릭터와 독립한 레이어 각도로
틸트될 수 있게 한다. 근경 프랍을 캐릭터처럼 살짝 세워(틸트) 퍼스펙티브 바닥과 자연스럽게 맞추기 위한
토대. 본 단위는 **데이터 필드 + 빌보드 경로**만 추가한다(컴파일 토대). 실제 적용/그림자 CAST 는 단위 1.

## 변경 대상

- `Assets/_Project/Scripts/Data/PropData.cs` — `float tiltAngle` + `PropBillboardMode.Tilted`
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs` — `Tilted` 정적 회전 경로

## 구현

- `PropData` 에 `public float tiltAngle;`(기본 0) 추가. 캐릭터 `CharacterBillboardTilt` 와 독립.
- `PropBillboardMode` enum 끝에 `Tilted` 추가(기존 FullCamera/YAxis/None 직렬화 보존, 끝에 append).
- `PropBillboard.LateUpdate` 에 `Tilted` 처리: 카메라 불필요, `target.rotation = Quaternion.Euler(tiltAngle, 0, 0)`
  (`Billboard.cs` Tilted 패턴 이식). `target` = visualRoot ?? transform. None 체크 직후, 카메라 획득 전에 분기.
- 정적 틸트라 매 프레임 재적용해도 비용 무시(프랍 소수). 별도 캐싱 불필요.

## 비포함 (다음 단위)

- 근경 프랍 `shadowCastingMode=TwoSided` 적용 → 단위 1(인스턴스화 시점).
- Tilted 프랍 실제 배치/각도 튜닝 → 단위 1.
- `PropBillboard`↔`Billboard` 완전 통합 → 후속 후보(컴포넌트 분리 유지).

## 완료 기준

- compile 성공, `read_console` 에러 0.
- 기존 Legacy3D 프랍 렌더 무변화(billboardMode 미지정 에셋은 FullCamera 유지).
- `PropData` 에셋에 `tiltAngle` 필드 노출, `Tilted` 모드 선택 가능.
