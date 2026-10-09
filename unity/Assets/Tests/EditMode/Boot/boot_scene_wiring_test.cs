// M2 接线轮阶段 1 装配轮 · 测试面 T5:Boot 场景挂载 + Addressable 条目的资产级门测。
//
// 两条判据(逐条可证伪;EditMode 无法加载场景 ⇒ 走**资产文本扫描**,与
// camera_presentation_discipline_test 的 AudioListener 计数同纪律):
//   ① Boot.unity 中 BootRoot 脚本 guid 恰出现 1 次(挂载点唯一 —— 0 = 掉挂载,≥2 = 重复挂);
//   ② Addressables 条目:AddressableAssetsData/ 不在(本地工作配置,gitignored)⇒ Ignore 不借绿;
//      在则同一 group 资产文件内 World.unity 的 guid 与 `m_Address: world` 共现
//      (BootRoot 加载键 "world" 与条目地址同源,缺一 = 运行期 LoadSceneAsync 硬失败)。

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>Boot 场景挂载与 Addressable 注册的资产级测试。</summary>
    public class BootSceneWiringTest
    {
        /// <summary>BootRoot.cs.meta 的 guid(挂载判据键)。</summary>
        private const string BootRootScriptGuid = "ca92175fcc4d7a740300f9d71c70cbc8";

        /// <summary>World.unity.meta 的 guid(Addressable 条目判据键)。</summary>
        private const string WorldSceneGuid = "4e3fa55256781274c119ea8a2b51c1cd";

        /// <summary>仓库根(Application.dataPath = &lt;root&gt;/unity/Assets ⇒ 上两级)——
        /// 与 camera_presentation_discipline_test 的工程根解析同款惯例。</summary>
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void test_bootScene_bootRootGuidAppearsExactlyOnce()
        {
            // Arrange:资产路径(文本读取,不进 PlayMode)
            string scenePath = Path.Combine(RepoRoot, "unity", "Assets", "Scenes", "Boot.unity");
            Assert.IsTrue(File.Exists(scenePath),
                $"Boot.unity 不在(启动序宿主的挂载体):{scenePath}");
            string text = File.ReadAllText(scenePath);

            // Act:guid 出现次数
            int count = Regex.Matches(text, BootRootScriptGuid).Count;

            // Assert:恰 1 次
            Assert.AreEqual(1, count,
                "Boot.unity 中 BootRoot 脚本 guid 须恰出现 1 次(0 = 挂载丢失,≥2 = 重复挂载)" +
                $"—— 实得 {count}");
        }

        [Test]
        public void test_addressables_worldEntry_addressAndGuidCooccur()
        {
            // Arrange:AddressableAssetsData 是**本地工作配置**(可能 gitignored)⇒ 不在则 Ignore
            string addrDir = Path.Combine(RepoRoot, "unity", "Assets", "AddressableAssetsData");
            if (!Directory.Exists(addrDir))
                Assert.Ignore("本地工作配置不在(gitignored)");

            string groupsDir = Path.Combine(addrDir, "AssetGroups");
            Assert.IsTrue(Directory.Exists(groupsDir),
                $"AssetGroups/ 缺失 —— 条目扫描面不存在(拒以空集冒充绿):{groupsDir}");

            // Act:逐 group 资产找「同文件共现」(guid 与地址必须同属一条目,各在一处不算)
            bool cooccurred = false;
            foreach (string f in Directory.GetFiles(groupsDir, "*.asset", SearchOption.AllDirectories))
            {
                string t = File.ReadAllText(f);
                if (t.Contains(WorldSceneGuid) && t.Contains("m_Address: world"))
                {
                    cooccurred = true;
                    break;
                }
            }

            // Assert:共现成立
            Assert.IsTrue(cooccurred,
                "Addressable 须在同一 group 资产内共现 World.unity guid(" + WorldSceneGuid +
                ")与 `m_Address: world`(BootRoot.LoadSceneAsync(\"world\") 的加载键)—— " +
                "缺任一侧 = 运行期启动序第 3 步硬失败");
        }
    }
}
