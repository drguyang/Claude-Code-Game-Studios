// `TD-build-03` 的解除动作 ①(用户 2026-11-05 裁定「按①把着色器那条也修了」)的**作者态生成器**。
//
// 病因:`CapsulePatientVisualFactory` 原来用 `Shader.Find("Universal Render Pipeline/Lit")`
//   在**运行期**找着色器 —— 而着色器只被代码字符串引用、没有任何资产引用它 ⇒
//   player 构建把它当「未使用」剥离 ⇒ 打包版 `Shader.Find` 恒 null ⇒ 占位材质建不出来
//   (实测日志 2489 条 `[patient-visual] 找不到 URP Lit 也找不到 Standard`)。
//
// 解药:把着色器**钉进一个资产**。资产住 `Resources/` ⇒ 必进包;其依赖(URP Lit 着色器)
//   随之必进包。运行期改为 `Resources.Load<Material>`(见 `CapsulePatientVisualFactory`)。
//
// ⚠️ **本工具只做「首次创建」,已存在即失败**(fail-closed,不静默覆盖):
//   资产的**唯一真源 = `.mat` 文件本身**。若允许覆盖,本文件里的字面量就成了第二真源,
//   将来改色会分叉。要改颜色请直接编辑 `.mat`(或先手工删掉再重跑本工具)。
//
// 用法(仓库 CLAUDE.md 的 CLI 约定):
//   cd unity && "$UNITY" -batchmode -quit -nographics -projectPath "$PWD" \
//     -executeMethod DaYiJingCheng.EditorTools.Bake.Cli.CreatePatientPlaceholderMaterialCli.Create \
//     -logFile Logs/create-patient-mat.log

using System.IO;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake.Cli
{
    /// <summary>一次性生成病人占位材质资产(`Resources/PatientPlaceholderMaterial.mat`)。
    /// <para>⚠️ **不是产品的一部分**:运行期从不调用它。跑过一次后资产入库,本类只是可复算的出处。</para>
    /// </summary>
    public static class CreatePatientPlaceholderMaterialCli
    {
        /// <summary>资产落点(仓库相对路径)。</summary>
        public const string AssetPath =
            "Assets/Gameplay.Presentation/Patients/Resources/PatientPlaceholderMaterial.mat";

        /// <summary>占位色 —— 与 `CapsulePatientVisualFactory` 原 `NeutralGrey` 同值
        /// `(0.7, 0.7, 0.7)`,零语义(不映射体征 / 病种 / 九态)。</summary>
        public static readonly Color PlaceholderColor = new Color(0.7f, 0.7f, 0.7f);

        /// <summary>batchmode 入口。失败 ⇒ 非零退出码。</summary>
        public static void Create()
        {
            if (File.Exists(AssetPath))
            {
                Debug.LogError(
                    $"[patient-placeholder-mat] 资产已存在,拒绝覆盖:{AssetPath}。" +
                    "本工具只负责首次创建 —— 颜色真源就是那个 .mat,要改请直接改它。");
                EditorApplication.Exit(2);
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError(
                    "[patient-placeholder-mat] 编辑器里找不到 URP Lit —— 工程的 URP 包缺失或被改名。" +
                    "不得用 Standard 顶替:URP player 构建会剥离 Standard,那正是本债的病根。");
                EditorApplication.Exit(3);
                return;
            }

            string dir = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var mat = new Material(shader) { name = "PatientPlaceholderMaterial" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", PlaceholderColor);
            else mat.color = PlaceholderColor;

            AssetDatabase.CreateAsset(mat, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 回读校验:创建不等于可加载。资源路径 = Resources 之后的那段(去扩展名)。
            var loaded = Resources.Load<Material>("PatientPlaceholderMaterial");
            if (loaded == null || loaded.shader == null)
            {
                Debug.LogError(
                    "[patient-placeholder-mat] 资产已写入但回读失败 —— " +
                    "Resources.Load(\"PatientPlaceholderMaterial\") 拿不到材质或 shader 为 null。");
                EditorApplication.Exit(4);
                return;
            }

            Debug.Log(
                $"[patient-placeholder-mat] 已创建 {AssetPath} · shader={loaded.shader.name} · " +
                $"baseColor={loaded.GetColor("_BaseColor")}");
        }
    }
}
