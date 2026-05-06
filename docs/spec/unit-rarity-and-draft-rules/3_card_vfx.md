# 3 — Card VFX: DraftCardVfxDriver

## 목적

카드 등급별 VFX를 담당하는 `DraftCardVfxDriver` MonoBehaviour를 추가한다.  
Common/Rare = PrimeTween 테두리 pulse, Epic = pulse + 파티클, Ego = pulse + 파티클 + 배너 shimmer.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Draft/DraftCardVfxDriver.cs`
- 수정: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs` (prefab 참조 + AddComponent)

## 구현

### DraftCardVfxDriver.cs

```csharp
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI.Draft
{
    // Attached to draft card GO by DraftCardFanView.CreateCard().
    // Reads DefenderRarity and drives:
    //   Common/Rare — PrimeTween border color pulse
    //   Epic        — pulse + particle child
    //   Ego         — pulse + particle + banner shimmer
    public class DraftCardVfxDriver : MonoBehaviour
    {
        // borderTween / bannerTween stored so OnDestroy can stop them individually.
        private Tween _borderTween;
        private Tween _bannerTween;

        public void Configure(
            DefenderRarity rarity,
            Image borderImage,
            Image bannerImage,
            ParticleSystem epicParticlePrefab,
            ParticleSystem egoParticlePrefab)
        {
            Color borderBase   = borderImage.color;
            Color borderBright = Color.Lerp(borderBase, Color.white, 0.35f);

            float halfCycle = rarity switch
            {
                DefenderRarity.Common => 1.5f,
                DefenderRarity.Rare   => 1.0f,
                DefenderRarity.Epic   => 0.6f,
                DefenderRarity.Ego    => 0.45f,
                _                    => 1.5f,
            };

            // Border pulse: Yoyo loop, base ↔ bright
            _borderTween = Tween.Color(borderImage, borderBright, halfCycle,
                Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);

            // Epic: ember particle
            if (rarity == DefenderRarity.Epic && epicParticlePrefab != null)
                SpawnParticle(epicParticlePrefab);

            // Ego: ember particle + banner shimmer
            if (rarity == DefenderRarity.Ego)
            {
                if (egoParticlePrefab != null) SpawnParticle(egoParticlePrefab);
                if (bannerImage != null)
                {
                    Color bannerBase   = bannerImage.color;
                    Color bannerBright = Color.Lerp(bannerBase, Color.white, 0.4f);
                    _bannerTween = Tween.Color(bannerImage, bannerBright, halfCycle,
                        Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
                }
            }
        }

        private void SpawnParticle(ParticleSystem prefab)
        {
            var ps = Instantiate(prefab, transform);
            ps.transform.localPosition = Vector3.zero;
            ps.Play();
        }

        private void OnDisable() { _borderTween.Stop(); _bannerTween.Stop(); }
        private void OnDestroy() { _borderTween.Stop(); _bannerTween.Stop(); }
    }
}
```

### DraftCardFanView.cs 추가 사항

SerializeField 추가:

```csharp
[SerializeField] private ParticleSystem epicCardParticlePrefab;
[SerializeField] private ParticleSystem egoCardParticlePrefab;
```

`CreateCard()` 마지막에 driver 부착:

```csharp
// borderImage = go.GetComponent<Image>()
// bannerImage = swatch (슬롯 배너 Image)
var driver = go.AddComponent<DraftCardVfxDriver>();
driver.Configure(unit.rarity, go.GetComponent<Image>(), swatch,
                 epicCardParticlePrefab, egoCardParticlePrefab);
```

### 파티클 프리팹 생성 (procedural, UnityMCP execute_code)

`DraftCardFanView` Inspector 배선 전에 프리팹을 먼저 생성한다. execute_code는 method body로 실행되므로 top-level using 없이 완전한 형식명을 사용한다.

**Epic 프리팹** (`Assets/_Project/VFX/Prefabs/DraftCard_Epic.prefab`):

```csharp
var go = new UnityEngine.GameObject("DraftCard_Epic");
var ps = go.AddComponent<UnityEngine.ParticleSystem>();
var main = ps.main;
main.startColor = new UnityEngine.Color(1f, 0.55f, 0.26f, 1f);
main.startLifetime = 0.8f;
main.startSpeed = 45f;
main.maxParticles = 20;
main.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.Local;
var emission = ps.emission;
emission.rateOverTime = 8f;
var shape = ps.shape;
shape.shapeType = UnityEngine.ParticleSystemShapeType.Circle;
shape.radius = 0.4f;
UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Project/VFX/Prefabs/DraftCard_Epic.prefab");
UnityEngine.Object.DestroyImmediate(go);
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log("DraftCard_Epic.prefab created.");
```

**Ego 프리팹** (`Assets/_Project/VFX/Prefabs/DraftCard_Ego.prefab`):

```csharp
var go = new UnityEngine.GameObject("DraftCard_Ego");
var ps = go.AddComponent<UnityEngine.ParticleSystem>();
var main = ps.main;
main.startColor = new UnityEngine.Color(0.8f, 0.27f, 1f, 1f);
main.startLifetime = 1.0f;
main.startSpeed = 60f;
main.maxParticles = 35;
main.simulationSpace = UnityEngine.ParticleSystemSimulationSpace.Local;
var emission = ps.emission;
emission.rateOverTime = 15f;
var shape = ps.shape;
shape.shapeType = UnityEngine.ParticleSystemShapeType.Circle;
shape.radius = 0.5f;
UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Project/VFX/Prefabs/DraftCard_Ego.prefab");
UnityEngine.Object.DestroyImmediate(go);
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log("DraftCard_Ego.prefab created.");
```

프리팹 생성 후 `DraftCardFanView` Inspector에서 `epicCardParticlePrefab`, `egoCardParticlePrefab` 슬롯에 각각 배선한다.

## 완료 기준

**Pulse (프리팹 없이 확인 가능):**
- [ ] 컴파일 오류 없음
- [ ] PlayMode: Common 카드 테두리가 ~3s 주기로 pulse
- [ ] PlayMode: Rare는 ~2s, Epic은 ~1.2s, Ego는 ~0.9s 주기로 더 빠르게 pulse
- [ ] 카드 버리기(discard) 후 driver OnDestroy 시 콘솔 오류 없음

**파티클 (프리팹 배선 후 확인):**
- [ ] PlayMode: Epic 카드 하단에서 주황색 파편 8/s 방출
- [ ] PlayMode: Ego 카드에서 보라색 파편 15/s + 배너 shimmer 동작
