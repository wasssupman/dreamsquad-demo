using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 9 — 옛 `ProjectileVariationTests` 의 규칙을 새 풀(`CoreProjectileViewPool`)로 옮긴 것.
    //
    // 옛 파일의 뒤 두 테스트는 `System.Random`·`Quaternion` 만 불러 **풀을 한 번도 지나지 않았다**(동어반복).
    // 여기서는 같은 규칙을 풀의 진짜 `Spawn` 으로 잰다:
    //   · 탄 색 hue 변주 — 0 이면 원색 그대로, 1 을 넘으면 감긴다(순수 함수)
    //   · 같은 시드 → 같은 시각 변주 순열(크기·롤·색)
    //   · 풀 재사용이 롤을 누적하지 않는다(재사용 뷰 = 새 뷰)
    //
    // EditMode 라 `Awake` 가 안 돈다 — 풀의 `Awake` 를 한 번 직접 부른다(MPB·RNG 준비. 판 구동 경로는 안 탄다).
    public class CoreProjectileVariationTests
    {
        private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _made.Count - 1; i >= 0; i--)
                if (_made[i] != null) Object.DestroyImmediate(_made[i]);
            _made.Clear();
        }

        // 옛 ProjectileVariationTests::HueShift_ZeroPreservesColor — hue 이동 0 은 색(알파 포함)을 바꾸지 않는다
        [Test]
        public void 색상_이동_0_은_원색을_지킨다()
        {
            var original = new Color(0.8f, 0.3f, 0.1f, 0.6f);
            var result = CoreProjectileViewPool.ApplyHueShift(original, 0f);
            Assert.AreEqual(original.r, result.r, 0.01f);
            Assert.AreEqual(original.g, result.g, 0.01f);
            Assert.AreEqual(original.b, result.b, 0.01f);
            Assert.AreEqual(original.a, result.a, 0.001f, "알파는 hue 변주의 대상이 아니다");
        }

        // 옛 ProjectileVariationTests::HueShift_WrapsAroundOne — hue 는 1 을 넘으면 0 쪽으로 감긴다
        [Test]
        public void 색상_이동은_1_을_넘으면_감긴다()
        {
            const float startHue = 0.95f, shift = 0.1f;
            var high = Color.HSVToRGB(startHue, 1f, 1f);
            var shifted = CoreProjectileViewPool.ApplyHueShift(high, shift);
            Color.RGBToHSV(shifted, out float h, out _, out _);
            Assert.AreEqual(Mathf.Repeat(startHue + shift, 1f), h, 0.02f, "0.95 + 0.1 은 ≈0.05 로 감겨야 한다");
        }

        // 옛 ProjectileVariationTests::Initialize_SameSeedProducesSameSequence — 같은 시드면 시각 변주(크기·롤·색) 순열이 같다
        [Test]
        public void 같은_시드면_같은_시각_변주_순열이다()
        {
            ConfigureBoard();
            var data = MakeData();
            var a = MakePool(seed: 42);
            var b = MakePool(seed: 42);

            for (int i = 1; i <= 6; i++)
            {
                var id = new SimEntityId(i);
                a.Spawn(id, data, float3.zero);
                b.Spawn(id, data, float3.zero);
                var va = ViewOf(a, id);
                var vb = ViewOf(b, id);
                Assert.AreEqual(va.transform.localScale.x, vb.transform.localScale.x, 1e-6f, $"크기 변주가 갈렸다(발 {i})");
                Assert.Less(Quaternion.Angle(va.transform.localRotation, vb.transform.localRotation), 1e-3f,
                    $"롤 변주가 갈렸다(발 {i})");
                var ca = TintOf(va);
                var cb = TintOf(vb);
                Assert.Less(Mathf.Abs(ca.r - cb.r) + Mathf.Abs(ca.g - cb.g) + Mathf.Abs(ca.b - cb.b), 1e-4f,
                    $"색 변주가 갈렸다(발 {i})");
            }

            // 대조: 변주가 실제로 걸리고 있다(모든 발이 같으면 위 단언은 아무것도 증언하지 않는다).
            float s1 = ViewOf(a, new SimEntityId(1)).transform.localScale.x;
            bool varied = false;
            for (int i = 2; i <= 6 && !varied; i++)
                varied = Mathf.Abs(ViewOf(a, new SimEntityId(i)).transform.localScale.x - s1) > 1e-5f;
            Assert.IsTrue(varied, "크기 변주가 한 번도 안 걸렸다 — 저작 jitter 가 풀에 안 닿는다");
        }

        // 옛 ProjectileVariationTests::RollDoesNotAccumulate_AcrossPoolReuse — 풀 재사용 뷰의 롤은 프리팹 회전 × 이번 롤 하나다(누적 없음)
        [Test]
        public void 풀을_재사용해도_롤이_누적되지_않는다()
        {
            ConfigureBoard();
            var data = MakeData();
            var reused = MakePool(seed: 7);
            var fresh = MakePool(seed: 7);

            // 두 풀이 같은 난수 순서를 소비하게 한다: 첫 발 → (한쪽만 회수) → 둘째 발.
            reused.Spawn(new SimEntityId(1), data, float3.zero);
            fresh.Spawn(new SimEntityId(1), data, float3.zero);
            var first = ViewOf(reused, new SimEntityId(1));
            reused.Despawn(new SimEntityId(1));

            reused.Spawn(new SimEntityId(2), data, float3.zero);
            fresh.Spawn(new SimEntityId(2), data, float3.zero);
            var again = ViewOf(reused, new SimEntityId(2));
            var brandNew = ViewOf(fresh, new SimEntityId(2));

            Assert.AreSame(first, again, "전제: 회수한 뷰가 다음 발에 재사용돼야 한다");
            Assert.AreNotSame(brandNew, ViewOf(fresh, new SimEntityId(1)), "전제: 대조 풀은 새 뷰를 만든다");
            Assert.Less(Quaternion.Angle(brandNew.transform.localRotation, again.transform.localRotation), 1e-3f,
                "재사용 뷰의 회전이 새 뷰와 다르다 — 이전 발의 롤이 누적됐다");
        }

        // ── 픽스처 ──────────────────────────────────────────────────────────

        private void ConfigureBoard()
        {
            var planeGo = new GameObject("CoreProjectileVariationPlane");
            _made.Add(planeGo);
            BoardSpace.Configure(float3.zero, 1f, planeGo.transform);
        }

        // 변주 폭은 픽스처가 정한다(시트가 덮는 값 아님). 높이 오프셋 0 — 카메라 유무와 무관하게 자리가 같다.
        private ProjectileData MakeData()
        {
            var template = new GameObject("CoreProjectileVariationTemplate");
            template.AddComponent<MeshRenderer>();
            template.transform.localRotation = Quaternion.Euler(30f, 45f, 0f);
            _made.Add(template);

            var data = ScriptableObject.CreateInstance<ProjectileData>();
            _made.Add(data);
            data.projectilePrefab = template;
            data.visualScale = 1f;
            data.visualHeightOffset = 0f;
            data.tintColor = Color.HSVToRGB(0.3f, 0.8f, 0.9f);
            data.scaleJitter = 0.2f;
            data.hueJitter = 0.1f;
            data.rotationJitter = 30f;
            return data;
        }

        private CoreProjectileViewPool MakePool(int seed)
        {
            var go = new GameObject("CoreProjectileVariationPool");
            _made.Add(go);
            var pool = go.AddComponent<CoreProjectileViewPool>();
            typeof(CoreProjectileViewPool).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(pool, null);
            pool.Initialize(seed);
            return pool;
        }

        private static GameObject ViewOf(CoreProjectileViewPool pool, SimEntityId id)
        {
            var dict = (System.Collections.IDictionary)typeof(CoreProjectileViewPool)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pool);
            Assert.IsTrue(dict.Contains(id), $"풀에 {id} 뷰가 없다");
            var state = dict[id];
            return (GameObject)state.GetType().GetField("view").GetValue(state);
        }

        private static Color TintOf(GameObject view)
        {
            var mpb = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            return mpb.GetColor(PropBaseColor);
        }
    }
}
