# 1 — 뷰 오버라이드 중립화 + 드롭 노브

## 목적

(a) 재배치 전용 이름이던 뷰 오버라이드를 소비처 2개(재배치·드롭)에 맞게 중립화한다. (b) 드롭 모션 튜닝값을 `DragSwaySettings` ⑩ 그룹으로 추가한다(하드코딩 금지).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — `SetRelocationViewOverride`/`ClearRelocationViewOverride`/`TryGetRelocationViewOverride`/`_relocationViewOverride` → `SetDefenderViewOverride`/`ClearDefenderViewOverride`/`TryGetDefenderViewOverride`/`_defenderViewOverride` 리네임
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_relocationViewOverride.Clear()` 호출부(맵 리셋 co-locate, 현 1160 부근) + `SyncMonoUnitViews` 소비부
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 호출부 리네임
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` — ⑩ 그룹 추가
- `Assets/_Project/Data/Config/DragSwaySettings.asset` — 직렬화 갱신(기본값이면 자동)

## 구현

리네임은 순수 기계적 — 의미·수명·Clear co-locate 불변식 유지. 주석의 "relocation unit 6" 이력 표기는 남기고 "drop-dismount 가 두 번째 소비처" 한 줄 추가.

⑩ 드롭 하마 노브 (기본값 = 설계 확정치):

```csharp
[Header("⑩ 드롭 하마 — 릴리스 반동→솟음→착지 (defender-drop-dismount)")]
public float dropTotalSeconds = 0.45f;    // 총 시간. 런타임에 deploymentDuration 으로 클램프(계약 3)
public float dropRecoilSeconds = 0.12f;   // 반동 구간(총 시간 내 비율로 환산)
public float dropRecoilDip = 0.35f;       // 반동 깊이(world, -camUp)
public float dropArcHeightFactor = 0.5f;  // apex = max(거리×factor, min)
public float dropArcMinHeight = 1.5f;     // apex 절대 하한(world) — "솟음" 보장
public Vector2 dropLaunchControl = new Vector2(0.25f, 1f); // c1: 전진비율·높이배수
public float dropLandingHeight = 0.25f;   // c2 높이배수(end 직상방 — 수직 착지)
public float dropCordSnapFade = 0.15f;    // 분리 후 줄 페이드(초)
public float dropRingFade = 0.3f;         // 고리 페이드(초)
```

Range/Tooltip 은 기존 그룹 스타일을 따른다. 값 자체는 unit 5 검증 후 Play 육안 튜닝 대상.

## 완료 기준

- compile 클린 · 재배치 PlayMode 테스트(`RelocationPlacementSessionTest` 2건) 통과 — 리네임 무회귀
- `DragSwaySettings.asset` 인스펙터에 ⑩ 그룹 노출 확인
