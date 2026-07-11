# Shadow Quality Settings — 그림자 각짐 개선 기록

> 2026-07-11 적용 (커밋 `9860b148`). URP 에셋의 그림자 설정 현황과 튜닝 손잡이 정리.
> 나중에 그림자 품질/성능을 다시 만질 때 이 문서에서 시작한다.

## 배경

에디터/빌드에서 그림자 경계가 각지고 계단 형태로 보였다. 원인은 씬 라이트가 아니라 URP 에셋 설정:

- BattleScene 의 Directional Light 는 Soft Shadows 로 정상 설정돼 있었음
- `Mobile_RPAsset` (Android 빌드 기본 퀄리티) 이 **Soft Shadows OFF + 1024 해상도 + 캐스케이드 1개 + Shadow Distance 50** 이라 픽셀당 ~5cm 의 하드 섀도우가 그대로 노출
- Render Scale 0.8 업스케일이 거칠기를 가중

## 현재 설정 (2026-07-11 이후)

| 항목 | Mobile_RPAsset | PC_RPAsset |
|---|---|---|
| Soft Shadows | **ON (Medium, 9탭)** | ON (High) |
| Shadow Distance | **30** | **30** |
| 섀도우맵 해상도 | 1024 | 2048 |
| 캐스케이드 | 1 | 4 |
| Render Scale | 0.8 | 1.0 |

- 퀄리티 레벨: Quality 0 = Mobile (Android 기본), Quality 1 = PC (에디터 기본)
- 에셋 경로: `Assets/Settings/Mobile_RPAsset.asset`, `Assets/Settings/PC_RPAsset.asset`

## 변경 근거 (성능 관점)

- **Shadow Distance 50→30**: 비용이 아니라 이득. 섀도우 컬링 범위 축소 + 같은 해상도에서 픽셀 밀도 ~2배. 탑다운 고정 카메라라 30 이면 가시 범위를 커버한다.
- **Soft Shadows Medium**: 픽셀당 섀도우맵 샘플 1→9탭. 바닥이 화면 전체를 덮는 탑다운 게임이라 전 픽셀이 비용을 내지만, Render Scale 0.8 기준 중급기에서 ~0.5–1.5ms 수준으로 감당 가능. 저사양에서 부담되면 **Low(4탭)로 내리는 게 첫 번째 손잡이**.

## 남은 튜닝 손잡이 (효과 순, 실기기 프로파일 후 결정)

1. **Mobile 해상도 1024→2048** — 메모리 +12MB 내외, 그림자 패스 래스터 면적 4배 (~0.3–1ms). distance 30 적용으로 밀도가 이미 올랐으므로 부족할 때만.
2. **Mobile 캐스케이드 1→2** — 근거리 선명도 상승. 그림자 패스 드로우콜/버텍스 최대 2배라 CPU 빠듯한 기기에서 유일하게 티 날 수 있는 항목. 마지막 순위.

## 주의점

- **Shadow Distance 30 밖 그림자는 잘린다.** 카메라 구도가 바뀌어 맵 가장자리 그림자가 사라져 보이면 35–40 으로 상향.
- **Soft shadow 셰이더 배리언트 프리필터**(`m_PrefilterSoftShadows*`)는 빌드 시점에 에셋 설정 기준으로 재계산된다. 에셋 YAML 을 직접 수정하지 말고 인스펙터/에디터 API(SerializedObject)로 변경할 것. execute_code 불가 환경이라 일회용 MenuItem 스크립트 패턴 사용 (`docs/reference/lessons/01-unity-mcp-operation.md` 참조).
- 라이트 자체의 Soft/Hard 설정도 병목이 될 수 있다 — URP 에셋에서 soft 를 켜도 Light 컴포넌트가 Hard 면 하드로 렌더된다. 새 씬/라이트 추가 시 확인.
