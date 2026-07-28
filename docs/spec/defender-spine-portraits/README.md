# defender-spine-portraits

> 상태: Unit 0~1 완료 · Unit 2 구현 중 (2026-07-28)
>
> 선행: `defender-portraits`(Sprite/UI), `unit-parts-appearance`(Spine 외형)

## 목표

기존 AI 일러스트 포트레이트를 폐기하고, 각 방어유닛의 **현재 Spine 외형 상체**를
투명 정적 Sprite로 베이크해 모든 포트레이트 UI에서 사용한다.

검증 질문: *"작은 UI 슬롯만 보고도 전장에 배치되는 Spine 유닛과 같은 캐릭터임을
즉시 알아볼 수 있는가?"*

## 확인 결과

- 카탈로그 20종은 같은 리그에서 `partSkins` + 일부 `slotColors`로 외형이 갈린다.
- 기존 1254×1254 AI 이미지 38장(약 88MB)은 이 데이터와 무관해 얼굴·복장·무기가 다르다.
- 모든 관련 UI는 기존 `portrait` 필드를 소비하므로 참조 교체만 필요하다.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_bake_profile_and_editor_tool.md` | 재현 가능한 Spine 포트레이트 베이크 도구와 프레이밍 프로필 |
| 1 | `1_roster_bake_and_cutover.md` | 라이브 로스터 전수 베이크 + `portrait` 참조 교체 |
| 2 | `2_consumer_smoke_and_legacy_cleanup.md` | 실제 UI 크기 검증 + 기존 AI 포트레이트 제거 |
| 3 | `3_handoff_summary.md` | 구현 종료 후 커밋·검증 인계 |

## feature-wide 계약

- source of truth는 `skeletonDataAsset + partSkins + slotColors`, PNG는 파생 산출물이다.
- 대상은 `DefenderCatalog.units` 전부다. 출력은
  `Art/DefenderPortraits/spine/defender_portrait_{id}.png`의 id 기반 1유닛 1Sprite다.
- 동일 애니메이션의 고정 포즈에 전장과 같은 `SpineCombinedSkinCache.Apply`를 사용한다.
- 이미지는 상체 중심 투명 컷아웃이다. 배경·액자·등급색·클래스색·텍스트를 굽지 않는다.
- 공통 프레이밍 + 필요한 id만 offset/zoom override를 쓰며 Editor profile이 소유한다.
- 출력은 512×512 RGBA straight alpha, sRGB, bilinear, mipmap off, clamp,
  Sprite Single/Full Rect/center pivot이다. PMA readback은 straight alpha로 변환한다.
- Editor preview scene만 사용한다. 현재 씬과 ECS·전투·Spine 런타임은 변경하지 않는다.
- nullable `portrait` 폴백은 유지한다.
- 기존 AI 원본은 새 Sprite 전수 배선과 UI 확인이 통과한 뒤 삭제한다. 복구는 git 이력으로 한다.

## 시각 완료 기준

- 92px에서도 얼굴/헤어 또는 헬멧/상의가 읽히고 몸통은 허리 부근에서 끝난다.
- 얼굴/머리장식은 보존한다. 장비는 일부가 잘려도 되며 전체를 담으려고 전신을 축소하지 않는다.
- 투명 경계에 검정/흰색 halo가 없고, 기존 UI 배경·이름 밴드·쿨다운 액체와 충돌하지 않는다.

## 파이프라인 커버리지

**N/A** — 플레이 오브젝트 경로는 그대로 두고 UI용 Sprite와 `portrait` 참조만 교체한다.

## 후속 후보

- 적 포트레이트, stale signature CI, 일부 화면의 라이브 `SkeletonGraphic` 전환.
