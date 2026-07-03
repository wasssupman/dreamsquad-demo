using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    // 파티클 다축 MinMaxCurve 모드 일치 불변식.
    //
    // Unity 는 velocityOverLifetime(x/y/z·orbital·orbitalOffset)/forceOverLifetime/
    // limitVelocityOverLifetime(separateAxes) 의 축별 모드가 섞이면 재생 프레임마다
    // "Particle ... curves must all be in the same mode" 에러를 뿜는다 (에디터가 저작을
    // 막아주지 않아 데이터로만 존재하다 런타임에 터진다). HazardVisual_Poison 플러딩
    // 사고(63c7240)의 재발 방지 — 신규 VFX 저작/벤더 임포트가 이 클래스의 결함을
    // 들여오면 여기가 빨개진다.
    public class ParticleCurveModeConsistencyTests
    {
        [Test]
        public void AllPrefabParticleSystems_MultiAxisCurveModes_AreConsistent()
        {
            var offenders = new StringBuilder();
            int scanned = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue; // 패키지 제외 — 우리가 못 고치는 영역
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    scanned++;
                    var issues = new List<string>();

                    var v = ps.velocityOverLifetime;
                    if (v.enabled)
                    {
                        if (!SameMode(v.x, v.y, v.z)) issues.Add("Velocity.xyz");
                        if (!SameMode(v.orbitalX, v.orbitalY, v.orbitalZ)) issues.Add("Velocity.orbital");
                        if (!SameMode(v.orbitalOffsetX, v.orbitalOffsetY, v.orbitalOffsetZ)) issues.Add("Velocity.orbitalOffset");
                    }

                    var f = ps.forceOverLifetime;
                    if (f.enabled && !SameMode(f.x, f.y, f.z)) issues.Add("Force.xyz");

                    var lim = ps.limitVelocityOverLifetime;
                    if (lim.enabled && lim.separateAxes && !SameMode(lim.limitX, lim.limitY, lim.limitZ))
                        issues.Add("LimitVelocity.xyz");

                    if (issues.Count > 0)
                        offenders.AppendLine($"{path} :: {ps.gameObject.name} [{string.Join(", ", issues)}]");
                }
            }

            Assert.Greater(scanned, 0, "no particle systems scanned — asset database empty?");
            Assert.AreEqual(0, offenders.Length,
                "다축 커브 모드가 섞인 ParticleSystem 발견 — 재생 시 콘솔 에러 플러딩을 유발한다. " +
                "축별 모드를 통일하라 (상수 축은 min==max TwoConstants 승격이 시각 동일·안전):\n" + offenders);
        }

        private static bool SameMode(ParticleSystem.MinMaxCurve a, ParticleSystem.MinMaxCurve b, ParticleSystem.MinMaxCurve c)
        {
            return a.mode == b.mode && b.mode == c.mode;
        }
    }
}
