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

### 이식 제외 — 구현에서 더한 행

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 카드 문안 formatter 의 **사본**(`CoreCardText.cs` ← `DreamcatcherCardText` 648줄 복사) | 그 formatter 는 아웃게임 덱 페이지·카드 상세(범위 밖, 옛 컨트롤러 그대로)도 쓰는 **단일 문안원**이다. 두 벌이면 로비와 전투가 같은 카드를 다르게 설명한다(= 구현 3 의 실패형). 엔진·브리지 참조 0 인 순수 static 이라 새 층이 **부른다**(unit 9 는 `Battle/`·`Bridge/` 만 지운다). `CoreCardText` 는 브리핑 문안 + 거절 사유 문장만 갖는다 | 공유(복사 안 함) |
| 순수 뷰 부품 7종의 사본(`CardAbsorbFlightPresenter` · `DreamcatcherTargetArrow` · `HandDismissTapCatcher` · `JarFigurePile`·`JarFigurePhysics` · `SpineFigureBuilder` · `AwakeningCharge` · `DreamcatcherFocusConfig`) | 엔진·브리지·옛 컨트롤러 참조 **0** 인 UI 부품이라 그대로 부른다(5a~6c 가 `KeyringSim`·`UnitOverheadView` 를 부른 선례). 브리지·ECS 를 잡던 넷(손패 뷰·드래그 슬롯·포커스·항아리)과 `Shader.Find` 를 쓰던 카드면 메쉬만 새로 옮겼다 | 공유(복사 안 함) |
| `DcActionFlipbookView`(213줄 — 선택 유닛 주변 「이동」 버튼) | 재배치 진입구다 — 재배치는 이식 제외(7d · 진입구가 이미 꺼져 있다 `DcInspectController.RelocationEnabled`) | 범위 밖(7d) |
| 옛 bake 의 **빔 프리팹 겸용 오라 등록**(`BakeUnitMechanics` 가 `AreaDot` 의 `auraPrefab` 을 빔 index 로 싣고 **이어서** 오라 풀에도 등록) | 한 필드가 빔·오라를 겸한 뒤 가드가 안 생긴 결함 — 버스터즈 빔 프리팹이 숙주에 기본 방향으로 박혀 떠 있게 된다. 빔 쪽만 옮긴다(`CardViewAssetTests.메커닉이_선언한_빔과_오라는_뷰_표에_실린다` 가 둘이 안 겹침을 건다) | 제거(결함) · ⚠ 사용자 플레이 확인 항목 |
| 손패 컨트롤러의 C# 이벤트 셋(`GaugeChanged` · `AwakeningOverflowed` · `AwakeningGainedAt` · `HandChanged`) | 각성은 **연속값**이라 사건이 없다 — 항아리가 코스트 물통처럼 매 프레임 `HandDeck.Gauge`·`OverflowLost` 를 읽는다. 흡수 비행의 출발점은 `UnitSlain` 의 자리(값 스냅샷). 손패 변화는 커밋 receipt(사용) · `CardDetached`(회수) · `MatchStarted`(리셋)가 계기다 | 제거(구조로 접힘) |
| 항아리 **ready 임계 = 종류별 저작 값 셋의 최솟값**(`AwakeningCharge.UnitCost(costSquad, costUnit, costActive)`) | 값의 주인이 카드로 옮겨졌다(D15 · 정의표 `CardDef.Cost`). 임계 = 이 판 카드 값의 최솟값 — 라이브 저작(20/20/20)에서 같은 값이다 | 소유 이전 |
| 온보딩 첫 손패 고정(`PinTutorialFirstHand` · D5) | 튜토리얼 콘텐츠는 전량 제거됐고(76038c26) 코어는 `pinnedFront` 칸만 갖는다 — 밀어 넣는 쪽(G11)이 생길 때 배선한다 | 보류 |
| 덱 확정 기록(`LogDeck` → `PersistMatchDeck` · D22) | 아웃게임 기록(토너먼트 대기 기록)이라 5c 의 제출 게이트와 한 묶음이다 — 이 unit 의 질문(카드가 손에 잡히나)에 불필요 | 후속 후보 |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 전 → 후 | 근거 |
|---|---|---|
| `HandDeck.UsableReason(entryId)` | 없음 → 손패·대기·각성 **읽기 전용 preflight**(커밋 앞단과 같은 순서 — 대기 &gt; 각성) | 딤·드래그 게이트·거절 문구가 뷰의 자기 셈(`gauge >= cost`) 대신 코어 답을 읽게(구현 6·9). 숙주 종속은 기존 `WouldAttach`, 국면은 커맨드가 답한다 · `MatchHandDeckTests.쓸_수_있나_preflight_는_커밋과_같은_답이다` |
| `MatchViewAssets` | 탄·거점·장판 → + **카드 줄 → 카드 에셋** · **규칙 줄 → 부착 오라** · **스킬 연출(빔) 표** | 번호를 매긴 순회(`CardDefinitionBuilder.Fill` — 빈 칸을 건너뛴다 · `BindingDefinitionBuilder.Bake`)가 채운다. 뷰가 덱을 다시 모으면 빈 칸 하나에 번호가 밀린다 |
| `BindingDefinitionBuilder` · `AreaDot` | 빔 index 안 실음(7a — 「무연출 · 7c 이월」) → **빔 = `DataIndex`**(스킬 연출 표) · 그 밖 `auraPrefab` = 오라 | `AreaDotSkill` 은 `HasData` 일 때만 빔을 요청한다 — 번호가 없으면 버스터즈 개시 빔이 **요청조차 안 된다**. 정의표 해시는 라이브 판에서만 변한다(골든 코퍼스는 SO 를 안 읽는다 — 골든 무변) |
| `BattleDriver` 덱 | `_cards` 저작만(「프로필·액티브 롤은 7c」) → `_cards` 가 비면 **프로필 확정 덱 + 판 시드 액티브 롤**(`CoreDeckComposition`) · 채우면 개발 덮어쓰기 · `Mode` 읽기 창구 | `rule-holders.md` D2·D3·D4 · S1~S5(새 주인 = 판 밖 빌드). ⚠ S2 — 굴림 시드를 **판 시드에서 파생**(옛 = 벽시계) · 난수 = `Unity.Mathematics.Random`(옛 `System.Random` 순열과 다르다) |
| `CoreMapOverlay` | 배치 오버레이만 → + **카드 조준 채널**(부착 범위 링 · 액티브 칸 원 · 칸 집합) | 옛 브리지 범위 채널의 카드 몫. 반경 = `RangeSpec.RadiusWithOrigin(host 몸)` 호출만 · 배치 드래그에 양보(옛 H-2) · 스타일 = `DreamcatcherFocusConfig.attachRangeStyle` · `TileSetData.aimRingStyle`(옛 두 채널 값) |
| `CoreVfxSpawner` | 카드 슬롯 없음 → **카드 흡수 임팩트**(옛 `cardAbsorbPrefab` · 폴백 링+버스트) · **카드 규칙 발동 임팩트**(펀치 · 흰 플래시 · 흡수 VFX, 숙주당 `DcVisualConfig.ProcImpactMinIntervalSec` — 5a 가 소비처 0 으로 세워 둔 자산의 개통) · **SkillVisual 적중 펄스**(탄 `hitPrefab`) | 옛 `DrainDcTriggerFiredEvents` · `SpawnCardAbsorbVfx` — 카메라 킥·흡수음은 발동 쪽에서 뺀다(옛 결정) |
| `CoreBeamPresenter` | 공격 빔만 → + **SkillVisual 빔**(키 = 맞는 쪽 · 수명 = 조사 지속) | 7a 이월(스킬 대상별 빔) |
| `CoreDcAuraVisualPool` | 드림캐쳐 출처 스탯 오라만 → + **메커닉 선언 부착 오라**(`BindingAttached` → 뷰 표) | 6c 이식 제외의 「카드 페이로드 오라」 행 해소 · 옛 규약(숙주당 하나 · 앵커 추종) |
| `CoreStatusFxSpawner` | 표식 줄 대기 → **표식**(`CardAttached` × `TargetsEnemies` → `Marked` · `CardDetached` 가 끈다) | 6c 「살찌운 제물 표식」 행 해소. 변경 대상 표의 `CoreBountyMarkView` 는 **따로 세우지 않았다** — 옛 그림이 곧 상태 표식 등록부의 `Marked` 줄이었다(`BattleBridge.cs:3666`) |
| `CoreUnitOverheadUiLayer` · `CoreSelectionPanel` | 카드 줄 없음 → **부착 카드 줄 두 자리**(자기 구독 · 부착 순) · 발동 펄스(`PulseCards`) | 6c 「부착 카드 줄」 행 해소 · D20 |
| `SelectionInput` | 선택 = 패널만 → 선택이 **손패를 연다**(선택 전환 = 재딜 없이 대상만) · 닫기가 손패도 닫는다 · 비-부착 조준 시 선택만 놓기 · 손패 열린 동안 보드 탭 라우팅 | 2026-08-19 사용자 결정(손패 진입구 = 유닛 선택뿐) · 옛 `DcInspectController` 의 선택 ↔ 손패 핸드오프 |
| `CoreDefenderTray` · `CoreCostDisplay` | → 칸 줄 창구(`StripRect` — 손패가 접는다, 접힌 동안 칸 픽 없음) · 코스트 배지 억제(`SetSuppressed`) | 옛 트레이 ↔ 손패 뒤집기 · `CostDisplay.SetSuppressed` |
| `RuntimeMaterialFactory` | + `CreateCardCrumpleUi`(always-included `Resources/RuntimeMaterials/CardCrumpleUI.mat`) | 옛 카드면 메쉬는 셰이더를 이름으로 찾았다(추가 제약 · 구현 10) — `CoreCardFaceMesh` 는 이 창구만 쓴다 |
| `ViewOrder` | + `Hand = 55`(오버헤드 뒤) | 몸에 붙는 것(표식·아이콘)이 선 뒤 손패가 같은 사건으로 창을 다시 읽는다 |
| `BattleCoreUnity.Check.csproj` | + `Rendering/RuntimeMaterialFactory.cs` 명시 컴파일 | 에디터가 새 메서드를 컴파일하기 전 `Wassup.Runtime.dll` 이라(5a~6a2 의 새 SO 와 같은 이유) |
| 선택 패널 캔버스 순서(`CoreSelectionPanel`) — **플레이 3차 「퇴근 작동 하지 않음」** | 0 → **9** | 7c 에서 선택이 손패를 연다. 손패 캔버스(5)의 전화면 바깥 탭 캐처가 패널 위를 덮어 퇴근 버튼 탭이 「선택 닫기」가 됐다(공개 진입 `InvokeAction` 테스트는 포인터 경로를 안 타 초록이었다). 옛 패널 = `DcInspectPanelView.cs:26` `PanelSortingOrder = 9`(손패 5 · 항아리 7 위). 증상 단언 `CorePlayThreeSymptomTests.유닛을_선택해_…`(버튼 자리 레이캐스트 최상단 = 버튼) |

