#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""kindgen —— SimEvent.Kind 单一真源生成器 + 构建期断言(ADR-024 §⑤)。

输入 : design/registry/entities.yaml(`SimEvent.Kind.*` 38 支,stream/author/payload_schema 必填)
       unity/Assets/Sim.Contracts/EventKind.cs(C# 侧镜像)
输出 : unity/Assets/Sim/StreamRouting.g.cs(Kind→StreamId 纯函数 switch)

断言(任一失败 = 非零退出 = 构建失败):
  A1 每支 Kind 恰有一个合法 stream ∈ {history, case, world}(禁双家/无家)
  A2 **严格形 · 载荷 struct 的字段类型 ∈ 整数域白名单**(✅ 2026-10-15 兑现,此前只登记未实现)
      对象 = `unity/Assets/Sim.Contracts/` 下 `<Kind>Payload` 的 **C# 字段声明**
      (非 registry 散文 —— ADR-024 §⑤ A2 原文即写「**字段类型** ∈ 整数域白名单」);
      逐 Kind 解析字段并**递归展开**嵌套 struct(WorldPos / CaseId / DiseaseIdSet / Fix …),
      **每个字段类型**一律判白名单:8 种整数原始类型 + bool + 树内可解析 struct;
      float / double / decimal / string / char / object 及**任何白名单外类型**皆 FAIL
      (白名单判定,**非 grep** —— 承 story-001 Edge case「载荷塞 float 字段 ⇒ A2 拒(白名单非 grep)」)。
      ⚠️ 非空跑守卫:扫得 0 个字段 = FAIL;`<Kind>Payload` 缺失或零字段 = FAIL(逐支)。
  A2b 辅助半边(对象 = registry 散文;**单独不足以**构成 A2 达标,保留理由见函数 docstring):
      payload_schema 出现「[]」须在 payload_schema+constraint 里有界证据;
      payload_schema 出现 float/double **字样**须在否定语境(过渡期文档层漂移拦截)。
  A3 Kind 名无重复
  A4 author 必填且首整数 ∈ 已登记系统号(systems-index 表实测集)
  A5 registry ↔ EventKind.cs 成员双向差集归零

⚠️ 口径登记:ADR-024 §⑤ 设想 kindgen = 编辑期 .NET 工具;本实现 = 纯 Python(本机
   无 .NET SDK / 无编译器实测面,断言逻辑不依赖引擎,Unity 侧零引用)。CI 挂接归
   ADR-012 矩阵故事;`--check` 即失败非零退出,天然可作构建步骤。
   ✅ 2026-10-15 已挂接:`.github/workflows/tests.yml` job `kindgen-assertions`
   (`--check` 失败即 CI 红 + 重生成后 `git diff` 产物一致性)= **AC-37-03 的唯一载体**
   (修补评原件 B-4「载体未跑却报绿」:此前零 CI 接线,生成物自述冒充证据)。

用法: python3 tools/kindgen/kindgen.py [--check]
  默认生成 + 断言;--check 只断言不写文件(比对既有产物的再生成一致性由 CI 另管 —— 见上)。
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
CONTRACTS_DIR = REPO / "unity" / "Assets" / "Sim.Contracts"

STREAM_MAP = {"history": "History", "case": "Case", "world": "World"}
INT_TYPE_TOKENS = ("i8", "i16", "i32", "i64", "u8", "u16", "u32", "u64")
ARRAY_UNBOUNDED_OK = ("上界", "≤", "MAX", "cap", "有界")   # [] 出现须在 schema 里找到界证据

# ── A2 严格形(载荷 struct 字段类型白名单)───────────────────────────────────────
# 载荷 struct 命名约定:EventKind.X ⟷ XPayload(A5 的姊妹面;实测 36/36 命中)。
PAYLOAD_SUFFIX = "Payload"

# 白名单 = 整数原始类型 8 种宽度(同 registry INT_TYPE_TOKENS 的量纲)+ bool。
# ⚠️ 这是**白名单**:不在集合内的一律 FAIL —— 故 float/double/string 无需单独列举即被拒;
#    HAZARD_TYPES 只为把「为何被拒」写进报错文本(点名 ADR 出处)。
# ⚠️ **枚举类型字段亦被拒 —— 是刻意,非漏配**:载荷面的已登记裁定是「枚举类字段一律 int +
#    注释标 ordinal 来源(contracts **不造枚举**,防 ordinal 表第二真源)」(PayloadCommon.cs
#    文件头 · 支 1-b)。故载荷 struct 里出现具名枚举类型字段 = 偏离该裁定,应红。
#    若未来该裁定改判,须在此处增补「枚举即合法」并同步 ADR;不得放宽整份白名单。
INT_PRIMITIVES = frozenset({
    "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "bool",
})
HAZARD_TYPES = {
    "float": "非整数域 —— ADR-006 定点域边界(sim 禁 float)",
    "double": "非整数域 —— 同上",
    "decimal": "非整数域(十进制浮点)",
    "string": "引用类型 —— ADR-006 Amendment A(载荷禁引用字段)",
    "char": "非整数域(C# char = UTF-16 码元)",
    "object": "引用类型 —— 同 string",
}
# 字段声明行判据:**必须带访问修饰符** + 以 `;` 收尾的纯声明。
#   ⇒ `return result;`(Fix.cs 实测存在)不命中;`public const int X = -1;` 亦不命中(const 非载荷字段)。
_FIELD_RE = re.compile(
    r"^[ \t]*(?:public|private|internal|protected)\s+"
    r"(?:static\s+)?(?:readonly\s+)?"
    r"([A-Za-z_]\w*(?:\[\])?)\s+([A-Za-z_]\w*)\s*;",
    re.M,
)
_NON_TYPE_KEYWORDS = frozenset({"const", "static", "readonly", "return", "new", "in", "ref", "out"})


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


def _brace_body(text, open_idx):
    """返回 text[open_idx] 起的第一个配平 `{...}` 的体内文本。"""
    depth = 0
    for i in range(open_idx, len(text)):
        c = text[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return text[open_idx + 1:i]
    return text[open_idx + 1:]


def parse_payload_structs():
    """Sim.Contracts 全树的 `public readonly struct` → {名: {"file": …, "fields": [(字段名, 类型)]}}。

    字段判据 = **声明行**(访问修饰符 + `;`),不含 const / 方法体语句 ——
    防 `return result;` 型误命中(Fix.cs 实测有该行;仅靠类型名正则会把 `result` 当字段)。
    """
    out = {}
    for path in sorted(CONTRACTS_DIR.rglob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for m in re.finditer(r"public readonly struct (\w+)", text):
            body = _brace_body(text, text.index("{", m.end()))
            out[m.group(1)] = {
                "file": path.name,
                "fields": [
                    (fm.group(2), fm.group(1))
                    for fm in _FIELD_RE.finditer(body)
                    if fm.group(1) not in _NON_TYPE_KEYWORDS
                ],
            }
    return out


def a2_payload_types(kinds, rep, stats):
    """A2 严格形:载荷 **struct 字段类型** ∈ 整数域白名单(可达闭包,每个字段只判一次)。

    ⚠️ B-2 修复点(2026-10-15,补评原件 `review-case-system-story-001-retro-2026-10-15.md` §二):
    旧实现对 registry **散文** grep `float|double`,实测 36 支命中仅 4 次且**全落否定语境**
    (case 流 5 支命中 **0** ⇒ 内层循环体执行 0 次)⇒ 断言永不可能触发,且对象不是字段类型。
    本实现把断言对象换成 C# 字段声明、判据换成**白名单**(白名单外一律拒),并加非空跑守卫。
    """
    structs = parse_payload_structs()
    roots = []
    for k in kinds:
        name = k["kind"] + PAYLOAD_SUFFIX
        if name not in structs:
            fail(rep, f"A2 {k['kind']}: 未找到载荷 struct {name}"
                      f"(命名约定 <Kind>{PAYLOAD_SUFFIX};已扫 {CONTRACTS_DIR.name}/ 全树)")
        else:
            roots.append(name)

    todo, seen = list(roots), set(roots)
    while todo:                      # 广度优先展开嵌套 struct(seen 兼作防环)
        cur = todo.pop(0)
        fields = structs[cur]["fields"]
        if not fields:
            fail(rep, f"A2 {cur}: 零字段 —— 该 struct 的字段扫描为空(空跑)")
            continue
        for fname, ftype in fields:
            stats["fields"] += 1
            is_array = ftype.endswith("[]")
            elem = ftype[:-2] if is_array else ftype
            path = f"{cur}.{fname}" + ("[]" if is_array else "")
            if elem in INT_PRIMITIVES:
                continue
            if elem in structs:      # 嵌套 struct:本字段合法,其字段由闭包继续判
                if elem not in seen:
                    seen.add(elem)
                    todo.append(elem)
                continue
            hint = HAZARD_TYPES.get(
                elem, "白名单外类型(引用类型 / 浮点 / 未登记 struct 一律拒)")
            fail(rep, f"A2 {cur}: 字段 {path} 类型 {elem!r} ∉ 整数域白名单 —— {hint}")

    stats["structs"] = len(seen)
    if stats["fields"] == 0:
        fail(rep, "A2 扫描字段数 = 0 —— 断言面为空(空跑)")


def a2b_registry_prose(kinds, rep):
    """A2 的**辅助**半边 —— 对象是 registry 散文。

    ⚠️ 本半边**单独不足以**构成 A2 达标(它只在「有人把 float 写进散文」时才会触发);
    A2 的载荷面判据 = `a2_payload_types()`(字段类型白名单)。保留两点理由:
      ① **数组界证据只此一处** —— 严格形在 C# 里只看得见 `T[]`,看不见「界」在哪;
      ② 顺手拦 registry 侧新增的 float/double **字样**(文档层漂移,零成本)。
    """
    for k in kinds:
        ps = k["payload"]
        # float/double 字样:否定语境(前 4 字内含 无/禁/非/零/不)豁免
        for m in re.finditer(r"[Ff]loat|[Dd]ouble", ps):
            ctx = ps[max(0, m.start() - 4):m.start()]
            if not re.search(r"[无禁非零不]", ctx):
                fail(rep, f"A2b {k['kind']}: 散文载荷出现非否定语境的 float/double 字样 —— {ps!r}")
        # 数组字段须有界证据(X-2 定长缓冲裁定的一致性)。
        # 界证据允许写在 payload_schema 或 constraint 任一(Craft 的界在 constraint:
        # 「点火行为率 ≤ 每玩家每 tick 1」)⇒ 两处合看;不放宽 token 集本身。
        bound_ctx = ps + k.get("constraint", "")
        if "[]" in bound_ctx and not any(t in bound_ctx for t in ARRAY_UNBOUNDED_OK):
            fail(rep, f"A2b {k['kind']}: 数组字段无界证据(payload_schema+constraint 均无 ∈/≤/MAX/cap/上界/有界)")


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
    if len(kinds) != 38:
        fail(rep, f"kindgen: registry Kind 总数 = {len(kinds)},须 38(ADR-024 V-2 条目数;2026-10-09 ADR-030 起 34 → 35;2026-10-21 ADR-032 起 35 → 36;2026-10-25 HouseCallStarted 起 36 → 37;2026-10-30 ADR-035 TreatmentHarmOnset 起 37 → 38)")
    stats = {"structs": 0, "fields": 0}          # A2 非空跑证据(随成功行打印)
    a1_streams(kinds, rep)
    a2_payload_types(kinds, rep, stats)
    a2b_registry_prose(kinds, rep)
    a3_unique_names(kinds, rep)
    a4_authors(kinds, rep)
    a5_bidirectional(kinds, rep)

    hard = [r for r in rep if r.startswith("FAIL")]
    for line in rep:
        print(line)
    if hard:
        print(f"\nkindgen: {len(hard)} 条断言失败 —— 构建失败(ADR-024 §⑤)")
        sys.exit(1)

    a2_note = (f"A2 严格形:{stats['structs']} 个载荷 struct / "
               f"{stats['fields']} 个字段类型受判(非空跑)")
    if args.check:
        print(f"kindgen: A1–A5 全过(--check 不写文件)· {a2_note}")
        return

    OUT_CS.write_text(render(kinds), encoding="utf-8")
    print(f"kindgen: A1–A5 全过 → 写出 {OUT_CS.relative_to(REPO)}"
          f"({len(kinds)} 支 switch)· {a2_note}")


if __name__ == "__main__":
    main()
