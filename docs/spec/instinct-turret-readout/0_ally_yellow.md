# unit 0 — 아군 본능은 노랗다

## 목적

판 위 본능 넷이 전부 같은 빨간 대포라 편이 안 읽힌다. 아군 본능을 **노랑**으로 갈라, 배치
판단에서 «내 포탑 / 저쪽 포탑»이 한눈에 서게 한다.

색은 **머티리얼이 아니라 메쉬**다(계약 2) — KayKit 의 red/yellow 프리팹은 같은
`platformer.mat` 을 쓰고 FBX 만 다르다. 그래서 틴트가 아니라 **프리팹 교체**가 답이다.

## 변경 대상

- `Assets/_Project/Prefabs/Structures/Instinct_Ally.prefab` (신규 — `cannon_base_yellow` 변형)
- `Assets/_Project/Prefabs/Structures/Instinct_Enemy.prefab` (신규 — `cannon_base_red` 변형)
- `Assets/_Project/Data/Structures/Structure_GuardInstinct.asset` — `viewPrefab` → Ally
- `Assets/_Project/Data/Structures/Structure_WatchInstinct.asset` — `viewPrefab` → Enemy
- `Assets/_Project/Data/Structures/Structure_TestInstinct.asset` — `viewPrefab` → Enemy (dev 맵 SiegeTest)

## 구현

1. 벤더 프리팹의 **변형(Variant)** 을 프로젝트 폴더에 만든다(계약 4 — 벤더 원본 무편집).
   - `Instinct_Ally`  ← `KayKit/.../Prefabs/yellow/cannon_base_yellow.prefab`
   - `Instinct_Enemy` ← `KayKit/.../Prefabs/red/cannon_base_red.prefab`
   - 변형이므로 지금은 오버라이드 0. unit 1 이 여기에 프리젠터를 얹는다.
2. 본능 SO 3종의 `viewPrefab` 을 위 변형으로 재지정한다. `viewScale`(0.4)·스탯은 그대로.
3. 마음(Core) SO 2종(`Structure_EnemyCore` · `Structure_EnemyHeart`)은 **손대지 않는다** — 중립
   `structure_A` 유지(후속 후보).

### 왜 SO 를 편으로 나누지 않고 프리팹만 바꾸나

`StructureData` 는 진영을 갖지 않는다(SO 주석의 명시적 설계). 지금 저작 현황이 이미
「수호 본능 = 아군 전용 · 파수 본능 = 적 전용」이라 **SO 의 프랍 슬롯만 갈라도 편이 갈린다** —
브리지에 진영 분기를 넣을 이유가 없다(계약 3).

⚠ 이 전제가 깨지는 순간은 **같은 본능 SO 를 양편에 저작할 때**다. 그때는 프랍 슬롯 하나로는
표현이 안 되므로 진영별 프랍을 브리지로 올린다 — 지금 미리 만들지 않는다.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0 (코드 변경 없음 — 에셋만)
- [x] EditMode 2 lane 그린 — 2,518개 중 실패 1(`UnitKitCatalogTests.CatalogDescriptions_UseThreeFixedSections`,
      malphite 배치 스킬 문안 30자 > 28자). **이 spec 과 무관한 사전 실패**다: `Defender_Malphite.asset`·
      `UnitKitSummary` 둘 다 워킹트리에서 clean 이고, 문안이 길어진 것은 `4bfba2c2 feat(malphite):
      배치 지진이 … 광역 피해 40` 이 남긴 것이다.
- [x] Duel 라이브 Play — 좌측 (4,3)(4,8) 아군 본능 **노랑**, 우측 (16,3)(16,8) 적 본능 **빨강**,
      (18,5) 적 마음은 중립 프랍 그대로
- [x] 스크린샷 육안 확인 (배치 페이즈 — 좌 노랑 2 / 우 빨강 2)
