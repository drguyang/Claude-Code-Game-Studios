#!/usr/bin/env python3
"""AC 编号撞号门 —— `TD-docs-01` 的唯一机械载体(2026-11-02 立)。

## 为什么有这个工具

`AC-20-NN` 这一串 ID **同时属于两个编号空间**:

- `ADR-020`(玩家控制器与相机)自有 `AC-20-01…17`
- **系统 20 库存 GDD**(`inventory-and-items.md`)自有 `AC-20-01…25`

⇒ 17 个 ID 每一位都有两个意思,两侧都是 BLOCKING。根因不是「历史遗留没办法」,
是 **ADR-020 Amendment C(2026-11-01)铸新 ID 时没查重**(库存那批 2026-10-26
就已在被引用)。

用户 2026-11-02 裁定 **① 零改名**:ADR 自有的 AC 一律写 `ADR-0NN AC-NN`,
系统 GDD 的维持 `AC-{sys}-{n}`;**存量不重命名**。本工具是该裁定的**执行体** ——
它不要求任何人「记得查重」,而是让**新的**撞号当场红。

## 判据(四条)

- **C1 新撞号**:某 ID 被 ≥2 个文件「定义」,且不在基线里 ⇒ 红
- **C2 基线不得扩大**:基线里的 ID 不得被**第三个**文件再定义 ⇒ 红
- **C3 基线不得陈旧**:基线里的 ID 必须**仍然是**撞号(否则说明有人改了名 /
  删了定义)⇒ 红,迫使基线随之刷新
- **C4 空集守卫**:扫描到的定义行数为 0 ⇒ 红(路径写错会让 C1 在空集上恒绿,
  这是本仓抓过的「空洞正控」形态)

## 「定义」怎么认(不靠语义,靠两种固定句式)

- GDD 表行:`| **AC-20-01** | **[A]** | ...`
- ADR 校验清单:`- [ ] **AC-20-01(BLOCKING)**:...`

⚠️ **`## GDD Requirements Addressed` 整节跳过** —— 该节的表是**引用**别的系统的 AC,
不是定义。ADR-033 就有一条 `| **AC-23-02** | ...`,按逐行扫会被误判成第二处定义
(本工具首版原型实测踩到过)。

## 用法

```bash
python3 tools/acid/acid_check.py --check           # CI / 提交前:违规即退出码 1
python3 tools/acid/acid_check.py --print           # 打印全部定义与撞号
python3 tools/acid/acid_check.py --mutation-probe  # 内存内变异,自证有判别力
```
"""

import argparse
import json
import os
import re
import sys
from collections import defaultdict

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BASELINE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "acid-baseline.json")

SCAN_ROOTS = ("design/gdd", "docs/architecture")

# 定义句式(两种,均为**行首固定形态**;不做行内匹配,避免把散文引用算成定义)
RE_GDD_ROW = re.compile(r"^\|\s*\*\*(AC-\d+-\d+)\*\*\s*\|")
RE_ADR_BOX = re.compile(r"^- \[ \] \*\*(AC-\d+-\d+)")
RE_HEADING = re.compile(r"^##\s+(.*)$")

# 该节整节跳过:它是**引用**别的系统 AC 的表,不是定义
SKIP_SECTION_MARK = "GDD Requirements Addressed"


def scan(root):
    """返回 {ac_id: [相对路径, ...]}(路径已排序,便于逐字节比对)。"""
    found = defaultdict(set)
    for base in SCAN_ROOTS:
        base_dir = os.path.join(root, base)
        for dirpath, _dirnames, filenames in os.walk(base_dir):
            for name in sorted(filenames):
                if not name.endswith(".md"):
                    continue
                path = os.path.join(dirpath, name)
                rel = os.path.relpath(path, root).replace(os.sep, "/")
                skip = False
                with open(path, encoding="utf-8", errors="replace") as handle:
                    for line in handle:
                        heading = RE_HEADING.match(line)
                        if heading:
                            skip = SKIP_SECTION_MARK in heading.group(1)
                            continue
                        if skip:
                            continue
                        match = RE_GDD_ROW.match(line) or RE_ADR_BOX.match(line)
                        if match:
                            found[match.group(1)].add(rel)
    return {key: sorted(value) for key, value in found.items()}


def collisions(definitions):
    return {ac: files for ac, files in definitions.items() if len(files) > 1}


def load_baseline():
    with open(BASELINE, encoding="utf-8") as handle:
        return json.load(handle)


