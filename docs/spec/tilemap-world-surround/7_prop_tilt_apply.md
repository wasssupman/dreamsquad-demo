# 7 — 프랍 Tilted 전환 (4b 적용)

## 목적

후속 후보 4(프랍 틸트 휴면 처리)를 **적용**으로 해소한다. 현재 프랍 7종은 `billboardMode=FullCamera`
(카메라 정면 추종) + `tiltAngle=0` 이라, 캐릭터(`Billboard.Tilted`, φ=45°)와 다른 빌보드 모델로 렌더된다.
이 때문에 보드 위 근경 프랍과 외곽 침엽수림이 바닥에 입체적으로 서지 못하고 카메라 정면 평면 카드("벽")로
늘어서 **원근감이 죽는다**. 프랍을 캐릭터와 같은 발-기준 Tilted 로 세워 룩을 통일하고 깊이를 살린다.

검증 질문: *프랍이 캐릭터와 같은 발-기준 틸트로 서서, 근경 프랍과 원경 침엽수림이 바닥에 입체적으로
이어져 보이는가? (플레이 영역 가독성 유지)*

## 변경 대상

- `Assets/_Project/Data/Theme/forest/prop_flower_{p,w,y}.asset` — `billboardMode: 0→Tilted`, `tiltAngle: 38`
- `Assets/_Project/Data/Theme/forest/prop_rock_{s,m,l}.asset` — `billboardMode: Tilted`, `tiltAngle: 45`
- `Assets/_Project/Data/Theme/forest/prop_tree.asset` — `billboardMode: Tilted`, `tiltAngle: 50`
- (조건부) `Assets/_Project/Scripts/Presentation/PropBillboard.cs` — Tilted 반전 시에만 `flip180` 추가

`PropBillboardMode` enum 값: `FullCamera=0, YAxis=1, None=2, Tilted=3`(0_prop_tilt_foundation 에서 끝에 append).

## 구현

- PropBillboard 의 `Tilted` 경로(`target.rotation = Quaternion.Euler(tiltAngle,0,0)`)는 이미 존재 →
  **코드 변경 없이 SO 값만** 바꾼다. `ApplyData`/`Configure` 가 `data.billboardMode` 를 읽으므로 자동 반영.
- 7종 PropData 의 `billboardMode`/`tiltAngle` 을 표대로 설정. 근경(`InstantiateBackgroundProps`)·
  원경(`InstantiateRingProps`)이 같은 PropData 공유 → 양쪽에 동시 적용(handoff 계약: 같은 에셋 근/원경 공용).
- 캐릭터 기준 φ=45°(pitch 52×≈0.85). 프랍은 키별 차등: 풀 38(덜 세움)·돌 45·나무 50(더 세움).
- 시작값이며 Play 스크린샷 보며 ±5° 미세 튜닝 허용(하드코딩 아님, per-SO).

## 검증 리스크 (구현 중 확인)

1. **스프라이트 반전**: Tilted `Euler(φ,0,0)` 적용 시 앞/뒤 또는 상/하 반전 가능. 반전되면 `PropBillboard`
   에 `Billboard.cs` 의 `flip180`(`rot *= Euler(0,180,0)`) 패턴을 추가하거나 φ 부호를 조정. 이 경우에만 코드 1줄.
2. **그림자 CAST**: 근경 `shadowCastingMode=TwoSided` 실루엣이 Tilted 후에도 정상인지.
3. **접지(발 피벗)**: `visualOffset.y` 그라운딩이 틸트 후 어긋나지 않는지(피벗=transform 원점=셀 위치라 불변 기대).

## 완료 기준

- Play 게임뷰 스크린샷(전/후 비교): ① 프랍이 캐릭터처럼 바닥에 입체적으로 섬 ② 원경 침엽수림에 깊이감
  ③ magenta/반전/접지 어긋남 없음 ④ 플레이 영역(Walk+Place) 가독성 유지.
- `read_console` CS 에러 0(코드 변경 시).
- forest 만 대상. Legacy3D·캐릭터 빌보드 무영향.