### `rule-holders.md` 귀속 행 → 코드 포인터(7c 몫)

| 행 | 새 자리 |
|---|---|
| D2 덱 = 저장 부착 + 판마다 굴린 액티브(섞기는 코어 한 곳) | `CoreDeckComposition.Compose` → `BattleDriver.Begin`(판 밖) → `HandDeck.Begin`(섞기) |
| D3 저장 덱 무효 = 빈 부착 덱(폴백 없음) | `CoreDeckComposition.ResolveAttachDeck` · `CardViewAssetTests.확정_덱이_무효면_부착_덱은_비어_있다` |
| D4 감싸는 액티브가 없으면 그 장만 빠진다 | `CoreDeckComposition.Compose`(경고) |
| D21 표식 픽 반경 = 저작 노브 | `CoreHandView.EnemyPickRadiusTiles`(모드 `AwakeningConfig`) → `CoreCardTargets.TryPickNearestEnemy` |
| D23 손패는 실시간 · 감속은 손패 화면의 몫 | `CoreHandView.TickSlomo`(카드를 잡는 동안만 `TimeManager` 리스 — 판은 발행률로만 느려진다) |
| S1·S3 시드 부분 셔플 · 빈 풀/0장 = 빈 목록 | `CoreDeckComposition.RollActives` · `CardViewAssetTests.액티브_굴림은_판_시드로_재현된다` |
| S2 시드 = 판 시드 파생(벽시계 금지) | 같은 곳(`seed ^ RollSalt`) |
| S4·S5 숨긴 카드의 스킬은 풀에서 뺀다 | `SkillLoadoutController.FilterHiddenSkills`(순수 함수 — 그대로 부른다) |

