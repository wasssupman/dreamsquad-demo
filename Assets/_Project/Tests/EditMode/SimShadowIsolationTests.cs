// battle-sim-extraction unit 18-D — **I2 검출기**(계획서 rev 3 이 여기에 배정).
//
// I2: `Sim/Lib/{Units,Movement,Combat,Effects}/**` 를 부르는 프로덕션 코드는 **0** 이다.
// 예외는 18-K(그림자 무장) 하나.
//
// **왜 이 검출기가 따로 필요한가**: 기존 `SimEngineIndependenceTests` 는 *sim 이 무엇을
// 참조하나* 만 본다(방향 하나). I2 는 **역방향**이고, 그걸 보는 장치가 지금까지 없었다.
//
// **왜 지금 세우나**: I1 의 논증은 *"골든이 초록인 이유는 파일을 안 건드렸기 때문"* 이다.
// 누군가 그림자를 프로덕션 경로에 배선하는 순간 그 논증이 무너지는데, **무너졌다는 사실을
// 알려줄 장치가 없다** — 골든은 여전히 초록일 수 있고(신 코드가 아직 아무것도 안 바꿔도),
// 그러면 A/B 의 기준선이 조용히 오염된다. T1 이 6조각 남은 지금이 값을 하는 시점이다.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wassup.Tests.EditMode
{
    public class SimShadowIsolationTests
    {
        /// 그림자 맥락 네임스페이스. `Wassup.Sim`(Core) · `.Contracts` · `.Match` · `.Math` 는
        /// **허용**이다 — 이미 프로덕션이 쓰는 졸업분(파사드·규칙·점수 산식)이다.
        private static readonly string[] ShadowNamespaces =
        {
            "Wassup.Sim.Units",
            "Wassup.Sim.Movement",
            "Wassup.Sim.Combat",
            "Wassup.Sim.Effects",
            // ⚠ 18-K/5 — **조립 지점은 네임스페이스가 `Wassup.Sim`(Core)이라 위 스캔에 안 걸린다.**
            //   그런데 `SimRuntime` 은 정의상 8 클러스터를 전부 끌어오므로, 프로덕션이 이 이름
            //   하나만 적어도 그림자가 통째로 무장된다. 맥락 스캔의 구멍이라 이름으로 막는다.
            //   (`Wassup.Sim` 전체를 금지할 수는 없다 — 파사드·규칙·점수 산식은 이미 졸업분이다.)
            "SimRuntime",
        };

        /// <summary>
        /// 무장 예외. **18-K 가 그림자를 켜는 지점 하나만** 여기 들어온다.
        /// ⚠ 항목을 추가하는 것은 I2 를 그만큼 포기하는 것이다 — 계획서 §불변식도 함께 고칠 것.
        /// </summary>
        private static readonly HashSet<string> ArmingExceptions = new HashSet<string>();

        private static string ScriptsRoot => Path.Combine(Application.dataPath, "_Project", "Scripts");
        private static string SimRoot => Path.Combine(ScriptsRoot, "Sim");

        /// 프로덕션 = `Scripts/**` 에서 `Scripts/Sim/**` 를 뺀 것. 테스트는 애초에 포함되지 않는다
        /// (`Assets/_Project/Tests/` 는 이 루트 밖이다) — 오라클 복제가 신 sim 을 부르는 것은 정상이다.
        private static IEnumerable<string> ProductionFiles()
        {
            string sim = SimRoot + Path.DirectorySeparatorChar;
            foreach (string path in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
                if (!path.StartsWith(sim, System.StringComparison.OrdinalIgnoreCase))
                    yield return path;
        }

        /// `//` 이후를 잘라낸다. 문자열 리터럴 안의 `//` 까지 보지는 않는다 — 이 게이트에서는
        /// 과탐이 미탐보다 낫다(주석에 네임스페이스를 적어 두는 건 흔하고, 그건 잘라내야 한다).
        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            return i >= 0 ? line.Substring(0, i) : line;
        }

        [Test]
        public void NoProductionCode_ReferencesShadowContexts()
        {
            var hits = new List<string>();

            foreach (string path in ProductionFiles())
            {
                string name = Path.GetFileName(path);
                if (ArmingExceptions.Contains(name)) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    if (code.Length == 0) continue;

                    foreach (string ns in ShadowNamespaces)
                    {
                        // `using Wassup.Sim.Effects;` · `Wassup.Sim.Effects.CcEffect` 둘 다 잡는다.
                        if (!code.Contains(ns)) continue;
                        hits.Add($"{name}:{i + 1}  {ns}  |  {code.Trim()}");
                    }
                }
            }

            Assert.IsEmpty(hits,
                "I2 위반 — 프로덕션 코드가 그림자 맥락을 참조한다. 그림자는 18-K 가 무장하기 " +
                "전까지 **아무도 부르지 않아야** 한다(그래야 I1 의 '건드린 파일이 없다' 가 " +
                "A/B 기준선의 근거로 성립한다):\n  " + string.Join("\n  ", hits));
        }

        [Test]
        public void Detector_ActuallyScansSomething()
        {
            // 검출기가 빈 목록을 훑으면서 초록을 내는 것을 막는다 — 이 spec 이 반복해서
            // 경계하는 "조용한 no-op" 모양이다(경로 오타 하나면 이 테스트는 영원히 초록이다).
            int count = 0;
            foreach (string _ in ProductionFiles()) count++;
            Assert.Greater(count, 100, $"프로덕션 파일 {count}개만 스캔됐다 — 경로가 틀렸을 수 있다.");
        }

        [Test]
        public void ShadowContextFolders_ExistUnderTheGraduatedAsmdef()
        {
            // I3 의 보조 관측점: 신 맥락 폴더는 `Sim/Lib/` **아래**여야 컴파일러가 엔진 참조를
            // 막는다. 형제로 올라가면 `Wassup.Runtime` 소속이 되어 게이트가 사라진다(critic H2).
            string lib = Path.Combine(SimRoot, "Lib");
            Assert.IsTrue(File.Exists(Path.Combine(lib, "Wassup.Sim.asmdef")),
                "졸업 asmdef 가 Sim/Lib 에 있어야 한다.");

            foreach (string ctx in new[] { "Units", "Combat", "Effects" })
                Assert.IsTrue(Directory.Exists(Path.Combine(lib, ctx)),
                    $"맥락 폴더 {ctx} 가 Sim/Lib 아래에 없다 — asmdef 밖이면 I3 가 집행되지 않는다.");
        }
    }
}
