# 17b. Prop Scale / Sorting Fix (Hotfix)

## 목적

17 커밋(`a1c7c98`) 이후 사용자 시각 검수에서 **프랍-캐릭터 sorting 회귀**와 **17/18/19 튜닝 실효가 체감되지 않음** 이 보고됐다. 원인 분석 결과 `MapView.InstantiateBackgroundProps` 가 17 에서 `instance.transform.localScale` 을 덮어쓰면서 `PropBillboard.ApplyData` 의 `visualRoot.localScale` 과 **중복 적용**되어 최종 world scale 이 `visualScale² × placement.scale` 로 증폭/축소되고 있다.

이 결과:

- 프랍 bounds 가 의도와 다른 크기로 커지거나 작아짐 → SpriteRenderer 의 y-based sorting 에서 캐릭터와 artifact.
- 프랍 크기가 흐트러져 17/18/19 의 튜닝 결과를 시각적으로 판별할 수 없음.

본 spec 은 **1 줄 수정**으로 root-cause 를 제거한다. 이후 audit 재캡처를 통해 17/18/19 실효를 다시 평가할 수 있는 상태를 복원한다.

## 신규 finding 기록

`docs/spec/board-visualization/audit/VISUAL_AUDIT.md` 에 추가:

```
### V-009: prop visualScale 이중 적용으로 인한 크기/sorting 회귀
- 축: A
- 위치: Forest theme 전반. Play 모드에서 프랍 크기가 PropData.visualScale 의 제곱으로 적용됨.
- 증상: 프랍-캐릭터 간 sorting artifact, 프랍 크기 왜곡으로 17/18/19 튜닝 실효 판별 불가.
- 재현: 커밋 a1c7c98 이후 모든 Play / audit 캡처.
- 심각도: High
- 가설: MapView.InstantiateBackgroundProps 가 root localScale 에 prop.visualScale 을 적용하지만 PropBillboard.ApplyData 가 visualRoot localScale 에도 같은 값을 적용 → 중복.
- 후속 spec: 17b
```

## 전제

- 17 (`a1c7c98`) 완료.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapView.cs` — `InstantiateBackgroundProps` 내 한 줄.
- `docs/spec/board-visualization/audit/VISUAL_AUDIT.md` — V-009 항목 추가 + 해소 상태 기록.

## 구현 가이드

### 수정

`InstantiateBackgroundProps` 안의 scale 적용 한 줄:

```csharp
// before (17 추가):
instance.transform.localScale = Vector3.one * (prop.visualScale * placement.scale);

// after:
instance.transform.localScale = Vector3.one * placement.scale;
```

### 근거

- `PropBillboard.ApplyData` 가 이미:
  ```csharp
  target.localScale = Vector3.one * Mathf.Max(0.01f, data.visualScale);
  ```
  에서 `visualRoot.localScale` 에 `visualScale` 을 적용함. **visualScale 의 source of truth 는 PropBillboard**.
- root transform (`instance.transform`) 은 **배치 jitter 만** 담당: `placement.scale = 1 + uniform(-scaleJitter, +scaleJitter)`.
- 최종 world scale = root × visualRoot = `placement.scale × visualScale` (정상).

### 회귀 확인

- `PropData.visualScale == 1.0` 인 프랍은 변화 없음 (1² = 1).
- `PropData.visualScale == 0.7` 인 프랍은 기존 `0.49 × placement.scale` → 수정 후 `0.7 × placement.scale` 로 복원.
- `PropData.visualScale` 이 1.0 이 아닌 모든 프랍 (Forest 테마 다수 해당) 에서 크기가 눈에 띄게 달라져야 함.

## 완료 기준

- `MapView.InstantiateBackgroundProps` 에서 `prop.visualScale *` 곱이 제거됨.
- Play 모드에서 프랍 크기가 `PropData.visualScale × placement.scale` (jitter 범위 포함) 로 관찰됨.
- 프랍-캐릭터 간 sorting artifact 해소 (같은 y 구간에서 앞뒤 flicker 없음).
- 동일 seed 로 audit 재캡처 → 프랍 크기가 설계값 범위 내.
- `VISUAL_AUDIT.md` 에 V-009 가 **해소 상태로 기록**됨 (심각도 해소 / 커밋 해시 포함).

## 주의

- 본 fix 만으로 **V-001 / V-003 / V-007 은 해소되지 않는다**. 17 의 Poisson 해석 오류, 18 의 asset 품질, V-007 palette 문제는 별도 spec.
- scale source of truth 를 반대로 (root 에 일원화) 바꾸는 선택도 가능하지만, 기존 PropBillboard 가 이미 visualRoot 에 적용하고 있으므로 **기존 경로 유지 + 중복 제거** 가 최소 diff 이자 안전.
- fix 이후 **audit 재캡처가 선행** 되어야 다음 spec 우선순위를 사실 기반으로 다시 정할 수 있다. 캡처 없이 17/18/19 재튜닝 금지.

확인 일자: 2026-04-24 / 커밋 해시: PENDING
