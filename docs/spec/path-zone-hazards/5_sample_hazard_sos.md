# Sample Hazard SO Assets

**작업 구분**: 5

## 목적

3 sample HazardSO asset 작성 — 디버그 spawn (Unit 7) 의 선택지 + game feel 검증 입력. 본 spec 의 *game feel 검증 질문* 답 핵심.

## 변경 대상

- Add: `Assets/_Project/Data/Hazards/Hazard_Poison_3x3.asset`
- Add: `Assets/_Project/Data/Hazards/Hazard_Ice_3x3.asset`
- Add: `Assets/_Project/Data/Hazards/Hazard_Fire_3x3.asset`
- Add: `Assets/_Project/Prefabs/HazardVisuals/HazardVisual_Poison.prefab`
- Add: `Assets/_Project/Prefabs/HazardVisuals/HazardVisual_Ice.prefab`
- Add: `Assets/_Project/Prefabs/HazardVisuals/HazardVisual_Fire.prefab`

## 3 SO 파라미터 (제안값, 작가 튜닝 대상)

### Hazard_Poison_3x3
```
shape = Square3x3
radius = 1            (Square3x3 에서는 사용 안 함이지만 일관성)
lifetime = 6.0s
visualPrefab = HazardVisual_Poison
effects = [
    { kind=DoT, param1=10, param2=0, restDuration=0.2 }    // 10 dmg/sec
]
```

### Hazard_Ice_3x3
```
shape = Square3x3
radius = 1
lifetime = 6.0s
visualPrefab = HazardVisual_Ice
effects = [
    { kind=Slow, param1=0.4, param2=0, restDuration=0.2 }  // 속도 0.4×
]
```

### Hazard_Fire_3x3
```
shape = Square3x3
radius = 1
lifetime = 6.0s
visualPrefab = HazardVisual_Fire
effects = [
    { kind=DoT, param1=20, param2=0, restDuration=0.2 }    // 20 dmg/sec (Poison 의 2배)
]
```

= MVP 화염 = *단순히 더 강한 DoT*. 향후 Burn (잔존 디버프) 추가는 effects 에 entry 한 줄만 더하면 됨 (composition 확장성 검증의 의도된 entry point).

## Placeholder visual prefab (3개)

각각 Cube primitive 또는 Quad 1×1×0.1 (XZ 평면) 에 unlit URP material:

| Prefab | Color | 의미 |
|---|---|---|
| HazardVisual_Poison | #66CC66 (녹색, alpha 0.6) | 독 |
| HazardVisual_Ice | #66CCFF (청색, alpha 0.6) | 얼음 |
| HazardVisual_Fire | #FF6633 (적색, alpha 0.6) | 화염 |

- Y 위치 0.05 (지면에서 살짝 띄움).
- Scale 1×1×1 (HazardPresenter 가 radius 따라 scale 조정).
- 정식 particle/decal 은 후속 (unity-vfx-authoring 스킬).

## 완료 기준

- 3 SO asset 생성, Inspector 에서 의도한 값 확인 (effects 배열 entry 1개씩).
- 3 placeholder prefab 생성 + URP material 연결.
- HazardSO `effects` 배열에 정상 항목 표시.
- 컴파일 + Project view asset 표시 정상.
- 콘솔 에러/경고 0.
