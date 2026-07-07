# 0 · LobbyKeyringSettings SO

## 목적

로비 키링 드래그의 모든 튜닝값을 담는 ScriptableObject 를 만든다. 인게임
`DragSwaySettings` 와 역할은 같지만 단위가 다르고(월드 → 캔버스 px) 낙하/바운스
필드가 추가되므로 별도 타입. 두 캐릭터가 에셋 1개를 공유한다.

## 변경 대상

- 신설: `Assets/_Project/Scripts/Data/LobbyKeyringSettings.cs` (`Wassup.Data`)
- 신설: `Assets/_Project/Data/Config/LobbyKeyringSettings.asset`

## 구현

`[CreateAssetMenu(menuName = "Wassup/Lobby Keyring Settings")]`. 필드(전부 캔버스 px
기준, 각 필드 Tooltip 필수):

| 그룹 | 필드 | 기본값 | 의미 |
|---|---|---|---|
| 추종 | `ropeLength` | 160 | 고리→캐릭터 머리 줄 길이(px) |
| 추종 | `spring` | 100 | 추종 강성(1/s²). ↑=팽팽 |
| 추종 | `damping` | 2.5 | 감쇠(1/s). ↓=출렁 |
| 추종 | `maxSpeed` | 2400 | 추종 속도 상한(px/s). 0=무제한 |
| 추종 | `maxAngle` | 8 | 기울임 최대각(deg) |
| 낙하 | `gravity` | 4000 | 낙하 가속(px/s²) |
| 낙하 | `bounceDamping` | 0.35 | 착지 반발 계수(0~1). 1회 작은 바운스 기준 |
| 낙하 | `bounceMinSpeed` | 300 | 이 속도(px/s) 미만 착지 시 반동 없이 정지 |
| 낙하 | `fallUprightSpeed` | 90 | 낙하 중 기울임의 직립 복귀 속도(deg/s) — unit 2 에서 추가 |
| 착지 | `landingMinX` / `landingMaxX` | -800 / 800 | 착지 x 클램프(anchoredPosition) |
| 비주얼 | `cordWidth` | 8 | 줄 폭(px) |
| 비주얼 | `cordColor` | (0.45, 0.38, 0.28) | 줄/고리 색 |
| 비주얼 | `ringRadius` | 28 | 고리 반경(px) |
| 비주얼 | `cordAttachDrop` | 60 | 줄 끝을 rect 상단에서 머리 안쪽으로 내리는 깊이(px) — rev 2026-07-07 (줄이 머리 위에 떠 보이는 문제) |

기본값은 시작점일 뿐 — unit 3 Play 검증에서 라이브 튜닝한다. 에셋 편집이 런타임
즉시 반영되도록 컨트롤러는 매 프레임 SO 를 읽는다(인게임과 동일 패턴).

## 완료 기준

- compile 클린 (Unity 콘솔 에러 0).
- `LobbyKeyringSettings.asset` 생성, 기본값 상기 표와 일치.

확인 2026-07-07 — 사용자 통과 확인. 커밋 `f076a76b`.
