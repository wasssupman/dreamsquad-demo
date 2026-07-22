# 6 — Handoff Summary

> 에디터 범위 완료 2026-07-21. unit 4(실기 QA)만 남음.

## Commit

| 해시 | 내용 |
|---|---|
| `ce024037` | spec (3-lane 리뷰 33건 중 31건 반영) |
| `17a17bba` | unit 0 — config 치수 계약 + `CostWellMath` |
| `cb50c4a9` | unit 1 — 코스트 셀을 트레이 안으로 (레일 제거) |
| `b5a0e6d5` | unit 2 — 물통 연출 + 코스트 변동 판정 |
| `fc9874e0` | unit 3 — 슬롯 가독성 (배지 제거·코스트 확대·흰 이름) |
| `4382c2f8` | fix — 물통이 "늘어난 스프라이트"로 보이던 문제 |
| `951d998d` | fix — 코스트 값 중앙 정렬 + ⚡ 제거 + 시인성 강화 |
| `2be173d7` | unit 5 — 물통 액체 셰이더 |
| `da316d4a` | fix — Mask 가 셰이더 프로퍼티를 삼켜 물통이 정지하던 문제 |
| `a45e0fdb` | style — 하늘색 팔레트 · 모서리 틈 제거 · 슬롯 코스트 색 통일 |

## Implemented

- 코스트 레일(별도 캔버스)을 없애고 트레이 좌측 셀로 흡수. 통합 박스 하나가 됐다.
- 세그먼트 바 10칸 → **물통 1개**. `CostRuntime.Current` 의 소수부가 곧 수위다.
- 액체 셰이더 `Wassup/UI/CostWell` — 수면 파형(사인 2개 합) · 깊이 음영 · 유리 반사 · SDF 라운드 코너.
- 폴링 판정 — sentinel · epsilon · 복합 변동 계약으로 오발 차단.
- 클래스 배지 제거 + 이름 밴드 클래스 틴트로 이관(튜토리얼 앵커 보존).
- 슬롯 코스트 40pt · 유닛명 흰 텍스트 · 슬롯 확대.
- 트레이 폭을 슬롯 수에서 유도 + `SafeArea − 640` 클램프.

## Key Files

- `Assets/_Project/Scripts/UI/CostDisplay.cs` — 셀 소유자. 물통·값·연출 전부
- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — 트레이 레이아웃·폭 산출·슬롯
- `Assets/_Project/Shaders/CostWell_UI.shader`
- `Assets/_Project/Data/Materials/CostWellLiquid.mat` — 튜닝 값은 여기
- `Assets/_Project/Data/Config/BattleHudTrayConfig.asset` — 치수·색·머티리얼 참조
- `Assets/_Project/Scripts/UI/CostWellMath.cs` + `Tests/EditMode/CostWellMathTests.cs`

## Verified

- EditMode 1151 통과 (실패 0). `CostWellMathTests` 14개 포함.
- Play 실측: 트레이 1280×158(=1920−640 클램프) · 셀/슬롯 폭 동일 · 배치 3회 재진입에도 셀 생존 · 억제 시 리플로우 없음 · 코너 위젯 겹침 0.
- 물통: max 에서 fill 1.0 · 소비 시 소수부 유지 · `AddCost(2)` → `+2` · sentinel 오발 없음.
- 애니메이션: 코스트 고정 상태에서 시간차 2프레임 픽셀 차이 **227,917**(수정 전 0).
- 사용자 Play 확인 완료.

## Notes — 되돌리면 안 되는 것

- **`Mask` 를 쓰지 말 것.** `IMaterialModifier` 라 스텐실 머티리얼 복사본을 만들고, 원본에 쓴 `_Fill` 이 렌더에 전파되지 않아 물통이 통째로 정지한다. 라운드는 셰이더 SDF 가 그린다.
- **`childForceExpandWidth = false`.** true 면 uGUI 가 `flexibleWidth` 를 `Max(flexible,1)` 로 덮어써 셀 고정폭이 무효화된다.
- **`SlotContainer` 분리 유지.** `RebuildSlots` 가 자식을 전부 Destroy 하므로 셀과 같은 부모에 두면 배치 진입마다 조용히 파괴된다.
- **억제는 `CanvasGroup.alpha`.** `SetActive` 는 레이아웃에서 빠져 슬롯 리플로우를 만든다.
- **`_prevInt = -1` sentinel · `FillEpsilon`.** 없으면 배치 진입마다 MaxBurst 오발 / `AddCost(1)` 의 10% 오분류.
- **코너 반경은 `WellCornerRadius` 하나를 공유.** 배경·테두리·셰이더 클립이 어긋나면 모서리에 어두운 틈이 생긴다.
- **`Shader.Find` 금지.** 머티리얼은 config 에셋 참조로 넘긴다(빌드 스트리핑 사고 이력).
- 각성 게이지와 **모션 어휘를 의도적으로 갈랐다** — MaxIdle 펄스·큰 바운스·halo·sheen 은 각성 전용. 두 리소스가 Battle 에 동시에 뜨는데 동작 규칙이 정반대다(각성=보유량, 물통=진행률).

## Follow-up

- **unit 4 실기 QA** — 20:9 실기기에서 ①슬롯 확대 체감 ②엄지 그립에 코스트 셀이 가리는지 ③`Screen.safeArea` 로그 ④셰이더 성능.
- **물통 학습 리스크** — `10/10` 에서 1코스트만 써도 물통이 가득→빔. 리뷰가 CRITICAL 로 지적했고 사용자가 소수부 원안을 유지하기로 했다. 실기에서 오독되면 대안은 "보유량 + 눈금 10칸"(unit 5 셰이더에 눈금 추가로 구현 가능).
- **에너지 기호 0개** — 슬롯 볼트와 셀 아이콘을 모두 뺐다. "이 숫자가 무슨 자원인가"를 말하는 게 화면에 없다. 필요해지면 물통 안 워터마크(중앙 정렬 무해).
- **드림캐쳐 손패 폭 정합** — 트레이가 1280 이 되면서 손패(980)와 344 어긋난다. 카드 크기가 `DreamcatcherHandView.cs:973` 에 하드코딩이라 별도 작업 단위 필요.
- **코스트 사운드·햅틱** — 정수 도달 틱. 검토했고 이번 범위에서 뺐다.
