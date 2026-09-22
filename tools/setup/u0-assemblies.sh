#!/usr/bin/env bash
#
# U0-a · a3 —— 落 7 个 asmdef + Sim.Codec/AssemblyInfo.cs
#
# 权威件:production/u0-assembly-checklist.md §1.1–§1.7
# 承:ADR-025 §①(六装配具名清单)· ADR-017 §二(门 A)
#
# 用法(在仓库根跑,或任意位置 —— 路径按脚本自身定位):
#     bash tools/setup/u0-assemblies.sh
#
# 幂等:重复运行会把文件覆盖成同一内容,不会重复追加。
#
# ⚠️ 本脚本只处理 §1.1–§1.7(7 个 asmdef)。
#    测试装配(§1.8)不在此列 —— 它必须用 Unity 的
#    Create → Testing → Tests Assembly Folder 生成器,不能手写。
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
A="$ROOT/unity/Assets"

if [ ! -d "$A" ]; then
    cat >&2 <<MSG
错误:$A 不存在。

请先完成 U0-a 的 a1 —— 在 <仓库根>/unity 建好 Unity 工程(空白 URP 模板),
然后重跑本脚本。
MSG
    exit 1
fi

mkdir -p "$A/Sim" \
         "$A/Sim.Contracts" \
         "$A/Sim.Codec" \
         "$A/Gameplay.Presentation" \
         "$A/Gameplay.UI" \
         "$A/Editor.Tools.Level" \
         "$A/Editor.Tools.Kindgen"

write() { printf '%s\n' "$2" > "$1"; printf '  ✓ %s\n' "${1#"$ROOT"/}"; }

# ── §1.1 Sim(门 A:noEngineReferences = true)──────────────────────────────
write "$A/Sim/Sim.asmdef" '{
    "name": "Sim",
    "rootNamespace": "DaYiJingCheng.Sim",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}'

# ── §1.2 Sim.Contracts(门 A)───────────────────────────────────────────────
write "$A/Sim.Contracts/Sim.Contracts.asmdef" '{
    "name": "Sim.Contracts",
    "rootNamespace": "DaYiJingCheng.Sim.Contracts",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}'

# ── §1.3 Sim.Codec(门 A)+ InternalsVisibleTo ─────────────────────────────
write "$A/Sim.Codec/Sim.Codec.asmdef" '{
    "name": "Sim.Codec",
    "rootNamespace": "DaYiJingCheng.Sim.Codec",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}'

write "$A/Sim.Codec/AssemblyInfo.cs" 'using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]'

# ── §1.4 Gameplay.Presentation(呈现层,可引 UnityEngine)──────────────────
write "$A/Gameplay.Presentation/Gameplay.Presentation.asmdef" '{
    "name": "Gameplay.Presentation",
    "rootNamespace": "DaYiJingCheng.Gameplay.Presentation",
    "references": [ "Sim.Contracts" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}'

# ── §1.5 Gameplay.UI(单向依赖 Presentation;焦点单栈门的编译期表达)───────
write "$A/Gameplay.UI/Gameplay.UI.asmdef" '{
    "name": "Gameplay.UI",
    "rootNamespace": "DaYiJingCheng.Gameplay.UI",
    "references": [ "Sim.Contracts", "Gameplay.Presentation" ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}'

# ── §1.6 / §1.7 Editor 工具族(不进构建)──────────────────────────────────
write "$A/Editor.Tools.Level/Editor.Tools.Level.asmdef" '{
    "name": "Editor.Tools.Level",
    "rootNamespace": "DaYiJingCheng.EditorTools.Level",
    "references": [],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}'

write "$A/Editor.Tools.Kindgen/Editor.Tools.Kindgen.asmdef" '{
    "name": "Editor.Tools.Kindgen",
    "rootNamespace": "DaYiJingCheng.EditorTools.Kindgen",
    "references": [],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}'

echo
echo "=== a3 完成 ===  应见 8 行:"
find "$A" \( -name '*.asmdef' -o -name 'AssemblyInfo.cs' \) | sort
echo
echo "下一步:回 Unity 窗口让它导入,并检查 Console 无报错。"
echo "提醒:'references' 若被 Unity 改写成 \"GUID:...\" 是正常行为,勿改回。"
