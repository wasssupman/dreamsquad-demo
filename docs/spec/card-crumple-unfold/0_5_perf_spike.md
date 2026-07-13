# 0.5 — 실기 perf spike (결과)

## 목적

서브디바이드 해상도와 `_Unfold` 전송 방식을 unit 1 셰이더 착수 **전에** 확정한다.

## 측정 (editor CPU, `UiCardFaceMesh.OnPopulateMesh` 반복 호출)

| 해상도 | verts/card | tris/card | rebuild/회 | ×5장 |
|---|---|---|---|---|
| 8×8 | 81 | 128 | 0.010ms | 0.05ms |
| 16×16 | 289 | 512 | 0.031ms | 0.15ms |
| 24×24 | 625 | 1152 | 0.064ms | 0.32ms |

## 결론

1. **메시/버텍스 비용 = 무시 가능**. 24×24×5장 풀 rebuild 도 0.32ms(editor). 그리고 rebuild 는 **layout 변경
   시에만**(매프레임 아님). → **해상도는 성능이 아니라 시각 품질로 결정**(부드러운 크리스 + ②-A 곡률). 기본 12~16,
   24 까지 여유. 실기 CPU 5~10× 느려도 정적 메시라 무해.
2. **`_Unfold` 전송 = per-instance 머티리얼 float** (rev1 계약의 "버텍스 스트림" 정정).
   - 애니메이션되는 per-card 스칼라를 버텍스 스트림에 실으면 **매프레임 mesh 재방출 = 병목**(0.32ms/프레임 ×실기배수).
   - 머티리얼 float 로 두면 **메시 정적 + 프레임당 셰이더 uniform 1개**, CPU 0.
   - **크럼플 target/크리스 데이터는 정적 버텍스 스트림(UV1/UV2)에 1회 베이크**(mesh 빌드 시). 셰이더:
     `pos = flat + (1−_Unfold)·crumpleOffset`.
   - per-instance 머티리얼 5장(5 draw call) — UI 5 draw call 은 무시 가능, `OnDestroy` 에서 `Destroy(material)`
     정리(`DraftCardVfxDriver` 선례 동일).
3. **미해결 = 프래그먼트 셰이더 GPU 비용**(가짜 크리스 AO/이터레이션). editor 로 측정 불가 → **unit 1 에서 Android
   실기 프로파일**이 진짜 게이트. 무거우면 크리스 이터레이션/노이즈 옥타브 축소 또는 off 폴백.

## 완료 기준

- 해상도 = 시각 결정(성능 비병목) 확인 ✓
- `_Unfold` 전송 = per-instance 머티리얼 float + 정적 vertex 스트림 확정 ✓
- 실기 GPU 프로파일은 unit 1 로 이관(명시) ✓
