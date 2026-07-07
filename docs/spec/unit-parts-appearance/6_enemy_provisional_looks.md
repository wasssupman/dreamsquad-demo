# Unit 6 — Enemy 임시 휴먼 외형 (기어 0 + 원색 틴트)

**작성일**: 2026-07-07
**성격**: 임시(provisional). 몬스터형 스켈레톤 수급 전까지의 stopgap. 사용자 지시로 Enemy 스코프를 일시 개방.

## 목적

기존 3.8 스파인 철거 후 skeleton 이 null 이던 Enemy 7종(Basic/Runner/Swift/Needler/Sniper/Rootcaster/Debuffer)이 quad 폴백(색 판때기)으로 렌더되고 있었다. 이들을 임시로 Defender 와 같은 Casual Character(휴먼) 스켈레톤에 물려, **최대한 기어를 착용하지 않고 원색 피부 틴트로 "나쁜놈" 느낌**을 주어 전투 화면을 통일한다. (Vanguard/Tanker 2종은 이미 `full_skins` 단일 스킨으로 물려 있어 대상 아님.)

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_{Basic,Runner,Swift,Needler,Sniper,Rootcaster,Debuffer}.asset` (7)
- `Assets/_Project/Editor/SpineUpgradeSmoke.cs` — enemy provisional 검증 블록 추가

## 구현

각 적 에셋에 적용:

- `skeletonDataAsset` → Casual Character (guid `ee98f82138b60430f97c6863317c3a2f`)
- 애니 매핑 → `idleAnimation: Walk / attackAnimation: Attack1 / deathAnimation: Die` (휴먼 스켈레톤 클립, Vanguard/Tanker 와 동일)
- `spineSkinName: full_skins` (partSkins 존재 시 무시되나 안전 폴백)
- **파츠 조합(기어 0)**: `bottom` + `brow` + `eyes` + `hair_short` + `mouth` + `skin/skin_1`. 상의·장갑·부츠·헬멧·무기(gear)·안경·망토 **전부 미착용** → `skin/skin_1` 의 맨몸(body/arm/leg/head 6슬롯)이 그대로 드러나 틴트가 보인다. `top` 을 빼야 원색 피부가 가려지지 않는다.
- **원색 틴트(`slotColors` 9슬롯)**: body 6슬롯(body/head/arm_l/arm_r/leg_l/leg_r) = 채도 높은 원색 피부, hair 3슬롯(hair/hair_long/helmet_hair) = 어둡게.
- `spineVisualScale` 은 기존 1.3 유지(Defender 스파인 기준 사이즈).

원색 피부 컬러(적별 아이덴티티):

| 적 | 피부 | 컨셉 |
|---|---|---|
| Basic | 초록 (0.32,0.58,0.20) | 고블린 |
| Runner | 적색 (0.82,0.15,0.12) | 핏빛 돌격 |
| Swift | 주황 (0.93,0.47,0.09) | 민첩 |
| Needler | 마젠타 (0.68,0.15,0.60) | 독성 |
| Sniper | 크림슨 (0.56,0.07,0.15) | 저격 |
| Rootcaster | 틸 (0.11,0.58,0.46) | 독성 캐스터 |
| Debuffer | 바이올렛 (0.45,0.14,0.72) | 디버프 캐스터 |

생성/검증 스크립트는 JSON(`Casual Character.json`) 스킨·슬롯 대조로 유효성을 사전 검증(결번·기어착용·중복·본체누락·틴트슬롯).

## 완료 기준

- [x] 7종 정적 검증: 파츠 스킨 실존, 결번 0, 기어 카테고리 0, 카테고리 중복 0, `skin/skin_1` 포함, 틴트 슬롯 실존 (2026-07-07)
- [x] 배치 스모크 PASS: `[SMOKE] enemy provisional looks OK: 7종, unique=7` — 조합 어태치>6(얼굴/머리 존재), validator 무경고, Walk/Attack1/Die 클립 해석, 조합 유일 (2026-07-07, 격리 리그)
- [x] **에디터 시각 확인(사용자) — 통과 (2026-07-07)**: 전투에서 7종 렌더 + facing 확인 완료. 커밋 `fc333507`(외형) + `d7ef2d58`(facing 수정). 얼굴/머리/색 세부는 인스펙터 드롭다운·`slotColors` 로 언제든 튜닝 가능(구조는 고정).

## 후속 발견 — facing 수정 (d7ef2d58)

적을 휴먼 리그에 물린 뒤 **이동 방향과 반대로 바라보는** 증상 확인. 원인은 데이터 flip 이 아니라 `SpineUnitView.SetFacingByViewDelta` 의 enemy/defender 역부호 분기(과거 적/디펜더가 반대 방향 별도 리그 쓰던 시절의 잔재). 단일 Casual Character 리그 공유가 된 지금은 규칙을 하나로 통합(`dx>=0 → ScaleX -1`)해 해결. 디펜더 경로 무영향. 방향 반대 리그는 코드 분기가 아니라 `SkeletonFlipX` modifier(데이터 정규화)로 처리하는 게 설계 의도.

## 주의점

- **임시 상태다.** 몬스터형 리소스가 들어오면 이 7종을 교체한다(README 후속 후보).
- quad 폴백 경로는 유지(`visualMaterial` 미삭제) — skeleton 이 다시 null 이 되면 자동 폴백.
- Vanguard/Tanker 는 `full_skins` 단일 스킨이라 partSkins 없음 → 스모크 대상에서 제외(`partSkins.Count == 0` 스킵).
- 얼굴/머리 인덱스는 색 틴트만큼 검증되지 않았다(구조 유효성만 배치 보장, 시각은 사용자 확인 통과).
- **facing 규칙은 이제 전 유닛 단일**(`SetFacingByViewDelta` 분기 제거). 새 캐릭터 리그 추가 시 방향이 반대면 코드가 아니라 `SkeletonFlipX` modifier 를 그 SkeletonDataAsset 의 `skeletonDataModifiers` 에 붙여 정규화할 것.
