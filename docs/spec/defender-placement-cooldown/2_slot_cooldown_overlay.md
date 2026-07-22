# 2 — 셀 쿨타임 오버레이 (표현, 액체)

## 목적

쿨타임 중인 슬롯 포트레이트 위에 **빠지는 액체**(코스트 물통 셰이더 재사용) + 중앙 카운트다운을 그리고, 다 빠지면 밝은 팝으로 "재사용 가능"을 알린다. 코스트 물통과 **방향(빠짐↔차오름)·색(탁한 슬레이트↔밝은 재화)·위치(포트레이트↔전용 셀)·숫자(카운트다운↔재화)** 넷으로 구분한다. tick 은 unit 0 런타임(Battle 도메인)이 하므로 여기서는 **읽어서 그리기만** → 슬로모 감속/정지 동결 자동.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `SlotVisual` 확장 + 오버레이 빌드(쿨다운>0 슬롯만) + 프레임 리페인트 + 머티리얼 수명
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs` — 쿨타임 액체 튜닝 필드(하드코딩 금지, 규칙 6)

## 구현

**재사용 구조** = `trayConfig.wellLiquidMaterial`(`Wassup/UI/CostWell`). 셰이더는 `_Fill`(수위 0..1) 하나만 매 프레임, 색은 생성 시 1회(`_LiquidBottom/_LiquidTop/_SurfaceColor`). **⚠️ 셰이더는 액체색의 alpha 를 안 본다**(`col.a = Image.color.a × 수위 × 코너AA`) → 반투명(포트레이트 비침)은 **`Image.color.a`** 로 준다(액체색 alpha 로 하면 무시됨). `_Fill = 남은비율`(1→0)이라 액체가 **아래로 빠진다**(코스트는 차오름 — 반대).

**BattleHudTrayConfig 필드 추가:**
- `Color cooldownLiquidBottom/Top/Surface` — **어두운 슬레이트**(bottom≈0.04, top≈0.09; surface 는 파형 가독용 약간 밝게). 코스트 하늘색·골드와 구분.
- `float cooldownLiquidOpacity`(기본 0.92) — `Image.color.a`. 높을수록 어둡게 덮음.
- `Color cooldownTextColor`(기본 흰색), `float cooldownFontSize`(기본 38)
- `float cooldownReadyPopScale`(기본 1.16), `float cooldownReadyPopDuration`(기본 0.22)
- `Color cooldownCellDim`(기본 어두운 반투명) — **셀 전체 딤 스크림**(이름밴드/칩 포함, 액체와 별개 상시 딤)
- 숫자 가독: 아웃라인 + 언더레이(드롭섀도) — CostDisplay value 레시피 재사용.

**오버레이 3층 구성** (cooldownRoot = 셀 전체 덮개, `SetActive` 토글 하나로 전부 제어):
1. **딤 스크림**(전체) — `cooldownCellDim` 상시 딤 → 이름밴드·칩까지 셀 전체가 "비활성"으로. 액체가 빠져도 유지.
2. **액체**(포트레이트 영역, 이름밴드 위) — `_Fill=남은비율`로 아래로 빠짐.
3. **카운트다운 숫자**(포트레이트 영역 중앙).
셰이더 SDF 종횡비는 **액체 quad(포트레이트 영역)** 기준으로 push.

**SlotVisual 확장**: `GameObject cooldownRoot; Image cooldownFill; TextMeshProUGUI cooldownText; Material cooldownMat;`
- ⚠️ shown 상태를 **struct 필드로 두지 않는다**(critic M1: `List<SlotVisual>` value-copy 라 write-back 없으면 소실). shown 판정 = **`cooldownRoot.activeSelf`**(참조 읽기). 리페인트는 참조형 멤버만 건드리므로 write-back 불요.

**오버레이 빌드 (RebuildSlots, `data.placementCooldown > 0f` 슬롯만)** — 쿨다운 0 유닛은 오버레이/머티리얼을 **아예 안 만든다**("0 = inert", 머티리얼 인스턴스도 절약):
- `cooldownRoot`: 포트레이트 영역 덮는 자식 RectTransform(이름밴드 위), `SetActive(false)`
- `cooldownFill`: `Image`, `sprite = UiRoundedSprite.Make(0,0,white,clear)`, `Type.Simple`, `material = new Material(wellLiquidMaterial){hideFlags=HideAndDontSave}`, 색 1회 push(config cooldown 색), `color = (1,1,1, cooldownLiquidOpacity)`, `raycastTarget=false`
- `cooldownText`: 중앙 TMP(nameFont), outline(액체 위 가독, CostDisplay value 패턴), `raycastTarget=false`
- 머티리얼 인스턴스는 `SlotVisual.cooldownMat` 에 보관. `Shader.PropertyToID` 캐시(`_Fill`/`_Aspect`/`_Radius`)

**리페인트 (Update)** — 위치: 패널활성 가드(`!_panel.activeInHierarchy…` 496행) **아래**, 코스트 조기반환(`current == _lastCostSeen` 499행) **위**(critic m4):
```
UpdateCooldownOverlays():
  rt = GameManager.Instance?.CooldownRuntime
  if (rt == null) { if (_anyCooldownShown) hide all + _anyCooldownShown=false; return }
  if (!rt.AnyActive && !_anyCooldownShown) return          // O(1) 스킵
  bool anyShown = false
  for each v in _slotVisuals:
    if (v.cooldownRoot == null) continue                   // 쿨다운 없는 유닛
    float rem = rt.RemainingFor(v.data)
    bool shown = v.cooldownRoot.activeSelf                 // struct 아님(M1)
    if (rem > 0):
      if (!shown) v.cooldownRoot.SetActive(true)
      v.cooldownMat.SetFloat(_FillId, rt.Fraction(v.data)) // 남은비율=수위 → 빠짐
      push _Aspect(w/h)·_Radius(rect 기준, CostDisplay PushWellGeometry 미러)
      v.cooldownText.text = Mathf.CeilToInt(rem).ToString()
      anyShown = true
    else if (shown):
      v.cooldownRoot.SetActive(false)
      StartCoroutine(ReadyPop(v.rect))                     // 종료 팝(전이 1회)
  _anyCooldownShown = anyShown
