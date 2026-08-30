# 3 — 셀 소비자 규약 정리 (대표 셀 / 둘레 접촉)

## 목적

「유닛 = 셀 하나」를 읽던 소비자들을 footprint 규약으로 정리한다. 원칙: **게임 규칙 값이 footprint 크기에 따라 왜곡되는 곳만 고치고**(시너지 인접), 나머지는 대표 셀 규약(README 계약 2)의 자연 귀결로 두고 규약을 명문화한다. sim 무변(결정 1) 경계는 유지.

## 변경 대상

- `Assets/_Project/Scripts/Data/FootprintMath.cs` — `RectChebyshevDistance`(rect 간 체비셰프 거리)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `RecomputeSynergyFor` 를 footprint 둘레 접촉 기준·전수 재계산으로 재정의
- `Assets/_Project/Tests/EditMode/FootprintMathTests.cs` — rect 거리 케이스

## 구현

- **시너지 인접 = 두 footprint rect 의 체비셰프 거리 1(둘레 접촉).** 대표 셀 8이웃으로 재면 3×3 유닛의 8이웃이 전부 자기 몸 안이라 시너지가 죽는다. 1×1 끼리는 거리 1 = 기존 8이웃과 동치(무회귀).
- **국소 재계산 → 전수 재계산.** 기존은 변경 셀의 3×3 만 갱신했는데, footprint 제거 직후엔 반납된 rect 를 알 수 없어 국소화가 성립하지 않는다. 판 위 유닛은 수십 기라 O(n²) rect 비교는 무시 가능. `EnqueueSynergyMul` refresh 는 멱등이라 과잉 enqueue 무해.
- **명문화하는 규약 (코드 무변경 — 대표 셀의 자연 귀결)**:
  - 효과 타일: **대표 셀 위에 있을 때만** 발동 (unit 1 — 정확 일치 조회가 곧 규약, 과발동 방지)
  - 픽업(레드불) 소비: 방어 유닛 권위값 = 대표 셀 (`PickupConsumeSystem` 무변경)
  - 사직서 드랍·아군 버프장 중심·배치 스킬 발화 지점: 대표 셀 (`DefenderTile.cell`·배치 경로가 primary 전달)
  - 방향지정 레인 중심: 대표 셀 (unit 2 에서 전달 확정)
  - 보스 밀집 셀(`DefenderDensity`): 유닛당 대표 셀 1표 — 큰 유닛이 밀집 계산에 1기로 세는 것 수용
  - 길막 해저드 배치 거절(`EffectSpawner`): `DefenderTile`(대표 셀)만 회피 — **비대표 칸 위 해저드 스폰은 수용**(시각 겹침뿐, B-1 겹침 수용 철학과 동일). ECS 로 footprint 를 넘기는 건 sim 무변 결정 위반이라 후속 후보로.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 코어 무회귀 — 2494 전건 실패 0 (1×1 시너지 동치 포함)
- [x] `RectChebyshevDistance` 케이스 그린 — 겹침 0 · 접촉 1 · 이격 N · 1×1 쌍 = 8이웃 동치
- [x] 라이브 동작 무변 — 전 유닛 1×1 (거리 1 판정 = 기존 8이웃 동치)
