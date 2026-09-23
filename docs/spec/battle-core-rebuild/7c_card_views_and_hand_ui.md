# 7c — 카드의 화면 (조각 D · 3/4)

> 7a·7b 가 연 사건이 **손에 잡힌다.** 각성이 차고, 손패가 펼쳐지고, 카드를 유닛에 끌어다 붙이고, 붙은 범위가 링으로 보인다. 이 unit 은 **Unity 층 전부**이고 코어 규칙은 한 줄도 안 만든다.

## 목적

복사·적응 대상이 실측 **8,149줄**로 이 spec 에서 가장 큰 덩어리다: `Scripts/UI/Dreamcatcher/` **7,608줄** 중 **6,169줄**(손패 뷰 1,782 · 드래그 슬롯 852 · 각성 게이지 771 · 포커스 655 · 카드 문안 648 · 흡수 비행 237 · 플립북 213 · 항아리 193+145 · 카드면 메쉬 190 · 타겟 화살 186 · 나머지 297) + `BattleBridge.Dreamcatcher.cs` **1,429줄** 중 연출 몫 + 오버레이·패널 보강.

⚠ **옛 검사 패널 2파일(`DcInspectController` 637 + `DcInspectPanelView` 802 = 1,439줄)은 이 unit 이 «다시» 만들지 않는다.** 그것이 곧 선택 패널이고 **5b 가 이미 `CoreSelectionPanel` + `SelectionInput` 로 이식했다** — **퇴근 버튼은 그 패널의 액션 슬롯에 산다**(길게 누르기는 은퇴). 여기서 더하는 것은 그 패널의 **부착 카드 줄 한 개**뿐이다. 새 패널을 세우면 화면에 패널이 둘이 된다.

⚠ **아웃게임 덱 컬렉션(`UI/Outgame/` 1,465줄 — 카드 브라우저·덱 페이지·덱 스트립)은 전투 밖**이라 이 spec 범위가 아니다. 그쪽은 계속 옛 컨트롤러를 본다.

그래서 이 unit 은 **한 파일에 담기지만 커밋은 화면 단위로 쪼갤 수 있다** — 규칙이 코어에 이미 서 있어(7a·7b) 어느 순서로 올려도 판이 안 깨진다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 손패 | `BattleCoreUnity/Cards/CoreHandView.cs`(← `UI/Dreamcatcher/DreamcatcherHandView.cs` 1,782줄) — 아치 부채 + 스프링 target 모델(focus/idle/드래그/딜 공유) · 손패 = `HandDeck.Hand()` 읽기 모델 |
| 드래그·부착 | `Cards/CoreCardDragSlot.cs`(← `DreamcatcherCardDragSlot.cs` 852줄) · `Cards/CoreCardFocusPresenter.cs`(← 655줄, 락온) · `Cards/CoreCardAbsorbFlight.cs`(← 237줄, 타겟 비행 찰싹 흡수) · `Cards/CoreDcTargetArrow.cs`(← 186줄) |
| 각성 | `Cards/CoreAwakeningGaugeView.cs`(← 771줄) + 항아리 독(`JarFigurePile` 193 · `JarFigurePhysics` 145 · `SpineFigureBuilder` 62 · `AwakeningCharge` 33 · `DreamcatcherFluidBackdrop` 53) |
| 카드면 | `Cards/CoreCardFaceMesh.cs`(← `UiCardFaceMesh.cs` 190줄) · `Cards/CoreCardText.cs`(← `DreamcatcherCardText.cs` 648줄) · `Cards/CoreDcActionFlipbook.cs`(← 213줄) |
| 손패 닫기 | `Cards/CoreHandDismissTapCatcher.cs`(← 54줄) — 빈 곳 탭으로 손패를 접는다. **검사 패널은 5b 가 이미 세웠다**(위 ⚠) |
| 범위 프리뷰 | `View/CoreMapOverlay.cs`(5b 신설분) 에 **부착 범위 링** 추가 — 반경은 **코어 `RangeCatalog` 가 준다**(7a). 뷰가 다시 계산하지 않는다 |
| 부착 카드 줄 | `Hud/CoreSelectionPanel.cs`(5b) 에 **부착 카드 줄** · `View/CoreUnitOverheadUiLayer.cs`(5a·6c) 에 **오버헤드 부착 카드 줄** — 6c 가 「부착 사건이 unit 7 이다」로 남긴 두 자리 |
| 표식 뷰 | `Cards/CoreBountyMarkView.cs` — 표식 붙은 적 위의 표식. 사건 `CardAttached`(host = 적) |
| 입력 | `Input/CardInput.cs` — 손패 드래그 → `AttachCard`/`CastActive` 커맨드. **판정 0**(5b 의 `DragPlacementInput` 규율) |
| 뷰 설정 SO | `Data/BattleView/DcVisualConfig`(5a 가 소비처 0 으로 세워 둔 것) 개통 + `DreamcatcherFocusConfig`(95줄) 이식 |
| 방출 순서 | `ViewOrder.cs` 에 상수 추가 — 부착 범위 링은 **바닥**(유닛 앞), 표식·부착 카드 줄은 **몸에 붙는 것**(유닛 뒤), 손패는 uGUI 캔버스 |
| 테스트 | `Tests/PlayModeCore/CoreCardViewTests` — 사건 → 뷰 수 일치 · 회수 · 드래그 제스처 사슬 |

