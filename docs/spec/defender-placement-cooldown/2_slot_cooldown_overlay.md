# 2 — 셀 쿨타임 오버레이 (표현)

## 목적

쿨타임 중인 슬롯 위에 **레이디얼 스윕(시계 와이프)** + 중앙 카운트다운을 그리고, 종료 시 밝은 팝으로 "재사용 가능"을 알린다. 코스트 액체 물통과 시각적으로 구분되는 시계 은유. tick 은 unit 0 런타임(Battle 도메인)이 이미 하므로 여기서는 **읽어서 그리기만** 한다 → 슬로모 감속/정지 동결이 자동으로 반영된다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` — `SlotVisual` 확장 + 오버레이 빌드 + 프레임 리페인트
- `Assets/_Project/Scripts/Data/BattleHudTrayConfig.cs` — 오버레이 튜닝 필드(하드코딩 금지, 규칙 6)

## 구현

**BattleHudTrayConfig** — 필드 추가(전부 오버레이 튜닝):
- `Color cooldownOverlayColor` (기본 어두운 반투명, 예 `(0,0,0,0.62)`)
- `Color cooldownTextColor` (기본 흰색)
- `float cooldownFontSize` (기본 30)
- `float cooldownReadyPopScale` (기본 1.18)
- `float cooldownReadyPopDuration` (기본 0.22)

**SlotVisual 확장**: `GameObject cooldownRoot; Image cooldownFill; TextMeshProUGUI cooldownText;`
- ⚠️ **shown 상태를 struct 필드로 두지 않는다** (critic M1). `SlotVisual` 은 `List<SlotVisual>` 안의 **value type** 이라 `var v = _slotVisuals[i]; v.xxx = ...` 는 **복사본**에 쓰여 소실된다(만료 전이가 영영 안 뜸). 기존 `Update` 가 무사한 건 참조형 멤버(`Image.color`)만 건드리기 때문. → shown 판정은 **`cooldownRoot.activeSelf`**(참조 읽기, write-back 불요)로 한다.

**오버레이 빌드 (RebuildSlots, 슬롯당 1회, 기본 비활성)**:
- `cooldownRoot`: 슬롯 자식 RectTransform, 포트레이트 영역을 덮음(anchor 0..1, 이름밴드 위). `SetActive(false)`.
- `cooldownFill`: `Image`, **`sprite = null`** (흰 quad 에 Radial360 이 그대로 먹는다 — 별도 원판 에셋 불요; 셀이 사각이라 사각 시계 와이프로 충분). `type = Filled`, `fillMethod = Radial360`, `fillOrigin = Top`, `clockwise = false`(시계방향으로 걷힘), `color = cooldownOverlayColor`, `raycastTarget = false`. fillAmount 는 **남은 비율**(1→0)이라 어둠이 시계방향으로 사라진다.
- `cooldownText`: 중앙 정렬 TMP(nameFont 주입), `cooldownFontSize`, `cooldownTextColor`, Bold, `raycastTarget = false`.
- 오버레이 `raycastTarget=false` → 슬롯 bg 가 계속 입력을 받는다(unit 1 게이트가 무반응 처리; 흔들림 팝은 아래 선택).
- `RebuildSlots` 말미에 `_anyCooldownShown = false` 리셋(재빌드 후 stale 방지, critic m4).

**프레임 리페인트 (Update)** — 위치: `if (_panel == null || !_panel.activeInHierarchy || _slotVisuals.Count == 0) return;`(현 496행) **아래**, 코스트 diff-gate(`if (current == _lastCostSeen) return;` 499행) **위**. 이러면 트레이가 보일 때만 돌고(핸드 오픈·비Battle/Placement 페이즈엔 skip), 코스트 조기반환에 삼켜지지 않는다(critic m4).
```
UpdateCooldownOverlays():
  rt = GameManager.Instance?.CooldownRuntime
  if (rt == null) { if (_anyCooldownShown) hide all + _anyCooldownShown=false; return; }
  if (!rt.AnyActive && !_anyCooldownShown) return;      // 활성/표시 둘 다 없으면 순회 skip (O(1))
  bool anyShown = false
  for each v in _slotVisuals:
     float rem = rt.RemainingFor(v.data)
     bool shown = v.cooldownRoot.activeSelf             // ← struct 필드 아님(M1). 참조 읽기.
     if (rem > 0):
         if (!shown) v.cooldownRoot.SetActive(true)
         v.cooldownFill.fillAmount = rt.Fraction(v.data)
         v.cooldownText.text = Mathf.CeilToInt(rem).ToString()
         anyShown = true
     else if (shown):
         v.cooldownRoot.SetActive(false)               // activeSelf 가 곧 상태 — write-back 불요
         StartCoroutine(ReadyPop(v.rect))               // 종료 팝(true→false 전이에서 1회)
  _anyCooldownShown = anyShown
```
- `ReadyPop(rect)`: 매 프레임 **`if (rect == null) yield break;`** 가드(pop 도중 `RebuildSlots` 가 슬롯을 Destroy 하면 MissingReference — critic m2). `rect.localScale` 을 `cooldownReadyPopScale` 로 튀겼다 `cooldownReadyPopDuration` 에 걸쳐 1 로 복귀(간단 lerp). 입력/레이아웃 영향 없게 scale 만.
- 카운트다운 = `CeilToInt`(3→2→1). 슬로모 중엔 rem 이 배틀시간이라 숫자가 느리게 감소.
- `fillOrigin=Top`/`clockwise=false`/`CeilToInt` 는 고정 시계 은유라 인라인 상수 허용(파일 전반의 레이아웃 리터럴과 일관, critic n2). 단 아래 선택 흔들림을 넣으면 그 진폭/시간은 `BattleHudTrayConfig` 에서 온다(규칙 6).

**선택 juice(가능하면 포함)**: unit 1 의 무반응 차단에 얹어, 쿨타임 중 슬롯 탭 시 짧은 흔들림. 별도 위젯 없이 `ReadyPop` 류 스케일 코루틴 재사용. 비용 크면 후속 후보로.

## 완료 기준 (Play 시각 검증)

- [ ] 컴파일 클린 + 콘솔 에러 없음.
- [ ] `placementCooldown` 값 있는 유닛 배치 → 셀 위 어두운 부채꼴이 시계방향으로 걷히고 중앙 숫자가 `⌈초⌉`로 카운트다운.
- [ ] 다른 유닛 드래그로 슬로모(0.2×) 발생 시 스윕/숫자가 눈에 띄게 느려짐. 메뉴 정지 시 동결.
- [ ] 0 도달 시 오버레이 사라지고 밝은 팝 1회 → 슬롯 재배치 가능.
- [ ] 코스트 액체 물통과 쿨타임 스윕이 시각적으로 구분됨.
- [ ] `placementCooldown == 0` 유닛: 오버레이 전혀 안 뜸.
- [ ] 쿨타임 전무일 때 매 프레임 슬롯 순회 없음(`AnyActive`/`_anyCooldownShown` 가드).