### 씬 배선 — **완료**(2026-09-24, MCP `execute_code` · SerializedObject → `manage_scene save`)

`BattleCoreScene` 에 아래를 배선했다(씬 diff = 추가 211줄 · 삭제 0). 컴포넌트는 비어 있는 **씬 참조**를 같은 씬에서 찾아 경고와 함께 쓰므로(배선이 정본) 테스트는 씬 배선 없이도 돈다(`CoreCardViewTests` 가 없으면 세운다).
씬 저장이 `CoreVfxSpawner` 에 7d 의 새 칸(`_areaBreath*` · `_overlay`)을 기본값으로 같이 직렬화했고, 이어서 **7d 미배선 표도 배선했다**(브레스 프리팹 · 오버레이 참조 · `CoreFieldPresenter` — 7d 문서). `BattleDriver._cards`·`_dreamstones` 는 빈 배열로 처음 직렬화됐다(7b 칸).

| 오브젝트 | 컴포넌트 · 칸 | 값(옛 `BattleScene` 저작) |
|---|---|---|
| `CardHand`(새, HUD 캔버스 밖 — 자기 캔버스 5) | `CoreHandView`: `_driver` · `_units` · `_overlay` · `_selection` · `_tray` · `_costDisplay` · `_gauge` · `_vfx` · `_mainCamera` | 씬 오브젝트 |
| | `_focusConfig` · `_defenderCatalog` · `_labelFont` · `_numberFont` · `_trayConfig` | `Data/Dreamcatcher/DreamcatcherFocusConfig.asset`(4c0ee755…) · `Data/DefenderCatalog.asset`(346c00d9…) · Jua SDF(218cce73…) · Anton SDF(7f50de03…) · `Data/Config/BattleHudTrayConfig.asset`(903c6fe7…) |
| `JarDock`(새 — 자기 캔버스 7) | `CoreAwakeningGaugeView`: `_driver` · `_tray` · `labelFont` · `numberFont` · `representativeUnit` · `figureSkeletonMaterial` | 폰트 위와 같음 · a2fc7863… · b66cf7a1… |
| `Vfx` | `CoreVfxSpawner._cardAbsorbPrefab` · `_cardAbsorbScale` · `_dcVisual` | 37f1dda3…(옛 `cardAbsorbPrefab`) · 0.6 · `Data/BattleView/DcVisualConfig.asset` |
| `BattleDriver` | `_profile` · `_cardCatalog` · `_activePool` · `_activeCount` · `_activeCards` | `Data/PlayerProfile.asset` · `Data/Dreamcatcher/DreamcatcherCardCatalog.asset` · 옛 `SkillLoadoutController.defaultPool` · 2 · 옛 `DreamcatcherHandController.activeCards`(`Active_*.asset` 6) |
| `SelectionPanel` | `CoreSelectionPanel._defenderCatalog` | `Data/DefenderCatalog.asset` |