## 구현

1. **풀마다 자기 구독**(계약 12 — 통합 뷰 없음). `CardAttached`·`CardDetached`·`CardCast`·`ScoreChanged`(각성) 를 각자 듣고 `SimEntityId → 뷰` 사전 하나만 갖는다. 유령이 잡히면 자가 치유(경고 로그 + 회수)이고 그것은 **어떤 소멸 경로가 사건을 안 냈다**는 신호다(5a·6c 와 같은 규율).
2. **손패는 코어의 읽기 모델이다.** `HandDeck.Hand(into)` 가 큐 앞 N 의 **비파괴 창**을 준다. 뷰가 자기 목록을 들면 창 멤버십 가드가 갈린다. ⚠ **각성 손패의 실시간 계약(D23)은 뷰 애니메이션 시간이고 규칙은 틱이다** — 딜인·스프링은 `Time.deltaTime`, 판정은 코어.
3. **카드 문안은 formatter 가 이긴다.** 에셋 `description` 은 폴백일 뿐이고, 실제 문장은 정의표 값에서 조립한다. 저작 문면과 실제 수치가 갈리면 **화면이 규칙을 틀리게 가르친다**.
4. **범위 프리뷰는 판정과 같은 함수를 부른다.** 드래그 중(사건 없음)에 형을 묻는 것이 7a 가 라우팅 표를 남긴 이유다. 뷰는 `RangeCatalog.Resolve(concrete, 반경, 트리거)` → `RadiusWithOrigin(host 몸)` 만 부르고 **상수를 다시 쓰지 않는다**. ⚠ 같은 카드가 배스티온(몸 1.5)에 붙으면 버스터즈(0.5)보다 **1칸 넓다** — 그것이 제약 13 이고 화면이 그것을 말해야 한다. 모르는 concrete 는 **안 그린다**(fail-closed — 화면이 판정보다 관대하면 그게 틀리게 가르치는 것이다).
5. **제약 13 은 뷰에도 적용된다**(6c 구현 6 과 같은 문장): 사건의 `OriginBody == 0` 이면 **자리에 떨어지는 것**이라 칸 반폭으로, `> 0` 이면 **몸에서 나오는 것**이라 그 반경으로 그린다. 새 필드가 필요 없다.
6. **부착 포커스 락온은 밀집 시인성 설계다.** 카드를 들면 후보 유닛이 떠오르고 나머지가 가라앉는다 — 이식하되 **판정은 코어**(`Applicability`)가 준 답만 쓴다. 뷰가 자기 자격 판정을 하면 「붙는데 무효」·「안 붙는데 초록」이 돌아온다.
7. **각성 항아리 독은 회차가 오를 때만 어필한다**(0.3초). 상시 어필은 이미 철거된 결정이다 — **되돌리지 말 것**.
8. **부착 카드 줄 두 자리**(6c 이월). 선택 패널의 줄은 **부착 번호 오름차순**(D20 — 부착 순서 자체가 기능이다), 오버헤드 줄은 아이콘만. 6c 가 「빈 카드 슬롯을 먼저 만들지 않는다」로 미뤄 둔 자리다.
9. **입력은 판정을 갖지 않는다.** 손끝 → 후보 host 변환은 순수 함수, 커밋은 커맨드 하나(`AttachCard`/`CastActive`), 답은 receipt. 거절 사유 문구는 코어 답을 **옮겨 적기만** 한다. ⚠ **표식은 적을 겨냥한다** — 후보 집합이 방어유닛이 아니므로 `TargetsEnemies` 를 코어에 묻고 뷰가 추측하지 않는다.
10. **런타임 머티리얼은 `RuntimeMaterialFactory` 경유다.** 옛 카드면 메쉬가 `Shader.Find` 를 쓰면 그대로 옮기지 않는다(추가 제약 · 모바일 shader stripping). ⚠ **UGUI 커스텀 셰이더는 `Canvas.additionalShaderChannels` 가 필요하다** — uv1/uv2/normal/tangent 를 읽는 카드면 셰이더가 캔버스 설정 없이는 조용히 깨진다.
11. **저장 덱의 stale ID 함정.** 카드 id 를 리네임하면 `profile.json` 의 저장 덱이 Validate 실패로 안 열린다. 이 unit 은 id 를 **안 바꾼다**.