```
- 카운트다운 `CeilToInt`. 슬로모 중 rem=배틀시간이라 느리게 감소.

**말랑말랑 juice (사용자 선택: 3·4)** — 전부 unscaled UI, `EaseOutBack` 오버슛 공유, rect 파괴 시 즉시 중단(critic m2):
- **(3) 숫자 틱 팝**: 카운트다운 정수가 바뀌는 프레임에만(=`cooldownText.text != 새값`, text 자체가 직전값 저장소라 struct write-back 불요) 스쿼시&스트레치 팝(`cooldownTickPopScale`, 가로↔세로 반대).
- **(4) 종료 플러리시**: `ReadyFlourishRoutine` = 슬롯 스프링-아웃 탄성 팝(0.9→pop→EaseOutBack 1) + **잔물결 링**(임시 GO, 확장·페이드, `cooldownReadyRippleColor/Scale`) + **섬광**(임시 GO, 페이드, `cooldownReadyFlashColor`). 임시 GO 는 종료 후 Destroy, 슬롯 파괴 시 가드.
- juice 튜닝값은 `BattleHudTrayConfig`(하드코딩 금지); 내부 sub-timing 은 CostDisplay 관례대로 인라인 const.
- **림 글로우**(셀 테두리 호흡 펄스, `cooldownRimColor`) 추가 — 유닛 셀에 유지.
- **기포는 코스트 물통으로 이관**(사용자 결정 2026-07-22): 유닛 셀엔 기포 없음. 코스트 물통(`CostDisplay`, 다른 spec `battle-tray-cost-well`)이 기포+림 글로우를 갖는다(`wellBubbleColor`/`wellRimColor`). 이 쿨타임 spec 범위 밖 터치라 코드 주석·handoff 에 명시.

**머티리얼 수명**: `RebuildSlots` 시작에서 기존 `_slotVisuals` 순회하며 `cooldownMat` 있으면 `Destroy`; `_anyCooldownShown=false` 리셋. `OnDestroy` 에서도 순회 Destroy(CostDisplay.OnDestroy 패턴, 에디터는 DestroyImmediate).

## 완료 기준 (Play 시각 검증)

- [ ] 컴파일 클린 + 콘솔 에러 없음.
- [ ] `placementCooldown` 값 유닛 배치 → 포트레이트가 탁한 액체에 잠겼다가 시간에 따라 **아래로 빠지며** 유닛이 떠오름 + 중앙 `⌈초⌉` 카운트다운.
- [ ] 슬로모(0.2×) 중 수위/숫자 눈에 띄게 느려짐. 메뉴 정지 시 동결.
- [ ] 0 도달 시 오버레이 사라지고 밝은 팝 → 재배치 가능.
- [ ] 코스트 물통과 **방향·색·위치·숫자**로 구분됨(나란히 놓고 확인).
- [ ] `placementCooldown == 0` 유닛: 오버레이/머티리얼 자체가 없음.
- [ ] 쿨타임 전무 시 매 프레임 슬롯 순회 없음(`AnyActive`/`_anyCooldownShown` 가드).
- [ ] 머티리얼 인스턴스 누수 없음(`RebuildSlots`/`OnDestroy` Destroy).

✅ 확인: 2026-07-22 · commit `4b9caeeb` — 컴파일 클린. 사용자 시각 확인("일단 오케이"): 어두운 액체·셀 딤·카운트다운·juice(틱 팝/종료 플러리시) 외형. 슬로모/정지 동결·머티리얼 누수 등 동작 엣지 전체 Play 패스는 handoff Follow-up.
