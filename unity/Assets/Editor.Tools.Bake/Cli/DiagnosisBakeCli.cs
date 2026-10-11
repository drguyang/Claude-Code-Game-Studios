// sprint-07 S1.1 · 词表/门槛 catalog 的 **batchmode 烘焙入口**(2026-10-11)
//
// 权威来源:
//   ADR-014 §二/§五(数据管线两阶段 + Addressables 单一 data-core 组)· ADR-023(构建期纪律)
//   Story 002 / 003(词条表 / 门槛定表的烘焙产物 —— Baker 早已就绪)
//   project CLAUDE.md「Unity Debugging(CLI First)」:凡 CLI 能确认的,一律自己跑,不让用户手动菜单
//
// ⚠️ 为什么需要本 wrapper:Unity `-executeMethod` 只接受**静态无参 public** 方法,而
//   `DataBakeMenu` 的两个诊断烘焙入口挂 `[MenuItem]`(同样是静态无参,但**含编译期菜单属性**;
//   batchmode 直调带 MenuItem 的方法本机实测可用,但混用会让「菜单语义」与「CLI 语义」难分)。
//   本类 = **CLI 专用薄壳**:同一 repoRoot 推导 + 同一落盘路径,**零自有逻辑**
//   (判据重复 = 第二真源;本类只做「菜单已有动作的 batchmode 可达化」)。
//
// ⚠️ 产物落盘路径与菜单逐字一致(Assets/DataCooked/*.cooked.bytes);Addressables 组条目
//   仍由「确保 data-core 组」菜单 / 现有管线维护(本类不碰组 —— gitignored 机器生成物)。
//
// ⚠️ E-13 同款硬失败:BakeValidationException 的原样上抛(不吞、不降级为警告),
//   batch log 的 ExitCode 非零即可证伪。

using System;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake.Cli
{
    /// <summary>
    /// 诊断数据集的 batchmode 烘焙入口(S1.1)—— 菜单同台机器的 CLI 可达化。
    /// <para>调用法:<c>Unity -batchmode -nographics -executeMethod
    /// DaYiJingCheng.EditorTools.Bake.Cli.DiagnosisBakeCli.BakeAll</c>。</para>
    /// </summary>
    public static class DiagnosisBakeCli
    {
        /// <summary>仓根推导(与 <see cref="DataBakeMenu"/> 同法:Assets/../..)。</summary>
        private static string RepoRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        /// <summary>
        /// 烘焙 **8 词条表 + 8 门槛定表**两份,产物落 Assets/DataCooked/。
        /// </summary>
        /// <exception cref="BakeValidationException">任一数据集校验失败 —— 聚合上抛,零降级。</exception>
        public static void BakeAll()
        {
            string repoRoot = RepoRoot();
            string cookedDir = Path.Combine(Application.dataPath, DataBakeMenu.CookedDirName);
            Directory.CreateDirectory(cookedDir);

            // ── 1. 词条表(34 行;display_词 三档 —— R-8.2 冻结表)────────────
            DiagnosisSignBaker.BakeOutput signs = DiagnosisSignBaker.BakeFromRepo(repoRoot);
            string signsPath = Path.Combine(cookedDir, DataBakeMenu.DiagnosisSignsCookedAssetName);
            File.WriteAllBytes(signsPath, signs.Cooked);
            Debug.Log(
                $"[S1.1] {DataBakeMenu.DiagnosisSignsCookedAssetName} = {signs.Cooked.Length} B, " +
                $"{signs.Rows.Count} 行, ConfigVersion = 0x{signs.ConfigVersion:X8}, path = {signsPath}");

            // ── 2. 门槛定表(skill_cap+1 档 float —— F-8.1 生成期定点求值)──────
            DiagnosisReadFloorBaker.BakeOutput floor = DiagnosisReadFloorBaker.BakeFromRepo(repoRoot);
            string floorPath = Path.Combine(cookedDir, DataBakeMenu.DiagnosisReadFloorCookedAssetName);
            File.WriteAllBytes(floorPath, floor.Cooked);
            Debug.Log(
                $"[S1.1] {DataBakeMenu.DiagnosisReadFloorCookedAssetName} = {floor.Cooked.Length} B, " +
                $"{floor.Table.Count} 档, ConfigVersion = 0x{floor.ConfigVersion:X8}, path = {floorPath}");

            AssetDatabase.Refresh();
            Debug.Log("[S1.1] 两份诊断数据集烘焙完成(batchmode 入口,零菜单交互)。");
        }
    }
}
