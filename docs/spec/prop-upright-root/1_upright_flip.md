# 1 — Upright Flip (원자적)

## 목적

background/ring props 루트를 upright 로 역회전하고, 블롭을 upright 프레임에서 XZ 바닥에 눕도록 재저작한다. 루트 flip 과 블롭 재저작은 **원자적**(따로 하면 블롭이 수직으로 서 버림).

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `_backgroundPropsRoot`(`:294`)·`_ringPropsRoot`(`:424`)
- `Assets/_Project/Scripts/Editor/PropDataEditor.cs` — `AttachAuthoredBlob` 기본 블롭 프레임(신규 프랍용)
- 일회성 마이그레이션(execute_code) — 기존 프랍 프리팹 블롭 transform 직접 변환
- `Assets/_Project/Tests/EditMode/PropUprightRootTests.cs` (신규) — upright basis 불변식

## 구현

### 루트 flip

`_backgroundPropsRoot`/`_ringPropsRoot` 생성 직후 `SetParent(transform,false)` 다음에:
```csharp
root.localRotation = Quaternion.Euler(-90f, 0f, 0f); // structure props 선례와 동일 → 월드 upright
```
프랍 위치는 이미 `transform.position`(월드)로 세팅되므로 placement 무변경. Visual 은 billboard 가 월드 회전 override → 무영향. forest offset=0 이라 visualOffset 위치 변화 없음.

### 블롭 프레임 — 월드 변환 보존 수식

루트가 90°→identity 로 바뀌면 블롭(프랍 자식)의 월드 transform 을 **동일하게 유지**하려면:
- **localRotation**: `identity` → `Euler(90,0,0)` (upright 부모에서 쿼드를 XZ 바닥에 눕힘)
- **localPosition**: `p_new = (x, -z, y)` (Rx(90) 적용). 즉 기존 `(x, heightY, depthZ)` 회전프레임 → upright `(x, -depthZ, heightY)`.
  - flower `(0,0,-0.20)` → `(0,0.20,0)` · tree_1x4 `(0,0.38,0)` → `(0,0,0.38)` · barrel `(0,0.60,-0.20)` → `(0,0.20,0.60)` · log `(0,0.45,-0.20)` → `(0,0.20,0.45)`
- 이 수식은 블롭 **월드 위치·회전을 그대로 보존** → 시각적으로 블롭은 변화 없음. 바뀌는 건 저작 프레임뿐.

### AttachAuthoredBlob 기본값 정정 (신규 프랍용)

`PropDataEditor.AttachAuthoredBlob` 의 default 분기(preservation 아닌 쪽, `:117-133`):
- `localRotation = Quaternion.Euler(90f,0f,0f)`
- `localPosition = new Vector3(0f, BlobGroundLiftLocal, depthOffset)` (높이=+Y, 깊이=+Z)
- 주석의 "local −z=월드 높이" → "upright: +Y=높이, +Z=깊이, 쿼드는 Euler(90,0,0)로 XZ 눕힘" 정정.
- **preservation 분기(`:109-114`)는 유지** — 마이그레이션은 regen 이 아니라 아래 직접 변환으로 하므로 M2 트랩 회피. regen 은 이미-정정된 블롭을 보존하므로 안전.

### 마이그레이션 (일회성, regen 아님 → M2 회피)

영향 프랍(unit0 audit: forest 10종 등 블롭 보유) 프리팹의 BlobShadow 를 직접 변환:
```
p_new = (p.x, -p.z, p.y); localRotation = Euler(90,0,0); PrefabUtility.SavePrefabAsset
```
regen 을 안 쓰므로 preservation 분기와 무관.

### EditMode 테스트 (m7)

`PropUprightRootTests`: 부모 `Euler(90,0,0)` + 자식 `Euler(-90,0,0)` → 자식 월드 회전 ≈ identity 를 assert. 루트 회전 우발적 회귀 가드.

## 완료 기준

- compile 클린.
- `PropUprightRootTests` green.
- Play→스크린샷: 프랍 기립 유지, **블롭이 접지(시각적 변화 없음)**, 프랍 아래 정상 그림자.
- 인스펙터에서 prop visualOffset `+Y` 를 넣으면 화면상 위로 이동(직관 확인, 스팟 체크 1종).
