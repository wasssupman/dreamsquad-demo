# Keyring 연출 이식 가이드

> keyring-unify (2026-07-08) 산출물. 다른 프로젝트에서 키링 드래그 연출을 재구축할 때 읽는 지도.
> 코드 모듈이 아니라 지식 이식이 목적 — 파일 몇 개 + 아래 함정 목록이 전부다.

## 1. 동작 모델 계약

- **고리 = 손가락/포인터 위치(즉시), 매달린 대상 = 스프링 지연 추종.** 목표점 = 고리에서 줄 길이만큼 아래.
- **스프링**: `accel = (target-pos)*spring - vel*damping`, `maxSpeed` 속도상한(빠른 스와이프 튐 방지, 탄성은 유지). **워밍업(가속 램프) 금지** — 억제 후 풀릴 때 큰 탄성 스냅.
- **기울임 = 줄 방향에서 유도** (`LeanAngle`: -atan2(x,y) 클램프 ±maxAngle), **회전 중심은 머리** — 발/중심 피벗이면 반대로 흔들린다.
- **게임 판정(배치 칸 등)은 흔들리는 위치가 아니라 포인터 아래 안정 목표로** — 연출과 정확도 분리.
- **낙하** = 중력 적분 + 착지 반동(`FallStep`), 반동 임계 미만이면 정착.

## 2. 가져갈 파일

| 파일 | 역할 |
|---|---|
| `Assets/_Project/Scripts/UI/KeyringSim.cs` | 순수 수학(스프링/기울임/낙하). 의존성 UnityEngine.Mathf 뿐 |
| `Assets/_Project/Scripts/Data/KeyringStyle.cs` | 스타일 SO (스프라이트 2 + UI/월드 머티리얼 4슬롯) |
| `Assets/_Project/Shaders/KeyringHologramCommon.hlsl` | 홀로 효과 함수 (CG/HLSL 양쪽 컴파일) |
| `Assets/_Project/Shaders/UICordHologram.shader` | UGUI 판 (스텐실/클립 골격 + 가산) |
| `Assets/_Project/Shaders/WorldCordHologram.shader` | 월드 판 (URP unlit 가산, `_LengthAxis`) |
| `Assets/_Project/Sprites/Keyring/*` + `Art/Keyring*.mat` | 홀로/로프 텍스처·머티리얼 |
| `Assets/_Project/Tests/EditMode/KeyringSimTests.cs` | 수학 회귀 고정 (전사 레퍼런스 포함) |

## 3. 컨텍스트 접점 (새로 쓰는 것)

새 프로젝트에서 작성할 것은 **좌표 산출과 세션 관리뿐**: 포인터→로컬/월드 변환, 드래그 시작/갱신/종료/취소, 리그(고리/줄 렌더러) 생성·파괴. 레퍼런스 구현 2종 —

- **월드형**: `DefenderDragPlacementController.cs` (ray→보드 평면, LineRenderer 줄 + SpriteRenderer 링 + Billboard, sortingOrder)
- **UGUI형**: `LobbyKeyringDrag.cs` (캔버스 px, Image 리그, 낙하/재잡기 상태머신, suspend/resume 접점)

## 4. 함정 목록 (심각도 순 — 전부 실제로 겪음)

1. **수직 분리는 카메라-up(화면 세로) 기준.** 월드-up 으로 올리면 기울어진 카메라에서 화면상 안 올라가 고리·유닛이 겹친다.
2. **월드 셰이더는 텍스처 샘플 uv 도 전치해야 한다.** 효과 축(lenUv)만 바꾸고 샘플을 안 바꾸면 빔 폭 프로파일이 길이로 늘어져 어둡게 뭉개진다 (unify unit 2 에서 실제 적발). `_LengthAxis`: LineRenderer(u=길이)=1, SpriteRenderer/UI(v=길이)=0.
3. **홀로/스타일 셰이더는 vertex color 를 곱한다.** LineRenderer startColor/Image.color 에 틴트색이 남아 있으면 그라데이션이 오염 — 스타일 적용 시 white 강제, 틴트는 절차적 폴백 전용.
4. **공유 include 는 순수 float + t 파라미터.** CG(fixed, UnityCG)↔URP(HLSL) 헤더/타입 비호환 — `_Time` 직접 참조 금지, 헤더 include 금지.
5. **워밍업 금지 / 튐은 maxSpeed 로.** (모델 계약 1 참조 — 여러 모델 폐기 끝에 확정.)
6. **줄 폭이 sub-pixel 이면 렌더 컬링** — 안 보이는 원인 1순위. 홀로 빔 텍스처는 글로우 여백을 포함하므로 단색 줄보다 폭을 크게(이 프로젝트: 0.14→0.3).
7. **줄 끝은 rect 상단이 아니라 머리 안쪽(cordAttachDrop)** — 투명 여백 위에 뜨는 것 방지 (UGUI).
8. **가산 블렌드는 밝은 배경에서 파스텔로 washout** — 도입 시 밝은 배경 오프스크린 렌더로 먼저 판정. 중간톤까지는 선명, `_Intensity` 여유(≤4)로 대응.
9. **시간 구동 효과의 회귀 검증은 same-frame A/B.** 전/후 별도 캡처 diff 는 `_Time` 때문에 무의미 — 구 셰이더 사본과 같은 프레임에 나란히 렌더해 diff.
10. **스타일 미할당 폴백도 "정상 동작"이라 이전 실패가 침묵한다** — 마이그레이션 후 실제 스타일이 렌더되는지(절차적 폴백이 아닌지) 확인할 것.
11. **텍스처 wrap=Clamp 전제** — 글리치 uv 오프셋이 0..1 을 벗어난다.
