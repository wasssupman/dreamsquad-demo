# 1 — 마음 머리 위 스트레스 바

## 목적

**스트레스를 마음 위에서 차오르는 바로 보여준다**(명제 4·8). 지금 마음은 화면에 수치가 전혀
없고(three-minute-kill-race unit 2 가 게이지 2종을 제거했다) 프랍 균열 틴트 4단계만 남아 있다.
그 균열은 **유지**한다 — 바는 정밀, 균열은 한눈이라 역할이 겹치지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs` — `OverheadBarSkin.Stress` 값 추가 + `stress` BarSkin 필드
- `Assets/_Project/Data/**/UnitOverheadUiStyle.asset` — **신설 스킨 값 기입**(아래 함정)
- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs` — 스킨 선택 경로
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncGoalOverheadGauges` 의 `DefenderCore` 스킵 제거

## 구현

**1. `OverheadBarSkin` 에 3번째 값을 추가해도 안전하다.** 이 enum 은 **코드 전용**이다 —
에셋·씬 직렬화 0건이고 런타임 인자로만 전달된다(critic 리뷰에서 전수 확인). `GamePhase` 가
`CameraDirectionConfig.asset` 에 정수로 박혀 있는 것과 **다른 상황**이다.

**2. ⚠ 바에 넘기는 값은 `healthRatio` 를 그대로 두고 «시각만» 반전한다.**
`UnitOverheadView` 의 damageTrail 은 **감소 방향으로만** 애니메이트한다(증가는 스냅).
스트레스 ratio(차오르는 값)를 그대로 넘기면 상승에 트레일이 안 붙고, 회복할 때 엉뚱하게 붙는다.
**체력 비율을 넘기고 `Stress` 스킨이 채움 방향과 색을 뒤집으면** 기존 트레일이 공짜로 맞는다
(마음이 맞을 때 = 체력 감소 = 트레일 발생).

**3. ⚠ 스킨 에셋 값을 반드시 채운다.** `UnitOverheadUiStyle.cs` 에 「shield 색이 `(0,0,0,0)` 투명
이라 안 보였다」는 실측 함정 주석이 이미 있다. `Stress` BarSkin 을 직렬화 필드로 신설하면 기존
`.asset` 은 **전 필드 기본값(투명·폭 0)** 으로 로드된다. 같은 커밋에서 에셋에 값을 기입한다.

**4. 색 어휘.** 방어 진영 초록/적 빨강과 **다른 축**이어야 한다 — 이건 진영 체력이 아니라 위기
게이지다. unit 3 의 화면 림과 **같은 계열 색**으로 묶어 「저 바가 차면 화면이 붉어진다」가
학습 없이 읽히게 한다.

**5. `SetUnit` 시그니처.** 현재 `bool defender` 로 스킨을 고른다(호출부 5곳). 오버로드를 하나 더
두기보다 **스킨 인자를 받는 형태로 좁게 확장**하고 기존 호출부는 기본값으로 그대로 둔다 —
5곳을 전부 마이그레이션하면 이 unit 의 diff 가 관계없는 파일로 번진다.

## 완료 기준

- [ ] 컴파일 0 에러 · 콘솔 에러 0
- [ ] EditMode 전체 완주, 신규 실패 0건
- [ ] Play: 마음 위에 바가 **보인다**(투명 함정 회피 확인)
- [ ] Play: 적이 마음을 팰수록 바가 **차오르고**, 뒤에 트레일이 따라붙는다
- [ ] Play: 본능·적 마음의 바는 **그대로**다(스킨이 안 섞였다)
- [ ] Play: 프랍 균열 틴트가 여전히 단계적으로 진행된다
