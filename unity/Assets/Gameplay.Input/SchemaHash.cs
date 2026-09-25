// Story 004 · 绑重 schema hash 稳定性(F-3.5 —— 优雅失效的判据式)
//
// 权威来源:design/gdd/input-system.md §F-3.5(全文本故事直接规格)·
//   ADR-011 Amendment A ③(hash 须含 bindingId 与三组字符串集合)·
//   AC-3-E3 ①②④⑤⑦(本故事承载;③ 归 Story 005、⑥ 归 Story 003)
//
// R = 记录元组(仅原始属性):
//   (mapName, actionName, bindingIndex, bindingId, name, path,
//    isComposite, isPartOfComposite, processors[], interactions[], groups[])
//   ⛔ 硬禁读 effective*(引擎已合并 override 的视图)与 SaveBindingOverridesAsJson 载荷
//      —— 读它们 = 把玩家改键混进 hash ⇒ 改键自我毁灭(GDD F-3.5 表 + 硬禁块)
//
// 两条不变量(F-3.5 核心,AC-E3②⑤ 守):
//   ① override 变化 ⇒ hash 不变(hash 只读资产结构,不读玩家改键)
//   ② bindingId 变化 ⇒ hash 必变(重建检测的使能性质;失配后果归 Story 005)
//
// 规范序列化(定长编码,平台/文化无关):
//   · 长度与计数 = uint32 大端 4 字节;字符串 = len ‖ UTF-8 字节(null → 空串)
//   · bindingIndex = int32 大端;bool = 1 字节(0/1)
//   · 集合 = count ‖ 元素(各 length-prefixed)按 StringComparer.Ordinal 升序
//     —— count 前缀是 GDD 明文强制(AC-E3⑦:[] 与 [""] 必须可区分)
//   · 记录 canon 间按字节序(无符号字典序)升序后拼接
//
// SchemaHash = FNV-1a-64(concat sort(R) canon(r)),输出小写 hex 16 位。
//   FNV-1a 只做 XOR + 乘,全程 ulong 无符号 ⇒ IL2CPP 无有符号溢出 UB 面
//   (GDD F-3.5 硬纪律:禁 BCL 默认哈希、禁 SplitMix64 —— 后者是 PRNG 混合器且
//    踩 ADR-012 F7 溢出 spike,已由评审撤名)。
//
// 职责边界:本类只算 hash(Save/Load 的入参由调用方算好传入 —— Story 003 已就位);
//   失配后的清空/备份/日志归 Story 005。local-only:不进 sim、不需跨机一致,但须同机稳定。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>F-3.5 的记录元组 r ∈ R —— 动作资产里一条绑重的原始属性快照(值类型,不可变)。</summary>
    /// <remarks>
    /// 字段只取原始属性(<c>path</c> 等),<b>永不含</b> <c>effective*</c> / override 载荷 ——
    /// 这是不变量①(改键不自我毁灭)的结构性保证,见 <see cref="SchemaHash"/>。
    /// </remarks>
    public readonly struct BindingSchemaRecord
    {
        /// <summary>所属 map 的原始名(<c>InputActionMap.name</c>)。</summary>
        public readonly string MapName;

        /// <summary>所属动作的原始名(<c>InputAction.name</c>)。</summary>
        public readonly string ActionName;

        /// <summary>绑定在该动作内的序号(0 基,<c>action.bindings</c> 索引)。</summary>
        public readonly int BindingIndex;

        /// <summary>绑定的持久化键(<c>InputBinding.id</c>,D 格式小写 Guid 串)—— 重建后必变。</summary>
        public readonly string BindingId;

        /// <summary>绑定原始名(<c>InputBinding.name</c>)。</summary>
        public readonly string Name;

        /// <summary>绑定原始控制路径(<c>InputBinding.path</c>;非 <c>effectivePath</c>)。</summary>
        public readonly string Path;

        /// <summary>是否复合绑重容器(<c>InputBinding.isComposite</c>)。</summary>
        public readonly bool IsComposite;

        /// <summary>是否复合绑重部件(<c>InputBinding.isPartOfComposite</c>)。</summary>
        public readonly bool IsPartOfComposite;

        /// <summary>处理器集合(原始 <c>processors</c> 串按 <c>;</c> 拆分;null/空 → 空集)。</summary>
        public readonly string[] Processors;

        /// <summary>交互集合(原始 <c>interactions</c> 串按 <c>;</c> 拆分;null/空 → 空集)。</summary>
        public readonly string[] Interactions;

        /// <summary>分组集合(原始 <c>groups</c> 串按 <c>;</c> 拆分;null/空 → 空集)。</summary>
        public readonly string[] Groups;

        /// <summary>以全部 11 个元组字段构造一条记录(测试探针与重放用)。</summary>
        public BindingSchemaRecord(
            string mapName, string actionName, int bindingIndex, string bindingId,
            string name, string path, bool isComposite, bool isPartOfComposite,
            string[] processors, string[] interactions, string[] groups)
        {
            MapName = mapName;
            ActionName = actionName;
            BindingIndex = bindingIndex;
            BindingId = bindingId;
            Name = name;
            Path = path;
            IsComposite = isComposite;
            IsPartOfComposite = isPartOfComposite;
            Processors = processors ?? Array.Empty<string>();
            Interactions = interactions ?? Array.Empty<string>();
            Groups = groups ?? Array.Empty<string>();
        }
    }

    /// <summary>F-3.5 绑重 schema hash 的唯一计算体(自实现 FNV-1a-64,零 BCL 哈希)。</summary>
    /// <remarks>
    /// 见文件头注释的权威来源与编码规格。确定性保证:规范序列化全定长编码 +
    /// 集合/记录双层 Ordinal 排序 ⇒ 同一资产两次导出逐位相等(AC-E3①),
    /// 枚举顺序漂移被排序吸收。
    /// </remarks>
    public static class SchemaHash
    {
        private const ulong FnvOffsetBasis = 0xcbf29ce484222325UL;
        private const ulong FnvPrime = 0x100000001b3UL;

        /// <summary>从动作资产构建 R —— 遍历全部 map(含 UI map,GDD F-3.5 明文)× 全部动作 × 该动作的全部绑定。</summary>
        /// <param name="asset">宿主动作资产;null 抛 <see cref="ArgumentNullException"/>。</param>
        /// <returns>记录列表(资产枚举序;排序发生在求 hash 时,此处不排序)。</returns>
        /// <remarks>
        /// bindingIndex = 绑定在 <c>action.bindings</c> 内的 0 基序号。
        /// 不被任何动作引用的孤儿绑定不在 R 内(无 actionName 可入元组,且引擎不参与求值)。
        /// </remarks>
        public static IReadOnlyList<BindingSchemaRecord> BuildRecords(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            var records = new List<BindingSchemaRecord>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    var bindings = action.bindings;   // ReadOnlyArray<InputBinding>(Utilities 命名空间,免 using 用 var)
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        InputBinding b = bindings[i];
                        records.Add(new BindingSchemaRecord(
                            map.name,
                            action.name,
                            i,
                            b.id.ToString(),
                            b.name,
                            b.path,
                            b.isComposite,
                            b.isPartOfComposite,
                            SplitList(b.processors),
                            SplitList(b.interactions),
                            SplitList(b.groups)));
                    }
                }
            }

            return records;
        }

        /// <summary>计算动作资产的 schema hash(小写 hex 16 位;null 抛 <see cref="ArgumentNullException"/>)。</summary>
        public static string ComputeSchemaHash(InputActionAsset asset)
            => ComputeSchemaHash(BuildRecords(asset));

        /// <summary>由记录集合计算 schema hash —— 记录枚举顺序不影响结果(排序吸收)。</summary>
        /// <param name="records">记录集合;null 抛 <see cref="ArgumentNullException"/>。空集合 → 空流的 FNV-1a-64 基准值。</param>
        public static string ComputeSchemaHash(IEnumerable<BindingSchemaRecord> records)
        {
            if (records == null)
                throw new ArgumentNullException(nameof(records));

            var canons = new List<byte[]>();
            foreach (BindingSchemaRecord r in records)
                canons.Add(Canon(r));
            canons.Sort(CompareBytes);

            ulong h = FnvOffsetBasis;
            unchecked
            {
                foreach (byte[] canon in canons)
                {
                    foreach (byte b in canon)
                    {
                        h ^= b;
                        h *= FnvPrime;
                    }
                }
            }

            return h.ToString("x16", CultureInfo.InvariantCulture);
        }

        // ─── 规范序列化(见文件头编码规格) ───

        private static byte[] Canon(in BindingSchemaRecord r)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                WriteString(ms, r.MapName);
                WriteString(ms, r.ActionName);
                WriteI32(ms, r.BindingIndex);
                WriteString(ms, r.BindingId);
                WriteString(ms, r.Name);
                WriteString(ms, r.Path);
                ms.WriteByte(r.IsComposite ? (byte)1 : (byte)0);
                ms.WriteByte(r.IsPartOfComposite ? (byte)1 : (byte)0);
                WriteList(ms, r.Processors);
                WriteList(ms, r.Interactions);
                WriteList(ms, r.Groups);
                return ms.ToArray();
            }
        }

        private static void WriteList(System.IO.MemoryStream ms, string[] elements)
        {
            WriteU32(ms, (uint)elements.Length);   // 元素计数前缀(GDD F-3.5 强制;AC-E3⑦)
            Array.Sort(elements, StringComparer.Ordinal);
            foreach (string e in elements)
                WriteString(ms, e);
        }

        private static void WriteString(System.IO.MemoryStream ms, string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s ?? string.Empty);
            WriteU32(ms, (uint)utf8.Length);
            ms.Write(utf8, 0, utf8.Length);
        }

        private static void WriteU32(System.IO.MemoryStream ms, uint v)
        {
            ms.WriteByte((byte)(v >> 24));   // 大端:高低位序与平台字节序无关
            ms.WriteByte((byte)(v >> 16));
            ms.WriteByte((byte)(v >> 8));
            ms.WriteByte((byte)v);
        }

        private static void WriteI32(System.IO.MemoryStream ms, int v)
            => WriteU32(ms, unchecked((uint)v));

        private static int CompareBytes(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                int d = a[i] - b[i];
                if (d != 0)
                    return d < 0 ? -1 : 1;
            }
            return a.Length.CompareTo(b.Length);
        }

        private static string[] SplitList(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return Array.Empty<string>();
            return raw.Split(InputBinding.Separator);
        }
    }
}