## 완료 기준

- [x] **새 PlayMode lane 초록** — `CoreCardViewTests`(5): 탭 부착 → 오버헤드 카드 아이콘 1 · 패널 카드 줄 1 · 퇴근 → 회수 · 끌기 부착 범위 링 = N + host 몸 · 거절 문구 = 코어 답 · 표식 → 숙주 소멸로 회수 · `ViewOrder`(구독을 뒤집어 건다) + 부팅 스모크 「표식 수 = 코어 표식 수」. 씬 배선 뒤 PlayMode 코어 **56/56**.
- [x] 뷰·입력 코드(`Cards/` · `Input/CardInput.cs`)에 `Unity.Entities` **0건** · `Shader.Find` **0건** · 매니저·브리지·컨트롤러 이름의 클래스 **0건**(grep). 판정 0: 자격 = `HandDeck.UsableReason`/`WouldAttach` · 조준 = `CardDef.Kind`/`TargetsEnemies` · 반경 = `RangeCatalog` → `RadiusWithOrigin` 호출만.
- [x] 드래그 → 부착 e2e: 커맨드 → receipt → 카드 줄 표시 · 거절 사유 문구가 코어 답과 **같은 문자열** — 위 PlayMode 두 건(탭·끌기 부착 · 거절 문구)이 초록.
- [x] `ledgers/bridge-methods.md` **잔량 변화 없음(28)** — `check_ledgers.py` exit 0 · 미정 28. 카드 UI 의 브리지 행은 이미 「HandDeck」·「뷰 풀」로 배정돼 있어 이 unit 이 닫을 미정 행이 0 이다(아래 이행 메모).
- [x] `core-reviewer` APPROVE(2026-09-24 · finding 0 — `UsableReason` 읽기 전용 · 덱 굴림 시드 결정론 · 뷰 판정 재계산 0(범위 링 = `RadiusWithOrigin`, 대상 몸 없음) · formatter 직접 호출 · `Shader.Find` 0 · `additionalShaderChannels` 2곳 · 빔/오라 겸용 등록은 옛 결함으로 확정). 리드 export 재검증(`88617a8fe`): build 0 · test 630 · Check 0 · 미정 28. Unity lane·씬 배선은 MCP 세션 복구 뒤(대기).
- [ ] **여기서 처음으로 카드가 손에 잡힌다.** 손맛 확인은 조각 D 전체 뒤(7d)의 사용자 플레이 2차로 미룬다 — 기믹·보스·분열이 빠진 채로 물으면 답이 항상 「아니다」가 된다(5c 의 규율). ⚠ 씬 배선(위 「미배선」 표)이 먼저다.

