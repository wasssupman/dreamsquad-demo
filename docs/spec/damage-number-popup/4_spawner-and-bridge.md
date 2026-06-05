# 4 — 스포너·브리지·씬 (DamageNumberSpawner + 드레인)

## 목적

`DamageNumberEvent` 를 실제 팝업으로 연결한다. `DamageNumberSpawner`(MonoBehaviour, VfxSpawner 패턴) 가 풀에서 팝업을 꺼내 적 머리 위에 재생하고, `BattleBridge.DrainDamageNumberEvents()` 의 스텁을 실제 드레인으로 교체한다. 씬 wiring + Play 검증.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Presentation/DamageNumberSpawner.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 필드 + 스텁 드레인 교체
- 씬 wiring (UnityMCP): 스포너 컴포넌트 추가 + 프리팹/카메라 할당 + BattleBridge 참조 연결

## 구현

### DamageNumberSpawner

- `[SerializeField] GameObject popupPrefab;` (= `DamageNumber_Popup.prefab`)
- `[SerializeField] DamageNumberStyle style;`
- `[SerializeField] float headYOffset = 0.6f;` (발치 position → 머리 위)
- `[SerializeField] Camera billboardCamera;` (미할당 시 `Camera.main`)
- `Awake`: `style.EnsureDefaults()`, 풀 생성 `new DamageNumberPool(popupPrefab, transform)`.
- `public void Spawn(Vector3 worldPos, float amount)`:
  - popupPrefab/카메라 null 가드(로그 후 return — VfxSpawner 규약).
  - `int shown = Mathf.Max(1, Mathf.RoundToInt(amount));`
  - `var pos = worldPos + Vector3.up * headYOffset;`
  - `pool.Get().Play(shown, pos, cam, style, pool.Return);`

### BattleBridge

1. **필드** (vfxSpawner 인근):
   ```csharp
   [SerializeField] private Wassup.Presentation.DamageNumberSpawner damageNumberSpawner;
   ```
2. **스텁 교체** — `DrainDamageNumberEvents()`:
   ```csharp
   private void DrainDamageNumberEvents()
   {
       if (!_damageNumberEventQueue.IsCreated) return;
       if (damageNumberSpawner == null) { _damageNumberEventQueue.Clear(); return; }
       while (_damageNumberEventQueue.TryDequeue(out var evt))
       {
           if (evt.amount <= 0f) continue;
           damageNumberSpawner.Spawn(
               new Vector3(evt.position.x, evt.position.y, evt.position.z), evt.amount);
       }
   }
   ```

### 씬 wiring (UnityMCP)

- VfxSpawner 가 붙은 오브젝트(또는 BattleBridge 오브젝트)에 `DamageNumberSpawner` 추가.
- `popupPrefab` = `DamageNumber_Popup.prefab`, `billboardCamera` = 전투 카메라(미할당 시 Camera.main).
- BattleBridge 의 `damageNumberSpawner` 필드에 연결.

## 완료 기준

- compile: CS 에러 0.
- Play (Squad): 방어유닛이 적을 때릴 때 **적 머리 위에 데미지 숫자**가 뜬다.
- 숫자가 펀치 스케일로 튀어나와 위로 떠오르며 사라진다.
- 데미지가 클수록 크고 빨갛게(작으면 작고 희게).
- 디펜더가 맞을 때는 숫자 없음.
- console 에러/경고 0.
- 사용자 Play 확인 후 일자 + 커밋 해시 기재.

✅ 2026-06-05 구현·wiring·compile 클린, 사용자 Play 반복 확인 후 마무리. 커밋: 1434911