def evaluate(definitions, baseline):
    """返回 (violations, warnings)。violations 非空 ⇒ 退出码 1。"""
    violations = []
    dups = collisions(definitions)
    grandfathered = baseline["known_collisions"]

    # C4 空集守卫 —— 先判,否则 C1 会在空集上恒绿
    defined_lines = sum(len(files) for files in definitions.values())
    if defined_lines == 0:
        violations.append(
            "C4 空集守卫:扫描到 0 条 AC 定义行 ⇒ 扫描路径或句式可能已失效"
            f"(roots={list(SCAN_ROOTS)})。空集上的『无撞号』不算通过。")

    # C1 新撞号
    for ac in sorted(dups):
        if ac not in grandfathered:
            violations.append(
                f"C1 新撞号:{ac} 被 {len(dups[ac])} 个文件同时定义为 AC —— "
                f"{dups[ac]}。\n"
                "    修法(用户 2026-11-02 裁定 ①):ADR 自有的 AC 写 `ADR-0NN AC-NN`,\n"
                "    系统 GDD 的维持 `AC-{sys}-{n}`;**不要**再铸一个同名 ID。")

    # C2 基线不得扩大
    for ac, recorded in sorted(grandfathered.items()):
        actual = dups.get(ac)
        if actual is None:
            continue
        for path in actual:
            if path not in recorded:
                violations.append(
                    f"C2 基线撞号扩大:{ac} 的既有定义方是 {recorded},"
                    f"现在又多了 `{path}` ⇒ 撞号面变大。")

    # C3 基线不得陈旧
    for ac, recorded in sorted(grandfathered.items()):
        actual = dups.get(ac)
        if actual is None:
            violations.append(
                f"C3 基线陈旧:{ac} 仍记在基线里,但它已不再被 ≥2 个文件定义 ⇒ "
                "撞号已消除(改名 / 删定义)。请从 `acid-baseline.json` 删掉该条,"
                "让基线跟上现状 —— 否则基线会永久掩盖一个已不存在的问题。")
        elif sorted(actual) != sorted(recorded):
            violations.append(
                f"C3 基线定义方不符:{ac} 记为 {sorted(recorded)},实测 {sorted(actual)}。"
                "若是有意的改名 / 挪动,同步更新基线;否则先查清。")

    # 基线覆盖度提示(非违规):让「还剩多少撞号」始终可见
    warnings = []
    remaining = len(grandfathered)
    if remaining:
        warnings.append(
            f"仍存 {remaining} 个已知撞号(基线冻结;见 TD-docs-01)。"
            "本门只保证**不再新增**,不声称它们已解决。")
    return violations, warnings


def cmd_print(definitions, baseline):
    dups = collisions(definitions)
    print(f"定义行所在唯一 AC ID: {len(definitions)}")
    print(f"撞号(≥2 文件定义): {len(dups)}(基线冻结 {len(baseline['known_collisions'])})")
    for ac in sorted(dups):
        mark = "基线" if ac in baseline["known_collisions"] else "**新增**"
        print(f"  [{mark}] {ac}")
        for path in dups[ac]:
            print(f"          {path}")
    return 0


def cmd_check(definitions, baseline):
    violations, warnings = evaluate(definitions, baseline)
    for note in warnings:
        print(f"提示:{note}")
    if not violations:
        print("✅ AC 编号撞号门:无新增撞号")
        return 0
    for item in violations:
        print(f"❌ {item}")
    print(f"\n共 {len(violations)} 项违规。工具说明见本文件头部注释。")
    return 1


def cmd_mutation_probe(definitions, baseline):
    """内存内变异,证明四条判据各自真的会红(**完全合成,不依赖仓库现状**)。

    ⚠️ **为什么不拿真实定义集做底**:2026-11-02 改名轮后基线**已是空集**,
    早期版本用 `sorted(base["known_collisions"])[0]` 取一个真基线 ID 当靶,
    在空基线上直接 `IndexError` —— 探针自己先崩了。⇒ 改为合成一套最小定义集,
    与仓库现状解耦:M1–M4 测的是**判据逻辑**,真实接线由 `--check` + 证伪轮的
    R1–R3(真文件变异)覆盖。
    """
    FAKE_BASE = {
        "known_collisions": {
            "AC-99-01": ["design/gdd/fake-a.md", "docs/architecture/adr-099-fake.md"],
        }
    }
    FAKE_DEFS = {
        "AC-99-01": ["design/gdd/fake-a.md", "docs/architecture/adr-099-fake.md"],
    }

    scenarios = []

    def probe(label, mutate, expect_prefix):
        mutated_defs, mutated_base = mutate()
        violations, _ = evaluate(mutated_defs, mutated_base)
        hit = any(item.startswith(expect_prefix) for item in violations)
        scenarios.append((label, expect_prefix, hit, violations))

    def copy_defs(defs):
        return {ac: list(files) for ac, files in defs.items()}

    # M1:凭空造一个新撞号(不在基线里)
    def m1():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-02"] = ["design/gdd/fake-b.md", "docs/architecture/adr-099-fake.md"]
        return defs, FAKE_BASE

    # M2:让基线 ID 的定义方多一个文件(撞号面扩大)
    def m2():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-01"] = defs["AC-99-01"] + ["design/gdd/some-third-file.md"]
        return defs, FAKE_BASE

    # M3:基线 ID 只剩一个定义方(撞号已消除 ⇒ 基线陈旧)
    def m3():
        defs = copy_defs(FAKE_DEFS)
        defs["AC-99-01"] = defs["AC-99-01"][:1]
        return defs, FAKE_BASE

    # M4:空集守卫
    def m4():
        return {}, FAKE_BASE

    probe("M1 新撞号", m1, "C1")
    probe("M2 基线扩大", m2, "C2")
    probe("M3 基线陈旧", m3, "C3")
    probe("M4 空集守卫", m4, "C4")

    failed = 0
    for label, expect, hit, violations in scenarios:
        status = "红(符合预期)" if hit else "**未红 —— 该判据无判别力**"
        print(f"[{label}] 期望命中 {expect} ⇒ {status}")
        if not hit:
            failed += 1
            for item in violations:
                print(f"        实际违规:{item.splitlines()[0]}")
    print("PROBE-OK" if failed == 0 else f"PROBE-FAILED({failed})")
    return 0 if failed == 0 else 1


def main(argv):
    parser = argparse.ArgumentParser(description="AC 编号撞号门(TD-docs-01)")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--check", action="store_true", help="违规即退出码 1(CI 用)")
    group.add_argument("--print", action="store_true", help="打印全部定义与撞号")
    group.add_argument("--mutation-probe", action="store_true", help="内存内变异自证判别力")
    args = parser.parse_args(argv)

    definitions = scan(REPO_ROOT)
    baseline = load_baseline()

    if args.print:
        return cmd_print(definitions, baseline)
    if args.mutation_probe:
        return cmd_mutation_probe(definitions, baseline)
    return cmd_check(definitions, baseline)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
