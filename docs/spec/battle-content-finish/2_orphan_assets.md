# 2 — 닿지 않는 에셋 (장부 `ledger.md`)

## 목적

씬·코드에서 **인스턴스화될 여지가 없는** 에셋을 지운다. 전수 목록은 [`ledger.md`](ledger.md) — 여기는 묶음과 근거만.

## 판정 (두 방향이 일치한 것만)

1. **순방향** — 루트 = `BattleCoreScene` + `ProjectSettings/*` + `Assets/Resources/**` + 코드(`.cs` 런타임·테스트·에디터)가 경로 문자열로 여는 파일 41. 거기서 GUID 를 따라가(씬 → 프리팹 → SO → 스프라이트 → 머티리얼 → 셰이더) **닿지 않으면** 후보. 테스트가 폴더째 스캔하는 경로 21(`Data/Defenders` 등)은 루트로 치지 않았다 — 스캔은 검증이지 인스턴스화가 아니다.
2. **역방향** — GUID 인바운드 0 → 추이적 고아(지우면 따라 고아가 되는 것까지) · basename 이 `.cs` 어디에도 없음(이름/경로 로드 사각).
3. 둘을 교차: 역방향 집합 가운데 순방향에서 **닿는 것은 0** 이었다. 순방향에서만 나온 것(서로를 참조하는 프롭 SO↔프리팹 쌍 같은 순환)은 C 로 따로 묶었다.

## 묶음

| 묶음 | 파일 | MB | 처분 |
|---|---|---|---|
| **A** `_Project` 고아 폐포(306) — 옛 Tilemap 타일 아트·풀 밖 스테이지 2(`Art/Theme`) · 카드 테스트 아트 6 · 컨셉 원본 · 탄 SO 49 + 그 GA vfx 프리팹 38 · 옛 액티브 스킬 연출 VFX · 오디오 20(옛 배치 음성 세트 16 · BGM · 리빌 테이크 3) · 테스트 덱/플랜 8 · Spine CH3/Doll 12 · 플립북 테스트 픽스처 · 루트 png 4 · 키링 잔재 · PrimeTween Demo 46 | 306 | 91.2 | 삭제 |
| **B** A 가 사라지면 닿지 않게 되는 벤더 파일 — GabrielAguiar 98 · PixPlays 16 · Hovl 9 · KayKit 4 | 127 | 51.1 | 삭제(벤더 재추림) |
| **C** 테마 풀 밖 프롭 17 세트(SO + 프리팹 + png + 생성 텍스처) + 효과 타일 2 세트 — 어느 테마의 풀에도, 어느 스테이지 프리팹에도 없다. SO↔프리팹이 서로를 참조해 역방향엔 안 잡혔다 | 55 | 4.2 | **D7** — 삭제(추천) vs 테마 풀에 넣기 |
| **D** 비활성 카드 `Card_IncubusPact` + `Effect_sub_incubus_pact` + 아트 — 2026-08-08 사용자 결정으로 카탈로그에서 뺐고 테스트가 사유를 지킨다 | 3 | 2.1 | **D8** — 삭제 vs 보관 |

합계(전부 승인 시) 491 파일 · ≈149 MB. `Assets` 511 → ≈362 MB.

## 닿지 않아도 남기는 것

- 테스트가 경로로 여는 덱 6 · 플랜 1(`Deck_Ford/Hook/Isle/Spiral/Twin` · `WaveA` · `WavePlan_BossTest`) — 라이브 효과는 0(맵 풀 밖). D4 ② 에 따라 덱 5 는 테스트 목록을 라이브 4 로 바꾸며 삭제, `WaveA` · `WavePlan_BossTest` 는 `Tests/Fixtures/` 로. 산 덱 4 는 `Data/Decks/`.
- `DreamcatcherDeck_Default.asset`(D2 재사용) · `DefenderPortraitBakeProfile.asset`(베이커가 경로로 연다) · `Assets/Editor/SpineSettings.asset`.
- `Assets/Resources/RuntimeMaterials/*.mat` 4 — 단위 4 가 SO 참조로 옮긴다.
- Spine 런타임/에디터 내부 128 · TMP 셰이더 15 — 패키지 내부지 콘텐츠가 아니다.
- `Data/EnemyCatalog.asset` — 순방향으로 닿는다(테스트 3 이 경로로 연다).

## 구현

- 삭제 직전 `guidrefs.py index` → `verify_spec.py` → `reach.py` → `ledger.py` 를 다시 돌려 장부와 같은지 본다(단위 0·1 뒤 집합이 바뀔 수 있다).
- `git rm -r` 경로 지정, 장부 묶음 순(A → B → C/D). png `.meta` 의 6.6 임포터 churn 은 섞지 않는다.
- PrimeTween `PrimeTweenInstaller.asset` 이 Demo 씬 2 를 가리킨다 — 벤더 설치 에셋이라 그대로 둔다(missing 참조 2, 무해).
- `Scripts/Data/Decks|WavePlans` 이동은 같은 커밋에서 테스트 상수 4(`RetiredWaveAuthoringPortTests` · `DragonBreath/LiveDeckBoss/SlimeSplit*AuthoringTests` · `CoreMatchEntryCarryTests`)를 고친다.

## 완료 기준

- [x] `reach.py` 재실행 → `_Project` 에서 닿지 않는 것 = 「남기는 것」 목록뿐
- [ ] 에디터 콘솔 missing reference 0 · Play 한 판(프롭 · 해저드 · 구조물 · 디펜더 Spine 4 · 플립북 3 · 탄 궤적 · 배치 음성이 전과 같다)
- [x] 커밋 2~3(경로 지정)

## 구현 결과 (2026-10-07) — `3260b71b3`

- 장부 A·B·C·D 491 + 테스트 전용 덱 5 → 파일 1,008(메타 포함) 삭제 · 243만 줄. 빈 폴더 메타 정리. 산 덱 4 → `Data/Decks/` · 픽스처 2 → `Tests/Fixtures/`(폴더 메타 신규) · 테스트 4 경로/목록 갱신(LiveDeckBoss 의 균등 단언은 뺌 — 라이브 4 덱이 보스 3종 전부 든다).
- 삭제 뒤 `guidrefs.py index` → `reach.py` 재실행: `_Project` 에서 닿지 않는 것 = 픽스처 2 · 베이크 프로필 · (Spine/TMP 내부 · SpineSettings) — 「남기는 것」과 일치.
- 에디터 재임포트 뒤 Hovl 편집기 로직이 `VFX/WeaponTrail_Slash_Cyan.prefab` 에 `appliedEffectPreset` 을 써 넣었다(이 spec 과 무관 — 미커밋, 백로그의 `HS_SwordMeshTrail` 항목).
