# 10 — 블롭 XZ: 카메라 투영 — **폐기 (2026-08-30)**

> 계측이 요구하지 않았다. 아래 «계측 결과» 가 판정 근거이고, 이 문서는 **읽기 전용 이력**이다.
> 남은 체크박스는 착수하지 않았다는 뜻이지 미완 작업이 아니다.

> **선행 조건**: unit 7~9 완료 후 Play 계측에서 «발끝과 그림자의 XZ 어긋남» 이 실제로 관측될 것.
> 관측되지 않으면 **이 unit 은 폐기한다.** 착수 전에 계측부터 한다 — CLAUDE.md 버그 절차 1·3번.

## 계측 먼저 (이 unit 의 진짜 첫 단계)

Play 중 유닛 1기에 대해 세 값을 같이 찍는다:

| 값 | 얻는 법 |
|---|---|
| transform 원점 | `view.transform.position` |
| 렌더 하단 중심 | `r.bounds.center − Vector3.up * r.bounds.extents.y` |
| 블롭 위치 | `blob.transform.position` |

- 셋의 XZ 가 일치 → **어긋남 없음. unit 은 폐기하고 README 후속 후보로도 남기지 않는다.**
- 하단 중심만 어긋남 → 리그 루트가 발에 없다. 아래 투영이 답이다.
- 블롭만 어긋남 → 원인이 다른 데 있다(앵커·부모 스케일). 이 unit 이 아니라 그 경로를 본다.

계측 결과를 이 문서 하단에 한 줄로 남기고 진행/폐기를 결정한다.

### 계측 결과 (2026-08-28 · MapStage_StreetDay, Play, 유닛 4기)

```
origin  =(4.000, 0.870, 3.500)   ← transform 원점(셀)
bounds0 =(3.616, 0.840, 4.066)   ← MeshRenderer.bounds 하단 중심
blob    =(4.000, 0.896, 3.500)   ← 블롭 (origin XZ + 평면 + lift)
dXZ(origin, blob)   = (0.000, 0.000)
dXZ(origin, bounds) = (−0.384, +0.566)   1칸 유닛 / Malphite 는 (−0.424, +0.683)
bounds.min.y − 평면 = −0.030              ← 발끝은 평면에 붙어 있다(3cm 이내)
```

**판정: 이 unit 은 폐기한다.**

1. **블롭은 이미 원점에 정확히 있다** — `dXZ(origin, blob) = 0`. 고칠 어긋남이 없다.
2. **`bounds` 는 «시각 발끝» 의 대용이 될 수 없다.** 이 unit 의 설계 전제가 바로 그것이었는데
   실측이 뒤집었다. `bounds` 는 월드 **AABB** 라 (a) 45° 틸트가 몸통을 +Z 로 눕혀 center.z 를
   0.57 밀고 (b) 무기·망토가 center.x 를 0.38 밀었다(Malphite 와 값이 다른 이유). 이걸 발끝으로
   믿고 투영했다면 **0.4~0.7타일짜리 오차를 새로 만들었을 것이다.**
3. `bounds.min.y` 가 평면에서 −0.03 이므로 **발끝은 실제로 평면 위에 있다** — 발끝이 평면 위에
   있으면 카메라 투영은 정의상 항등이다. 메커니즘 자체가 할 일이 없다.

**그럼 사용자가 본 «XZ 어긋남» 은 무엇이었나** — 높이 오차가 XZ 오차로 보인 것으로 설명된다.
StreetDay 에서 블롭이 평면보다 0.654 아래에 있었고, 카메라 pitch 55° 에서 그만큼 아래 있는 점은
시선 방향으로 `0.654 / tan(55°) ≈ 0.46타일` 밀려 보인다. **unit 7 이 그 원인을 제거했다.**
육안으로 증상이 남아 있다면 그때 이 unit 을 되살린다 — 단 발끝 추정을 `bounds` 가 아닌
다른 근거(스켈레톤 본 등)로 다시 세워야 한다.

## 목적 (어긋남이 확인된 경우)

블롭 XZ 를 transform 원점이 아니라 **카메라에서 본 시각 발끝의 접지점**으로 푼다.
빌보드의 접지는 화면상의 착시이므로, 화면을 만드는 카메라를 통해 푸는 것이 맞다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/BlobShadowMath.cs` (신규, 순수)
- `Assets/_Project/Scripts/Presentation/BlobShadow.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` · `QuadUnitView.cs`
- `Assets/_Project/Tests/EditMode/BlobShadowMathTests.cs` (신규)

## 구현

```csharp
// 카메라에서 visualBottom 을 지나는 광선이 plane 과 만나는 점.
// 폴백(광선이 평면과 평행 / 카메라가 평면 뒤) = 수직 투영 = unit 7 동작.
public static Vector3 SolveGroundAnchor(Vector3 camPos, Vector3 visualBottom, Plane plane);
```

- `Plane.Raycast(new Ray(camPos, visualBottom - camPos), out float enter)`. `false` 또는 `enter <= 0` → 폴백.
- 추출 근거: 제약 10 하위조항 **(a) 비자명(분기·폴백)**. 호출처는 1곳이지만 (a)(b)(c) 는 OR 이다.
- **발끝은 스폰 후 첫 LateUpdate 에 1회 샘플**해 `transform.InverseTransformPoint` 로 로컬 고정.
  매 프레임 바운즈를 읽으면 팔 드는 애니에 그림자가 출렁인다. 이후 `TransformPoint` 로 재구성.
- 샘플 실패(렌더러 없음) → 로컬 오프셋 0 = unit 7 동작.
- `SetGroundAnchor`(비행)가 걸려 있으면 그 XZ 를 그대로 쓴다 — 아치 기저선은 이미 지면 위 점이다.

## 완료 기준

- [ ] 위 «계측 먼저» 결과가 문서에 기록되고 착수 근거가 된다
- [ ] EditMode 4케이스: 수직 카메라=수직 투영과 동일 / 비스듬=손계산 교점 / 발끝이 평면 위=항등 / 평행·평면 뒤=폴백
- [ ] Play 육안: 어긋났던 유닛의 그림자가 발끝에 붙는다
- [ ] Play 육안 회귀: 안 어긋났던 유닛은 unit 7 결과와 동일
