#!/usr/bin/env bash
#
# U0-a · a4 —— 修正/补齐两个 UTF 测试装配 asmdef
#
# 权威件:production/u0-assembly-checklist.md §1.8
#
# 背景(2026-09-22 桌面机实测):
#   1. Unity 6.3 的生成器吐的是老机制 `optionalUnityReferences: ["TestAssemblies"]`,
#      **不写** references / precompiledReferences / noEngineReferences。
#   2. Test Runner 的 PlayMode 生成按钮实测**漏建 .asmdef**,只留 PlayMode.cs
#      ⇒ 那些 .cs 掉进默认 Assembly-CSharp ⇒ `CS0246: UnityTest could not be found`。
#
# 本脚本做两件事(幂等):
#   · EditMode/EditMode.asmdef —— 改名 Sim.Contracts.Tests + 补 rootNamespace,字段照旧
#   · PlayMode/PlayMode.asmdef —— 不存在则新建;存在则只重置 name/rootNamespace/includePlatforms
#
# 刻意不做的:
#   · 不叠加 references(Sim / Sim.Codec 等)—— U0-a 用不上,归 U0-b(见 §1.8 取舍①)
#   · 不写 noEngineReferences —— 生成器没给,且测试装配本就引用 UnityEngine(见 §1.8 取舍②)
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
T="$ROOT/unity/Assets/Tests"

if [ ! -d "$ROOT/unity/Assets" ]; then
    echo "错误:$ROOT/unity/Assets 不存在 —— 先完成 a1(建 Unity 工程)。" >&2
    exit 1
fi

mkdir -p "$T/EditMode" "$T/PlayMode"

write() { printf '%s\n' "$2" > "$1"; printf '  ✓ %s\n' "${1#"$ROOT"/}"; }

# ── EditMode:Sim.Contracts.Tests ───────────────────────────────────────────
write "$T/EditMode/EditMode.asmdef" '{
    "name": "Sim.Contracts.Tests",
    "rootNamespace": "DaYiJingCheng.Tests.Unit.Sim",
    "optionalUnityReferences": [ "TestAssemblies" ],
    "includePlatforms": [ "Editor" ]
}'

# ── PlayMode:Gameplay.Tests(6.3 生成器漏建此文件)────────────────────────
write "$T/PlayMode/PlayMode.asmdef" '{
    "name": "Gameplay.Tests",
    "rootNamespace": "DaYiJingCheng.Tests.PlayMode",
    "optionalUnityReferences": [ "TestAssemblies" ],
    "includePlatforms": []
}'

echo
echo "=== a4 修正完成 ==="
echo "asmdef 清单(期望 9 行):"
find "$ROOT/unity/Assets" -name '*.asmdef' | sort

echo
echo "JSON 自检:"
python3 - "$ROOT" <<'PY'
import json, glob, sys, os
root = sys.argv[1]
bad = 0
files = sorted(glob.glob(os.path.join(root, 'unity/Assets/**/*.asmdef'), recursive=True))
for f in files:
    try:
        json.load(open(f, encoding='utf-8'))
    except Exception as e:
        print(f"  ✗ {os.path.relpath(f, root)}: {e}")
        bad += 1
print(f"  {len(files)} 个 asmdef,{bad} 个非法 JSON" + ("  ✅" if bad == 0 else "  ❌"))
PY

echo
echo "下一步:回 Unity 导入 → Console 应无 CS0246 → 进 a5(迁移种子测试)。"
