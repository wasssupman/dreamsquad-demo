# 16. Visual Audit

## 목적

rev3 구현 커밋 후에도 화면에서 "어긋남 없이 완결" 까지는 도달하지 못한 상태다. 튜닝 spec (17~) 을 착수하기 전에 **현재 결함 지점을 데이터로 카탈로그화**한다. 근거 없는 튜닝 루프를 막고, 후속 spec 우선순위를 사실 기반으로 정한다.

## 산출물

- `docs/spec/board-visualization/audit/VISUAL_AUDIT.md` — 결함 카탈로그 (아래 포맷)
- `Assets/Screenshots/audit/YYYYMMDD/*.png` — 근거 이미지
- 후속 spec 분기 제안 (본 문서 말미 Dispatch Table 참조)

## 변경 대상

- 신규: `docs/spec/board-visualization/audit/VISUAL_AUDIT.md`
- 신규 폴더: `Assets/Screenshots/audit/YYYYMMDD/`
- 수정: `docs/spec/board-visualization/README.md` — 작업 순서표에 17~ spec 분기 표시 (audit 결과에 따라 추가)

## 수행 프로토콜

### Screenshot 수집

1. Forest theme, 고정 seed (예: `12345`) 로 `BattleBridge.StartBattle` 진입.
2. 해상도 1920×1080 고정.
3. UI canvas 임시 비활성화.
4. 같은 장면에서 아래 3장 수집:
   - `game_full.png` — game view 전체
   - `game_close.png` — 카메라 근접, Place 영역 중심
   - `scene_top.png` — scene view top-down, gizmo 비활성
5. seed 바꿔 2 회 추가 반복 (총 9 장).

### 검사 축 (각 축당 최소 1 항목 기록)

#### A. 프랍 분포
- 같은 prop family 가 너무 몰려 있나? 너무 흩어져 있나?
- 비어 보이는 Env region 이 있나?
- Rotation/scale jitter 로 복붙 인상이 실제로 해소됐나?
- cluster 모드가 활성된 prop 의 결과가 cluster 로 읽히는가?

#### B. Inner corner overlay
- L자 / ㄱ자 Place 경계에서 overlay 가 자연스럽게 "꺾인 경계" 로 읽히나?
- 45°/135°/225°/315° 배치된 overlay 가 **sprite** 로 보이나, 아니면 회전된 사각형 패치로 떠 있나?
- 2 개 이상 overlay 가 같은 셀에 겹칠 때 z-fighting / 겹침 artifact 가 보이나?

#### C. Outer corner / Edge fringe
- Place 외곽이 "배치 구역" 느낌으로 분리돼 보이나, 묻혀 보이나?
- 직선 edge 구간의 fringe 두께가 일관되나?
- Walk 와 맞닿는 경계에서 shape sprite 가 끊기지 않고 이어지나?

#### D. Env 내부 variation / region blend
- region 내부가 단조롭나, 과도하게 어지러운가?
- variation texture 2 종 이상이 실제로 관찰되나?
- 인접 Env region 경계가 hard cut 인가, blend 가 보이나?
- blend band 폭이 너무 좁거나 너무 넓은가?

#### E. Walk shape
- Straight / Corner / T / Cross 가 방향이 맞게 배치되나?
- shape 간 연결부가 매끄럽나?
- Isolated / End 케이스가 시각적으로 이상한 모양을 내지 않나?

#### F. 전체 보드감
- Enter the Gungeon 참조와 비교해 가장 부족한 축은?
- 색감 톤이 zone 간 대비로 잘 분리되나, 한 덩어리로 뭉개지나?
- 이 화면이 "보드" 로 읽히나, "패치워크" 로 읽히나?

## 결함 기록 포맷

각 항목 아래 구조로 `VISUAL_AUDIT.md` 에 누적:

```
### V-<3자리 번호>: <한 줄 제목>
- 축: A/B/C/D/E/F
- 위치: screenshot 파일명 + (x,y) 셀 좌표 or 영역
- 증상: 무엇이 어떻게 이상한지
- 재현: seed / theme / 반복 여부
- 심각도: High / Mid / Low
- 가설: 원인 추정 1~2 문장
- 후속 spec 후보: 17 / 18 / ... 또는 "미분기"
```

## Dispatch Table (결함 → 후속 spec 분기 기준)

| 결함 축 | 후속 spec 초안 | 내용 |
|---|---|---|
| A. 프랍 분포 (성김/뭉침) | `17_poisson_proper.md` | Bridson 정식 도입, cluster 밀도 재튜닝 |
| B. Inner corner overlay 품질 | `18_corner_asset_pass.md` | inner/outer corner sprite 품질 + band width |
| C. Outer edge / fringe | `19_place_edge_finish.md` | edge sprite / opacity / shape 분리 |
| D. Env variation / blend | `20_env_variation_tuning.md` | noise scale, blend width, variation weight |
| E. Walk shape 정합 | `21_walk_shape_polish.md` | sprite 정합, yaw 오프셋, T/Cross 검증 |
| F. 전체 보드감 (theme) | `22_theme_palette_pass.md` | zone 톤 대비 조정, palette 고정 |
| 여러 축 복합 / volcano 채움 | `23_volcano_theme_fill.md` | 테마 계약 완성 |
| 기타 인터랙션 회귀 | (bug fix PR, spec 아님) | hover/flash 등 |

결함 1 건이 여러 축에 걸치면 주요 축에 귀속 + 부가 축 표시. Dispatch 의 후속 spec 은 audit 결과에 따라 그대로 다 만들지, 일부만 만들지 결정.

## 완료 기준

- `VISUAL_AUDIT.md` 가 존재하고, 각 검사 축 A~F 별로 최소 1 항목 기록되어 있음.
- 9 장 이상의 audit screenshot 이 `Assets/Screenshots/audit/YYYYMMDD/` 에 저장.
- 심각도 High 항목 각각에 Dispatch Table 후속 spec 번호가 매핑됨.
- README 의 작업 순서표에 audit 결과로 실제로 열릴 후속 spec (17~) 이 표시됨.
- audit 를 근거 삼지 않는 튜닝 spec 은 개시하지 않는다 (본 문서가 정책).

## 주의

- Audit 는 **관찰 + 카탈로그** 단계. 이 단계에서 코드 수정 금지. 수정은 Dispatch 가 정한 spec 에서.
- 결함을 과장하거나 축소하지 않는다. "좋아 보인다" 도 기록 (기준 영역 식별).
- screenshot 해상도 / seed / theme 는 반드시 고정. 비교가 가능해야 함.
- audit 반복 시 (예: 튜닝 spec 후 재점검) 날짜 폴더를 새로 열고 diff 로 비교.

확인 일자: 2026-04-24 / 커밋 해시: becdbd1
