using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // enemy-detection-range unit 9 — 「발견」 표식 프리팹의 **조용한 파손**을 잡는 그물.
    //
    // 이 표식은 순수 저작물이라 컴파일도 다른 테스트도 파손을 증언하지 않는다. 깨져도 증상은
    // 「감지는 되는데 화면에 아무것도 안 뜬다」 하나뿐이고, 그건 unit 5 의 **정상 상태**(슬롯
    // 미할당 = no-op)와 구분이 안 된다. 그래서 여기서 값으로 고정한다.
    public class DetectionMarkVfxTests
    {
        private const string PrefabPath = "Assets/_Project/VFX/DetectionMark_SKELETON.prefab";

        private static GameObject LoadPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(go, "표식 프리팹이 없다: " + PrefabPath);
            return go;
        }

        private static ParticleSystem Find(GameObject root, string child)
        {
            var t = root.transform.Find(child);
            Assert.IsNotNull(t, "자식 누락: " + child);
            var ps = t.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, "ParticleSystem 누락: " + child);
            return ps;
        }

        // 정렬 대역은 **두 곳에 산다** — `BoardSortOrder` 상수(대역 문서)와 프리팹의
        // `ParticleSystemRenderer.sortingOrder`(실제 적용값). `VfxSpawner.SpawnDetectionMark` 은
        // 정렬을 런타임에 쓰지 않으므로 프리팹이 정본이고, 상수는 다음 사람이 대역을 겹치지
        // 않게 보는 지도다. 둘이 갈리면 지도가 거짓말을 시작한다(`WeaponTrailOrder` 와 같은 규약).
        [Test]
        public void 표식_정렬은_대역_상수와_일치한다()
        {
            var root = LoadPrefab();
            Assert.AreEqual(BoardSortOrder.DetectionMarkOrder,
                Find(root, "BodyFlash").GetComponent<ParticleSystemRenderer>().sortingOrder,
                "몸 플래시 링이 대역을 벗어났다");
            Assert.AreEqual(BoardSortOrder.DetectionMarkOrder + 1,
                Find(root, "Bang").GetComponent<ParticleSystemRenderer>().sortingOrder,
                "「!」가 링 위에 있어야 한다(대역 +1)");
        }

        // 머티리얼이 끊기면 마젠타이거나 아예 안 보인다 — 둘 다 「표식이 안 뜬다」로만 읽힌다.
        [Test]
        public void 표식_머티리얼이_붙어_있다()
        {
            var root = LoadPrefab();
            foreach (var name in new[] { "BodyFlash", "Bang" })
            {
                var mat = Find(root, name).GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Assert.IsNotNull(mat, name + " 머티리얼이 비었다");
                Assert.IsNotNull(mat.mainTexture, name + " 머티리얼에 텍스처가 없다");
            }
        }

        // ⚠ `VfxSpawner.SpawnDetectionMark` 이 태우는 `ConfigureOneShot` 은 emission 을
        // **t0 버스트 1발(개수 최소 4)** 로 덮어쓴다. shape 를 켜면 그 4개가 흩어져 「!」가
        // **네 개**로 보인다 — 저작 시점에는 버스트가 1이라 에디터에서 멀쩡해 보이고,
        // 라이브에서만 깨진다. 그 함정을 여기서 막는다.
        [Test]
        public void 표식은_겹쳐도_하나로_읽히게_저작됐다()
        {
            var root = LoadPrefab();
            foreach (var name in new[] { "BodyFlash", "Bang" })
            {
                var ps = Find(root, name);
                Assert.IsFalse(ps.shape.enabled,
                    name + ": shape 를 켜면 ConfigureOneShot 의 강제 4연발이 흩어진다");
                // ⚠ `MinMaxCurve` 는 모드에 따라 값이 사는 자리가 다르다 — 단일 상수는
                // `constant` 에만 있고 `constantMin` 은 0 이다. 모드를 안 보고 min/max 를
                // 비교하면 «산포 0.62» 같은 거짓 실패가 난다(이 테스트의 초판이 그랬다).
                var size = ps.main.startSize;
                float lo = size.mode == ParticleSystemCurveMode.TwoConstants ? size.constantMin : size.constant;
                float hi = size.mode == ParticleSystemCurveMode.TwoConstants ? size.constantMax : size.constant;
                Assert.LessOrEqual(hi - lo, 0.15f,
                    name + ": 크기 산포가 넓으면 4겹이 «두꺼운 띠»로 뭉친다");
            }
        }

        // ⚠ 겹침 «개수»를 정하는 것은 shape 도 크기 산포도 아니라 **저작된 `duration` 과
        // `rateOverTime`** 이다 — `ConfigureOneShot` 이 그 둘로 버스트를 계산해 덮어쓴다.
        // 인스펙터에서 `Rate over Time` 을 10 으로 올리는 것은 파티클 저작에서 가장 자연스러운
        // 동작인데, 그러면 버스트가 4 → 10 이 되어 **가산 링 밝기가 2.5배** 뛰고 「!」의 구운
        // 어두운 림이 10겹으로 뭉친다. 그런데 위 두 테스트는 **전부 초록**이다.
        //
        // 산식을 여기 복제하지 않는다(정의가 두 벌이 되는 것이 이 프로젝트의 반복 결함).
        // 진짜 함수를 태워 **결과**를 읽는다.
        [Test]
        public void 스포너가_강제하는_버스트는_최소값_4에_머문다()
        {
            var mi = typeof(VfxSpawner).GetMethod("ConfigureOneShot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi,
                "VfxSpawner.ConfigureOneShot 이 사라졌다 — 원샷 계약이 바뀌었으면 이 테스트를 다시 쓴다");

            var go = Object.Instantiate(LoadPrefab());
            try
            {
                mi.Invoke(null, new object[] { go });
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var emission = ps.emission;
                    Assert.AreEqual(1, emission.burstCount, ps.gameObject.name + ": 버스트는 t0 1발이다");
                    Assert.AreEqual(4, (int)emission.GetBurst(0).count.constant,
                        ps.gameObject.name + ": 겹침이 4를 넘으면 가산 링이 포화하고 「!」 림이 뭉친다 " +
                        "— duration/rateOverTime 저작을 되돌리거나, 늘릴 거면 색·알파를 다시 잡아라");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        // 모바일 예산(VFX 저작 스킬: 일반 효과 상한 50). 원샷 표식이 예산을 먹을 이유가 없다.
        [Test]
        public void 표식_입자_예산을_지킨다()
        {
            var root = LoadPrefab();
            int total = 0;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) total += ps.main.maxParticles;
            Assert.LessOrEqual(total, 50, "입자 예산 초과: " + total);
        }
    }
}
