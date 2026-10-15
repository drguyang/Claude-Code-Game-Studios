#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""kindgen 变异探针 —— A2 / A2b 判别力的**常驻执行体**。

背景
────
`production/qa/evidence/fix-s11-12-case-system-story-001-b2-b4-2026-10-15.md` §四
的 7 变异矩阵当时是 `/tmp` 沙箱的**一次性**实测;补评原件
`review-case-system-story-001-postfix-2026-10-15.md` §六-5 如实登记:

    【变异矩阵无常驻执行体】§四 的 7 变异是沙箱一次性实测,**未固化为 CI 步骤**。

本脚本把它固化为**可重复执行**的探针:每个变异**注入 → 跑断言 → 断言被捕获 → 复原**。
任一变异**未被捕获**即非零退出 —— 即「A2/A2b 的判别力」从一次性实测升为持续判据。

实现口径
────────
· **零文件写入** —— 变异施加在**内存**上(monkeypatch `kindgen.parse_payload_structs`
  的返回值 / 直接改 `kinds` 列表的副本),**不触碰工作树** ⇒ 与 `git status` 无交互、
  可与其它步骤并发,且脚本崩溃也不会留下脏树。
· **零 Unity 依赖** —— 纯 Python + PyYAML,与 `.github/workflows/tests.yml` 既有取向一致。
· **直接调断言函数** —— 不经 CLI,故不受 `main()` 的「总数须 35」校验干扰
  (变异面是**断言函数本身**,不是 registry 总量)。
· **确定性** —— 选中的 struct / 字段一律经 `sorted()` 定序;同一输入必得同一报告。

用法
────
    python3 tools/kindgen/mutation_probe.py

退出码
──────
    0 = 7/7 变异全部被捕获(判别力成立)
    1 = 有变异**逃逸**(判别力缺口 —— 与 kindgen 断言失败同级,须修)
    2 = 夹具前提不成立(找不到目标 struct / 字段 —— 是**夹具过期信号**,非判别力缺口)