> **이행 메모(2026-09-24).** 커밋: `b22e0708e`(preflight) · `87f9c5781`(덱 배선 · 뷰 표) · `e139d3625`(카드 사건의 그림) · `674829015`(손패 UI) · `1f7d524c2`(테스트) + 이 문서 커밋. 커밋마다 클린 export 3종: build 0 · test **630**(+1) · Check 0. EditModeAssets `CardViewAssetTests`(5) · PlayMode `CoreCardViewTests`(5)는 스크래치 csproj 로 **컴파일만** 확인(오류 0) — Unity EditMode 코어·Assets · PlayMode 코어 lane 은 MCP 세션 끊김으로 **미실행**. 골든: 코어 변경은 읽기 전용 메서드 하나라 사건·해시 무변 **예상**(골든 코퍼스는 SO 를 안 읽는다 — 빔 `DataIndex` 는 라이브 정의표만 바꾼다) — Unity 골든 11종 확인 대기.
> **Unity lane(씬 배선 뒤, 2026-09-24)**: EditMode 코어+Assets **925/927**(빨강 = 기준선 `bomb_man`·`boomerang` 둘만 · 골든 11종 포함 초록 → 골든 무변) · PlayMode 코어 **56/56**. 첫 실행에서 잡힌 것 셋을 고쳤다: ⑴ 각성 저작이 없는 모드(테스트 강제 모드 SO)가 덱을 짓다 카드마다 에러를 냈다 → 그런 모드는 **카드 없는 판**으로 짓고 한 번 경고(`BattleDriver`) ⑵ 거절 문구 테스트가 배치 국면의 손 배치 닫힘(`inputEnabledDuringPlacement`)에 막혔다 → 디버그 스폰으로 세운다 ⑶ 움찔 트윈의 표시 플래그 콜백이 손패 파괴 뒤 PrimeTween 에러를 냈다 → `warnIfTargetDestroyed: false`(콜백은 표시 플래그만 되돌린다).
> 복사·적응 실측: 새 파일 9(손패 1,410 · 포커스 666 · 항아리 648 · 드래그 582 · 카드면 194 · 조준 기하 192 · 문안 103 · 덱 조립 82 · 입력 68 = 3,945줄) + 기존 12 파일 보강. 옛 순수 부품 8 과 formatter 는 **부른다**(위 이식 제외).
