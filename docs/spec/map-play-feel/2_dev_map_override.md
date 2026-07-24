# 2. 개발 확인용 맵 인덱스 강제 (런타임)

## 목적

풀이 매판 시드로 배정돼 어떤 맵이 나올지 예측 불가하다. 개발 확인용으로 **특정 풀 인덱스를 직접 지정해 진입**할 수 있게 한다. 유닛 0·1 로 만든 맵을 실제로 보려면 이 도구가 선행돼야 한다(유닛 3 Play 검증의 전제).

**제약**: 에디터 전용(EditorPrefs/EditorWindow) 금지 — **모바일 개발 빌드에서 구동**되어야 한다. 출시 빌드에는 노출되지 않는다.

## 계약

- **우선순위**: `개발 override 인덱스(≥0) > fixedMapSeed(디버그) > 토너먼트 시드 > 폴백 0`. 서버 API 는 그대로 받되(토너먼트 시드 파싱·리포팅 무변경), override 가 설정돼 있으면 그것만 이긴다.
- **override 없으면 기존 로직 100% 불변** — 이 유닛은 최상단에 한 분기를 얹을 뿐, 아래 3분기는 손대지 않는다.
- **저장 = PlayerPrefs** (`dev_forceMapIndex`, `-1`=off). 모바일에서 작동하고 앱 재시작에도 유지된다. UI ↔ BattleBridge 를 잇는 seam.
- **노출 = 개발 빌드/에디터만** — `DevOnlyGroup`(`Debug.isDebugBuild || Application.isEditor`)을 패널에 직접 부착. 출시 빌드에서 자동 숨김.
- **위치 = START GAME 버튼 위 상시 고정** — Dev 트레이(토글로 열림)에 두니 Dev 토글 버튼과 겹쳐서, `MenuButtons` 아래로 옮겨 START GAME 버튼 바로 위에 상시 노출. 트레이를 열지 않아도 바로 보인다.
- **임시 도구** — 맵 확인용 임시 기능. 정리되면 통째 제거 예정(사용자 요청 시). 그래서 게이팅·배선을 가볍게 유지한다.
- **강제 대상 = 맵+덱 페어** — 풀 엔트리는 (맵, 덱) 쌍이라 인덱스를 고르면 그 맵의 웨이브 패턴도 함께 잠긴다(기존 풀 계약 그대로).

## 변경 대상

- 신규: `Assets/_Project/Scripts/Core/DevMapOverride.cs` — static holder (PlayerPrefs backed)
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle`(≈873) 인덱스 선택 최상단에 override 분기 추가
- 신규: `Assets/_Project/Scripts/UI/DevMapOverridePanel.cs` — 스테퍼 UI (◀ ▶ OFF + 맵 이름)
- 수정: `Assets/_Project/Scenes/OutgameScene.unity` — `MenuButtons` 아래(START GAME 버튼 위) 패널 GameObject 배선 + `DevOnlyGroup` 직접 부착

## UI (스테퍼 — 사용자 선택)

```
MAP  [◀]  5:Hook  [▶]
          [ OFF ]
```

- **◀ ▶**: 인덱스 순환 (0..Count−1, 끝에서 wrap). OFF 상태에서 ▶ = 0 부터, ◀ = 마지막부터.
- **OFF**: `DevMapOverride.Clear()` → 서버 시드 경로 복귀.
- **라벨**: `{index}:{맵이름}` (예: `5:Hook`, `MapDocument_` 접두 제거, 노란색) 또는 OFF 시 `OFF`. `MapDocumentPool` 참조로 이름 표시.
- START GAME 버튼 위 상시(개발 빌드만). 한 탭에 1칸 이동 — 모바일 손가락 조작 전제.

## 구현

1. **`DevMapOverride`**: `HasIndex`(Index≥0) / `Index`(get/set PlayerPrefs) / `Clear()`. UnityEngine.PlayerPrefs 만 참조(순수 아님 — 저장이 본질).
2. **BattleBridge 훅**: `if (DevMapOverride.HasIndex) { poolIndex = Mathf.Clamp(DevMapOverride.Index, 0, mapPool.Count-1); poolSource = "dev"; }` 를 `fixedMapSeed != 0` 분기 **앞**에 둔다. 로그의 source 태그로 강제 여부가 콘솔에 남는다.
3. **`DevMapOverridePanel`**: `MapDocumentPool`·`TMP_Text`·`Button` 3개 참조. Awake 에서 리스너 바인딩 + Refresh. Step/Off/Refresh 세 메서드.
4. **씬 배선**: `DevOnlyGroup` 아래 패널·버튼·라벨 생성, pool 참조 연결. Play 로 강제 진입 검증.

## 완료 기준

- [x] `DevMapOverride` — HasIndex/Index/Clear, PlayerPrefs backed
- [x] BattleBridge — override 있으면 `source=dev` + 그 인덱스 진입, 없으면 기존 3분기 그대로(diff 9줄, 회귀 0)
- [x] 패널 — ◀▶ 순환(wrap)·OFF 복귀·맵 이름 표시(reflection 전 케이스 검증), START GAME 버튼 위 상시 + DevOnlyGroup 게이팅
- [x] EditMode green (신규 스크립트 컴파일 clean)
- [x] Play — 로비에서 인덱스 지정 → 그 맵 진입, OFF → 시드 경로 복귀 (확인 2026-07-24)
