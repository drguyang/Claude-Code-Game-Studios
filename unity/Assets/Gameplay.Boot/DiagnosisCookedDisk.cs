// sprint-07 S1.2 · 诊断烘焙产物的**磁盘兜底装载**(EditMode / 测试装配用 · 2026-10-11)
//
// 权威来源:
//   ADR-014 §五(Addressables 生产装载路径 = 乙案)+ 用户 2026-10-11 裁定
//   「乙案 + EditMode 直读兜底」:Addressables 组 gitignored ⇒ 只在生产(BootRoot)显式走;
//   测试 / EditMode 无 Addressables 运行时 ⇒ 本类从 Assets/DataCooked 直读字节。
//   E-13 同款硬失败:任何缺失 / 损坏 ⇒ 显式 throw,零默认值、零静默降级。
//
// ⚠️ 本类**不是生产装载路径** —— 生产唯一 = AddressablesDataProvider(经 BootRoot 的
//   DataCorePreloader)。本类只服务「组合根在测试环境的真产物读取」:
//   · 读的是**同一份烘焙产物**(bake 菜单 / DiagnosisBakeCli 产物),零合成、零 Fake;
//   · 解码器是同一个 CookedCodec / DiagnosisSignCookedCodec(与 bake 写方镜像);
//   ∴ 无第二真源:字节产自 bake,解码自 codec,本类只补「测试环境无 Addressables」的读路径。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 诊断两份 cooked 产物的磁盘兜底装载(EditMode / 测试装配专用)。
    /// <para>生产路径 = <c>AddressablesDataProvider</c>(乙案);本类只在
    /// <see cref="CompositionRoot"/> 无 provider 注入时被调(测试装配)。</para>
    /// </summary>
    public static class DiagnosisCookedDisk
    {
        /// <summary>DataCooked 目录(相对 Assets;与 Editor.Tools.Bake.DataBakeMenu.CookedDirName 同值)。</summary>
        private const string CookedDirName = "DataCooked";

        /// <summary>DataCooked 目录绝对路径(测试比对用 —— 产物漂移门 C3-B3)。</summary>
        public static string CookedDirectory() => System.IO.Path.Combine(
            UnityEngine.Application.dataPath, CookedDirName);

        /// <summary>词条表产物名(与 AddressablesDataProvider.DiagnosisSignsAddress 同值)。</summary>
        private const string SignsAssetName = "diagnosis_signs.cooked.bytes";

        /// <summary>门槛定表产物名(与 AddressablesDataProvider.DiagnosisReadFloorAddress 同值)。</summary>
        private const string ReadFloorAssetName = "diagnosis_read_floor.cooked.bytes";

        /// <summary>病名册词表产物名(sprint-08 S1.1 件 D;与 provider 同值)。</summary>
        private const string LexiconAssetName = "case_judgment_lexicon.cooked.bytes";

        /// <summary>读词条表(34 行;硬失败:文件缺失 / 解码失败都抛)。</summary>
        public static DiagnosisSignDataSet LoadSigns()
        {
            byte[] bytes = ReadAll(SignsAssetName);
            try
            {
                return DiagnosisSignCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][disk] {SignsAssetName} 解码失败(兜底装载路径)—— 启动/装配期硬失败:{ex.Message}",
                    ex);
            }
        }

        /// <summary>读门槛定表(61 档;硬失败同上)。</summary>
        public static DiagnosisReadFloorDataSet LoadReadFloor()
        {
            byte[] bytes = ReadAll(ReadFloorAssetName);
            try
            {
                return DiagnosisReadFloorCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][disk] {ReadFloorAssetName} 解码失败(兜底装载路径)—— 启动/装配期硬失败:{ex.Message}",
                    ex);
            }
        }

        /// <summary>读病名册词表(sprint-08 S1.1 件 D;硬失败同上)。</summary>
        public static JudgmentLexiconDataSet LoadJudgmentLexicon()
        {
            byte[] bytes = ReadAll(LexiconAssetName);
            try
            {
                return JudgmentLexiconCookedCodec.Read(bytes);
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException(
                    $"[E-13][disk] {LexiconAssetName} 解码失败(兜底装载路径)—— 启动/装配期硬失败:{ex.Message}",
                    ex);
            }
        }

        /// <summary>读产物字节(Application.dataPath 直读 —— 兜底路径零 Addressables)。</summary>
        private static byte[] ReadAll(string assetName)
        {
            string path = Path.Combine(Application.dataPath, CookedDirName, assetName);
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    $"[E-13][disk] 烘焙产物不存在:{path} —— 须先跑 bake" +
                    "(DataBakeMenu 或 DiagnosisBakeCli.BakeAll)。拒绝返回空数据集。");
            return File.ReadAllBytes(path);
        }
    }
}