"""

import copy
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kindgen  # noqa: E402  (同目录模块;上面已把本目录放进 sys.path)

MUTATIONS = []


def mutation(name):
    def deco(fn):
        MUTATIONS.append((name, fn))
        return fn
    return deco


# ══════════════════════════════════════════════════════════════════════════════
# 夹具辅助
# ══════════════════════════════════════════════════════════════════════════════

def _fresh_structs():
    """载荷 struct 表的**深拷贝**(变异施加在副本上,原件不受影响)。"""
    return copy.deepcopy(kindgen.parse_payload_structs())


def _closure(structs, kinds):
    """A2 的**扫描闭包** = roots ∪ 其字段类型可达的嵌套 struct。

    ⚠️ 变异目标**必须**落在此集合内,否则断言根本不扫它 ⇒ 探针会报「逃逸」
    而其实是**探针选错了目标**(假阴),不是判别力缺口。闭包算法与
    `kindgen.a2_payload_types` 的广度优先展开同口径(`[]` 数组取元素类型)。
    """
    roots = [k["kind"] + kindgen.PAYLOAD_SUFFIX for k in kinds]
    seen = {r for r in roots if r in structs}
    todo = list(seen)
    while todo:
        cur = todo.pop()
        for _fn, ft in structs[cur]["fields"]:
            elem = ft[:-2] if ft.endswith("[]") else ft
            if elem in structs and elem not in seen:
                seen.add(elem)
                todo.append(elem)
    return seen


def _first_field_of_type(structs, ftype, restrict=None):
    """定序找第一个含指定类型字段的 struct → (struct 名, 字段名);无则 None。

    `restrict` 非 None 时只在其中选(通常传 A2 的扫描闭包 —— 见 `_closure`)。
    """
    for name in sorted(structs):
        if restrict is not None and name not in restrict:
            continue
        for fname, ft in structs[name]["fields"]:
            if ft == ftype:
                return name, fname
    return None


def _replace_field_type(structs, sname, fname, new_type):
    structs[sname]["fields"] = [
        (fn, new_type if fn == fname else ft)
        for fn, ft in structs[sname]["fields"]
    ]


def _run_a2(kinds, structs):
    """以给定 struct 表跑 A2,返回断言报告(monkeypatch 后必复原 —— finally 保证)。"""
    original = kindgen.parse_payload_structs
    kindgen.parse_payload_structs = lambda: structs
    try:
        rep = []
        kindgen.a2_payload_types(kinds, rep, {"structs": 0, "fields": 0})
        return rep
    finally:
        kindgen.parse_payload_structs = original


def _root_names(kinds):
    return {k["kind"] + kindgen.PAYLOAD_SUFFIX for k in kinds}


# ══════════════════════════════════════════════════════════════════════════════
# 7 变异(逐条对齐 fix-s11-12 §四 的表)
# ══════════════════════════════════════════════════════════════════════════════

@mutation("M1 载荷字段 int → float")
def m1_int_to_float(kinds):
    structs = _fresh_structs()
    hit = _first_field_of_type(structs, "int", _closure(structs, kinds))
    if hit is None:
        return None
    sname, fname = hit
    _replace_field_type(structs, sname, fname, "float")
    return _run_a2(kinds, structs)


@mutation("M2 载荷字段 bool → string")
def m2_bool_to_string(kinds):
    structs = _fresh_structs()
    hit = _first_field_of_type(structs, "bool", _closure(structs, kinds))
    if hit is None:
        return None
    sname, fname = hit
    _replace_field_type(structs, sname, fname, "string")
    return _run_a2(kinds, structs)


@mutation("M3 嵌套 struct 内 int → double(间接字段)")
def m3_nested_int_to_double(kinds):
    structs = _fresh_structs()
    # 「未登记 struct」= 在扫描闭包内、但**不是**任何 Kind 的 `<Kind>Payload`
    nested = sorted(_closure(structs, kinds) - _root_names(kinds))
    for n in nested:
        hit = _first_field_of_type({n: structs[n]}, "int")
        if hit is None:
            continue
        _replace_field_type(structs, n, hit[1], "double")
        return _run_a2(kinds, structs)
    return None


@mutation("M4 字段类型 → 未登记类型(白名单外)")
def m4_unregistered_type(kinds):
    structs = _fresh_structs()
    roots = sorted(_root_names(kinds) & set(structs))
    for r in roots:
        if structs[r]["fields"]:
            fname = structs[r]["fields"][0][0]
            _replace_field_type(structs, r, fname, "BogusUnregisteredType")
            return _run_a2(kinds, structs)
    return None


@mutation("M5 删 struct 名(<Kind>Payload 缺失)")
def m5_missing_payload_struct(kinds):
    structs = _fresh_structs()
    roots = sorted(_root_names(kinds) & set(structs))
    if not roots:
        return None
    del structs[roots[0]]
    return _run_a2(kinds, structs)


@mutation("M6 抽空字段(零字段 ⇒ 空跑)")
def m6_zero_fields(kinds):
    structs = _fresh_structs()
    roots = sorted(_root_names(kinds) & set(structs))
    if not roots:
        return None
    structs[roots[0]]["fields"] = []
    return _run_a2(kinds, structs)


@mutation("M7 registry 散文注入非否定 float(A2b 面)")
def m7_registry_prose_float(kinds):
    if not kinds:
        return None
    mutated = copy.deepcopy(kinds)
    # 找一个 payload_schema 非空的 Kind 注入;注入点**不含**否定词(无/禁/非/零/不)
    target = next((k for k in mutated if k["payload"].strip()), None)
    if target is None:
        return None
    target["payload"] = target["payload"] + " ; float injected_probe_field"
    rep = []
    kindgen.a2b_registry_prose(mutated, rep)
    return rep


# ══════════════════════════════════════════════════════════════════════════════
# 主流程
# ══════════════════════════════════════════════════════════════════════════════

def main():
    kinds = kindgen.parse_registry()
    if not kinds:
        print("mutation_probe: registry 解析出 0 支 Kind —— 夹具前提不成立")
        sys.exit(2)

    print(f"kindgen 变异探针 —— {len(MUTATIONS)} 变异(fixture: {len(kinds)} 支 Kind)")
    print("─" * 78)

    escaped, skipped, caught = [], [], 0
    for name, fn in MUTATIONS:
        rep = fn(kinds)
        if rep is None:
            skipped.append(name)
            print(f"  SKIP  {name}")
            print(f"        夹具前提不成立(找不到目标 struct / 字段)—— 夹具过期信号")
            continue
        if rep:
            caught += 1
            print(f"  捕获  {name}")
            print(f"        {rep[0]}")
        else:
            escaped.append(name)
            print(f"  ⚠️ 逃逸 {name}")
            print(f"        断言**未捕获**该变异 —— 判别力缺口")

    print("─" * 78)
    print(f"捕获 {caught} · 逃逸 {len(escaped)} · 跳过 {len(skipped)}")

    if escaped:
        print("\nmutation_probe: 有变异逃逸 —— A2/A2b 判别力不成立(须修)")
        sys.exit(1)
    if skipped:
        print("\nmutation_probe: 夹具前提不成立 —— 请更新本探针的目标选择")
        sys.exit(2)
    print("\nmutation_probe: 全部变异被捕获 —— A2/A2b 判别力成立")


if __name__ == "__main__":
    main()
