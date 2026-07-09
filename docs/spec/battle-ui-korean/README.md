# battle-ui-korean

> 상태: 완료 2026-07-09

## 목표

전투 화면(BattleScene)의 **플레이어용 UI 문구를 전부 한글화**한다. 폰트는 한글 지원
폰트로 렌더되게 한다.

검증 질문: *"전투 화면의 모든 안내/버튼/결과 문구가 한글로, 깨짐(두부) 없이 보이는가?"*

## 폰트 전략 (핵심)

- 프로젝트 UI 폰트는 대부분 라틴 전용(LiberationSans 기본 · Bangers · Anton · Kanit)이라
  한글 글리프가 없다. 유일한 한글 폰트는 **Jua SDF**(동적 아틀라스, 캐주얼 라운드).
- **라벨마다 폰트를 갈아끼우지 않는다.** 대신 **TMP Settings 전역 폴백 리스트**에 Jua SDF 를
  추가(`Assets/TextMesh Pro/Resources/TMP Settings.asset` → `m_fallbackFontAssets`).
  → 모든 폰트에서 없는 글리프(한글)가 자동으로 Jua 로 렌더된다. 라틴 문구는 기존 폰트 유지.
- 효과 범위: 폴백은 "없는 글리프"에만 개입 → 아웃게임 영문 UI 는 영향 없음(스코프 안전).

## 변경 대상 (문구만 교체, 배선 없음)

| 파일 | 문구 |
|---|---|
| `PlacementPhaseView.cs` | 배치 단계 / 배치 단계 · N초 / 전투 시작 |
| `NextWaveDock.cs` | 다음 웨이브 / 다음 웨이브 N / 웨이브 없음 |
| `ScoreHudView.cs` | 점수 |
| `ResultScreen.cs` | 승리·패배 / 내 점수 / 시간·유출 / 대기 중… / 나·봇-N / 다시하기 |
| `Draft/WavePatternStripView.cs` | 다가오는 웨이브 / 웨이브 미리보기 불가 / N초 |
| `Draft/DraftView.cs` | 이번 라운드\n스킬 / 코스트 N |
| `Draft/DraftCardFanView.cs` | 기본·메타·수집·에고 / 체력·사거리·공격·쿨 |
| `Dreamcatcher/DreamcatcherSelectionView.cs` | 드림캐쳐 |
| `SkillBar.cs` | 입구/출구 타일 선택 · 대상 선택 |
| `TMP Settings.asset` | 전역 폴백에 Jua SDF |

## 범위 밖

- **MapSettingsPanelView** — 개발용 맵 설정 툴(LEGACY/MAPGRID 등)이라 제외.
- 아웃게임(로비/스쿼드/드림캐쳐 덱빌더) UI — 별도 작업.
- 데이터 유래 문자열(유닛/스킬 displayName)은 SO 데이터 소관.

## 완료 기준

- 컴파일 클린. 전투 화면의 배치/웨이브/점수/결과/드래프트/스킬 안내가 한글로 두부 없이 표시.