## 파이프라인 커버리지

| 정거장 | 손패 카드 | 부착 범위 링 | 표식 | 각성 항아리 |
|---|---|---|---|---|
| 저작 | `DreamcatcherCard`(아트·문안) | N/A — 도형은 코어가 준다 | `DcVisualConfig` | `AwakeningConfig` · Spine 피규어 |
| 정의표 | `CardDef` + `MatchViewAssets` 줄 번호 | `RangeCatalog`(코어) | 〃 | 〃 |
| 생성 | `HandDeck.Hand()` 읽기 모델 → 카드 뷰 대여 | 드래그 시작 → 오버레이 | `CardAttached`(host = 적) | 판 시작 1회 |
| 매 프레임 | 스프링 target 추종 | host 따라감 | 숙주 추종(유닛 뒤) | 게이지 보간 |
| 소멸 | 손패 이탈 · 판 종료 | 드래그 종료 | `CardDetached` · 숙주 `UnitDestroyed` | 판 종료 |
| 소리 | 카드 딜·부착음(5c 의 `SoundManager` 재사용) | N/A | N/A | 회차 상승음 |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 뷰가 들던 손패 목록·부착 등록부 | 진실원은 코어다(`HandDeck`). 뷰가 자기 목록을 들면 창 멤버십이 갈린다 | 제거(계약 12) |
| 뷰의 자격 판정(부착 가능 여부) | `Applicability` 가 코어에 있다. 두 벌이면 「붙는데 무효」가 돌아온다 | 소유 이전 |
| 뷰의 범위 반경 재계산 | `RangeCatalog` + `SkillMath.TryOriginRadius` 하나. 상수 복사 금지 | 제거(제약 13) |
| 상시 각성 어필 | 이미 철거된 결정(회차 상승 0.3초만). 되돌리지 않는다 | 제거(선행) |
| `CardCategory` 의 보라 프레임·「무의식」 칩 | **뷰 데이터로** 온다(7b 가 정의표에서 뺐다). 규칙 소비처는 0 | 소유 이전 |
| 아웃게임 덱 페이지·스쿼드 페이지 | 전투 밖이라 이 spec 범위가 아니다. 그쪽은 계속 옛 컨트롤러를 본다 | 범위 밖 |
| 벤더 VFX 프리팹 **신규 저작** | 이 unit 은 **배선**이다. 옛 씬이 쓰던 프리팹을 그대로 가리킨다 | 범위 밖 |
| 카드 문안의 에셋 `description` 우선 | formatter 가 이긴다(구현 3) — 에셋은 폴백 | 소유 이전 |

## 고친 것 (기존 코어·Unity 층 변경)

*(구현 중 채운다.)*

## 완료 기준

- [ ] **새 PlayMode lane 초록** — `CoreCardViewTests`: `CardAttached` 1건 → 카드 줄 1개 · `CardDetached` → 회수 · 숙주 소멸 → 붙어 있던 표식·카드 줄 전부 회수 · `ViewOrder` 로 정렬(씬 순서를 뒤집어 확인) · 부팅 스모크의 「뷰 수 = 코어 개체 수」에 **표식**을 추가.
- [ ] 뷰·입력 코드에 `Unity.Entities` **0건** · **판정 0건**(반경·자격 재계산 없음 — grep) · `Controller` 이름 **0건** · `Shader.Find` **0건**.
- [ ] 드래그 → 부착 e2e: 커맨드 → receipt → 카드 줄 표시 · 거절 사유 문구가 코어 답과 **같은 문자열**(5b 의 트레이 거절 표시와 같은 규율).
- [ ] `ledgers/bridge-methods.md` **잔량 변화 없음(28)** — 카드 UI 의 브리지 행은 이미 「HandDeck」·「뷰 풀」로 배정돼 있어 **이 unit 이 닫을 미정 행이 0** 이다. 잔량을 안 줄이는 unit 이라는 사실을 상태 라인에 명시한다(6b2 의 선례 — 누락으로 읽히지 않게).
- [ ] `core-reviewer` APPROVE(Unity 층 — 매니저·브리지·컨트롤러 이름 0 · 판정 이전 0).
- [ ] **여기서 처음으로 카드가 손에 잡힌다.** 손맛 확인은 조각 D 전체 뒤(7d)의 사용자 플레이 2차로 미룬다 — 기믹·보스·분열이 빠진 채로 물으면 답이 항상 「아니다」가 된다(5c 의 규율).
