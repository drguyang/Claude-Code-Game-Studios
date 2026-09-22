#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""kindgen —— SimEvent.Kind 单一真源生成器 + 构建期断言(ADR-024 §⑤)。

输入 : design/registry/entities.yaml(`SimEvent.Kind.*` 34 支,stream/author/payload_schema 必填)
       unity/Assets/Sim.Contracts/EventKind.cs(C# 侧镜像)
输出 : unity/Assets/Sim/StreamRouting.g.cs(Kind→StreamId 纯函数 switch)

断言(任一失败 = 非零退出 = 构建失败):
  A1 每支 Kind 恰有一个合法 stream ∈ {history, case, world}(禁双家/无家)
  A2 payload_schema 的**类型标注 ∈ 整数域白名单**{i32,i64,u16,u64,枚举,格 WorldPos,
     具名整数域类型};扫描 float/double 字样(否定语境「无/禁/非/零 float」豁免)
        ⚠️ 严格形(逐字段类型化)未实现 —— registry 的 payload_schema 本身是散文,
        ADR-024 §①「禁从散文解析」的机读字段化是登记在案的未兑现要求(见本文件头注)
  A3 Kind 名无重复
  A4 author 必填且首整数 ∈ 已登记系统号(systems-index 表实测集)
  A5 registry ↔ EventKind.cs 成员双向差集归零

⚠️ 口径登记:ADR-024 §⑤ 设想 kindgen = 编辑期 .NET 工具;本实现 = 纯 Python(本机
   无 .NET SDK / 无编译器实测面,断言逻辑不依赖引擎,Unity 侧零引用)。CI 挂接归
   ADR-012 矩阵故事;`--check` 即失败非零退出,天然可作构建步骤。

用法: python3 tools/kindgen/kindgen.py [--check]
  默认生成 + 断言;--check 只断言不写文件(比对既有产物的再生成一致性由 CI 另管)。
"""

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
REGISTRY = REPO / "design" / "registry" / "entities.yaml"
EVENTKIND_CS = REPO / "unity" / "Assets" / "Sim.Contracts" / "EventKind.cs"
OUT_CS = REPO / "unity" / "Assets" / "Sim" / "StreamRouting.g.cs"
SYSTEMS_INDEX = REPO / "design" / "gdd" / "systems-index.md"

STREAM_MAP = {"history": "History", "case": "Case", "world": "World"}
INT_TYPE_TOKENS = ("i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64")
ARRAY_UNBOUNDED_OK = ("上界", "≤", "MAX", "cap", "有界")   # [] 出现须在 schema 里找到界证据


def fail(report, msg):
    report.append("FAIL  " + msg)


def parse_registry():
    import yaml
    doc = yaml.safe_load(REGISTRY.read_text(encoding="utf-8"))
    kinds = []
    for e in doc["constants"]:
        name = str(e.get("name", ""))
        if not name.startswith("SimEvent.Kind."):
            continue
        kinds.append({
            "kind": name.split(".")[-1],
            "stream": e.get("stream"),
            "author": e.get("author"),
            "payload": str(e.get("payload_schema", "")),
            "constraint": str(e.get("constraint", "")),
        })
    return kinds


def parse_eventkind_cs():
    text = EVENTKIND_CS.read_text(encoding="utf-8")
    body = text.split("public enum EventKind", 1)[1]
    body = body.split("}", 1)[0]
    return [m.group(1) for m in re.finditer(r"^\s{8}(\w+),", body, re.M)]


def registered_system_numbers():
    """systems-index 表第一列的系统号实测集(纯文本,非散文裁决)。"""
    nums = set()
    for line in SYSTEMS_INDEX.read_text(encoding="utf-8").splitlines():
        m = re.match(r"^\|\s*(\d+[a-z]?)\s*\|", line)
        if m:
            nums.add(m.group(1))
    return nums


def a1_streams(kinds, rep):
    for k in kinds:
        if k["stream"] not in STREAM_MAP:
            fail(rep, f"A1 {k['kind']}: stream={k['stream']!r} 非法/缺失")


def a2_payload_domain(kinds, rep):
    for k in kinds:
        ps = k["payload"]
        # float/double 扫描:否定语境(前 4 字内含 无/禁/非/零/不)豁免
        for m in re.finditer(r"[Ff]loat|[Dd]ouble", ps):
            ctx = ps[max(0, m.start() - 4):m.start()]
            if not re.search(r"[无禁非零不]", ctx):
                fail(rep, f"A2 {k['kind']}: 载荷出现非否定语境的 float/double —— {ps!r}")
        # 数组字段须有界证据(X-2 定长缓冲裁定的一致性)。
        # 界证据允许写在 payload_schema 或 constraint 任一(Craft 的界在 constraint:
        # 「点火行为率 ≤ 每玩家每 tick 1」)⇒ 两处合看;不放宽 token 集本身。
        bound_ctx = ps + k.get("constraint", "")
        if "[]" in bound_ctx and not any(t in bound_ctx for t in ARRAY_UNBOUNDED_OK):
            fail(rep, f"A2 {k['kind']}: 数组字段无界证据(payload_schema+constraint 均无 ∈/≤/MAX/cap/上界/有界)")


def a3_unique_names(kinds, rep):
    names = [k["kind"] for k in kinds]
    dup = {n for n in names if names.count(n) > 1}
    if dup:
        fail(rep, f"A3 registry 重名: {sorted(dup)}")


def a4_authors(kinds, rep):
    registered = registered_system_numbers()
    for k in kinds:
        author = str(k["author"] or "")
        m = re.match(r"^\s*(\d+[a-z]?)", author)
        if not m:
            fail(rep, f"A4 {k['kind']}: author 缺失或非系统号开头 —— {author!r}")
        elif m.group(1) not in registered:
            fail(rep, f"A4 {k['kind']}: author 系统号 {m.group(1)} ∉ systems-index 登记集")


def a5_bidirectional(kinds, rep):
    reg = {k["kind"] for k in kinds}
    cs = set(parse_eventkind_cs())
    if reg - cs:
        fail(rep, f"A5 有 registry 无 C#: {sorted(reg - cs)}")
    if cs - reg:
        fail(rep, f"A5 有 C# 无 registry: {sorted(cs - reg)}")


def render(kinds):
    order = {"history": 0, "case": 1, "world": 2}
    rows = sorted(kinds, key=lambda k: (order.get(k["stream"], 9), k["kind"]))
    arms = "\n".join(
        f"            EventKind.{k['kind']} => StreamId.{STREAM_MAP[k['stream']]},"
        for k in rows
    )
    counts = {s: sum(1 for k in kinds if k["stream"] == s) for s in STREAM_MAP}
    return f"""// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本文件由 tools/kindgen/kindgen.py 生成 —— **勿手改**。
//    真源 = design/registry/entities.yaml(ADR-024 §①);改 Kind 的合法顺序 =
//    先改 registry,再重跑 kindgen。A1–A5 断言在生成前执行,产物存在即断言已过。
//
// 命名口径偏离登记(承 b1a 文件头):ADR-024 生成物形状原文写 `SimEvent.Kind.X`
//   前缀,但 SimEvent 是 struct,「结构体成员访问其嵌套类型」在 C# 非法(编译期),
//   且 b1a 已裁 EventKind 住命名空间作用域(CS0102 拆除,见 SimEvent.cs 头注)。
//   故本产物用 `EventKind.X` —— 成员名与 registry 的 A5 差集零不受影响。
//   `throw new BuildContractException(kind)` 的该类型全案无定义件可依,
//   以 InvalidOperationException 承位;若后续 ADR 具名该异常,替换属机械改。
//
// 生成参数: {counts['history']} history / {counts['case']} case / {counts['world']} world = {len(kinds)} 支
// ─────────────────────────────────────────────────────────────────────────────

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{{
    /// <summary>Kind → StreamId 纯函数路由(ADR-008 §一 · ADR-024)。白名单穷举 ⇒ default 运行期不可达。</summary>
    public static class StreamRouting
    {{
        public static StreamId Of(EventKind kind) => kind switch
        {{
{arms}
            _ => throw new System.InvalidOperationException(
                "不可达:Kind 白名单由 kindgen 穷举(ADR-024)。抵达即 registry 与产物脱钩 —— 重跑 kindgen。"),
        }};
    }}
}}
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="只断言,不写文件")
    args = ap.parse_args()

    kinds = parse_registry()
    rep = []
    if len(kinds) != 34:
        fail(rep, f"kindgen: registry Kind 总数 = {len(kinds)},须 34(ADR-024 V-2 条目数)")
    a1_streams(kinds, rep)
    a2_payload_domain(kinds, rep)
    a3_unique_names(kinds, rep)
    a4_authors(kinds, rep)
    a5_bidirectional(kinds, rep)

    hard = [r for r in rep if r.startswith("FAIL")]
    for line in rep:
        print(line)
    if hard:
        print(f"\nkindgen: {len(hard)} 条断言失败 —— 构建失败(ADR-024 §⑤)")
        sys.exit(1)

    if args.check:
        print("kindgen: A1–A5 全过(--check 不写文件)")
        return

    OUT_CS.write_text(render(kinds), encoding="utf-8")
    print(f"kindgen: A1–A5 全过 → 写出 {OUT_CS.relative_to(REPO)}({len(kinds)} 支 switch)")


if __name__ == "__main__":
    main()
