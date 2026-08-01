# 20 — 첫 판 전투 HUD 안내 ②: 다음 웨이브

## 목적

스트레스 안내 직후, 좌하단 `다음 웨이브` 버튼을 가리켜 **점수를 위해 웨이브를 당길 수 있다**는
선택지와 그 조건을 알린다. 클릭을 요구하지 않는다.

선행: unit 19(체인 골격 · 활성 대기 · 앵커). 이 unit 은 그 체인에 스텝 2개를 잇는다.

## 변경 대상

- `Scripts/UI/NextWaveDock.cs` — 웨이브 버튼 rect 읽기 전용 노출
- `Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — 체인 ③④
- `Assets/_Project/Scenes/BattleScene.unity` — 신규 SerializeField `waveDock` 배선

> feature-wide 계약 갱신은 스펙 작성 커밋에서 이미 README 에 반영했다(아래 근거 참조).

## 구현

### 뷰 seam

`NextWaveDock.WaveButtonRect` — `_buttonRoot` 의 RectTransform. `_panel`(타이머 포함 dock 전체)이
아니라 버튼만 가리킨다. 링이 남은시간 캡슐까지 감싸면 지시 대상이 흐려진다.

### 스텝 ③④

- 앵커를 `Default`(상단)로 되돌린다 — 대상이 좌하단이라 말풍선과 겹치지 않는다.
  `FocusUi(waveDock.WaveButtonRect)`
- ③ `더 높은 점수를 위해 다음 웨이브 호출해보세요` / ④ `단, 준비가 되었을때!` — 사용자 작성본
- 두 줄 모두 `hudHintLineSeconds`(3초) 경과로 넘어간다. **클릭을 요구하지 않는다**
  (사용자 결정 2026-08-01). 근거 두 가지:
  1. ④ 자체가 "지금 누르지 마"라는 뜻이라 행동 성공 신호로 진행시키면 문구와 모순이다.
  2. 첫 판에서 웨이브를 겹치게 만들면 신규 플레이어를 스트레스 한계 = 패배로 몬다.
     안내가 패배를 유도하는 형태가 된다.

### 버튼 부재 처리

`NextWaveDock` 은 버튼 활성을 **자기 `Update` 에서** `bridge.NextWaveAvailable` 로 결정한다
(`_buttonRoot.SetActive(available)`). 그래서 Battle 진입 프레임엔 아직 꺼져 있다 —
unit 19 의 활성 대기를 그대로 탄다.

`NextWaveAvailable = _running && _usingGeneratedWaves && _wavePlan.waves != null` 이므로
**레거시 덱(생성 웨이브 아님) 경로에는 버튼이 없다.** 그 경우 ③④를 조용히 생략한다(경고 로그만).
현재 정규 경로는 생성 웨이브이므로 실기에서는 항상 뜬다.

### 계약 갱신 (README)

`Gift·에너지·점수·타이머·기믹·Next Wave·결과·스쿼드·덱 편집은 설명하지 않는다` 에서
**`점수`(스트레스)와 `Next Wave` 를 해제한다**(사용자 결정 2026-08-01). 여전히 설명하지 않는 것:
Gift 카드 운용·에너지·타이머·기믹·결과 화면·스쿼드·덱 편집.

## 완료 기준

- 컴파일 오류 0 (Runtime · Tests.EditMode · Tests.PlayMode)
- Play(첫 판 전투): 스트레스 ②가 끝난 직후 좌하단 버튼에 링 + ③④가 3초씩 순차 → 자동 종료
- 안내 중에도 웨이브 버튼이 **실제로 눌린다**(튜토리얼이 입력을 막지 않는다). 눌러도 체인이
  깨지거나 예외가 나지 않는다
- 종료 후 링·말풍선 잔류 없음. 이어지는 기믹 안내가 정상 노출
- 콘솔 경고·에러 0

**완료 확인 2026-08-01** — 사용자 Play 확인 통과. 커밋 `34cf2a8d`. 안내 중 웨이브 버튼이 실제로 눌리고(비차단) 체인이 깨지지 않음, 종료 후 링·말풍선 잔류 없음, 이어지는 기믹 안내 정상 노출까지 확인.
