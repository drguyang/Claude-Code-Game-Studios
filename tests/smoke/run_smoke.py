#!/usr/bin/env python3
"""critical-paths 的可执行入口(TD-smoke-01 · sprint-21 S21-5 · 2026-10-31)。

═══════════════════════════════════════════════════════════════════════════════
它解决的是什么
═══════════════════════════════════════════════════════════════════════════════
`tests/smoke/critical-paths.md` 自 2026-09-21 起是**一份清单,不是一个门** ——
22 条一条都跑不了,于是每一次 `/smoke-check` 都对 A–D **整组报 N/A 却给出 PASS**。
债册 `TD-smoke-01` 的实测 ④:10 份 smoke 报告,**没有一份**跑过 A–D。

本脚本不假装能把 22 条都变成自动判据。它做三件**可判定**的事:

  1. **双向校验** md 的编号集合 ↔ 机读表 `critical_paths.json` 的 id 集合。
     任一方向缺项 ⇒ 退出码 2(**结构损坏**),不是「少跑一条」。
  2. **逐条给出口**:每条落在 `test` / `manual` / `none` 三种之一。
     `none` 就是**没有载体** —— 会被**逐条具名**列出来,不再是一个整组的 N/A。
  3. **能跑的跑**:`test` 条从 Unity 测试结果件判 PASS/FAIL(本脚本自己不启动 Unity,
     免得把「15 分钟的冒烟」变成「40 分钟的编辑器批跑」;过滤器用 `--emit-filter` 取)。

═══════════════════════════════════════════════════════════════════════════════
退出码(这是本脚本的 PASS/FAIL 出口)
═══════════════════════════════════════════════════════════════════════════════
  0  PASS         22 条全部由可执行判据给出 PASS
  1  FAIL         至少一条**执行过且失败**
  2  STRUCTURAL   md ↔ json 不一致 / 表项缺字段 / 有 test 项但结果件里查无此测
  3  INCOMPLETE    无失败,但仍有 N 条没有可执行判据(具名列出)

⚠️ **3 不是 0。** 「没有可执行判据」与「通过了」是两件事;把 3 读成绿,
    就回到了本债要消灭的那个形态。CI 上要硬门用 `--require-full`(把 3 升级为 1)。

用法:
  python3 tests/smoke/run_smoke.py                        # 只做结构校验 + 需结果件的条目标 NOT-RUN
  python3 tests/smoke/run_smoke.py --results <unity.xml>  # 用既有结果件判 test 条
  python3 tests/smoke/run_smoke.py --emit-filter          # 打印 -testFilter 串(复合判据会全部展开)

`critical_paths.json` 的 `check` 字段(2026-10-31 起):
  字符串      —— 单条判据
  字符串数组  —— 复合判据;**全部**载体都 Passed 才算 PASS,任一 FAIL 即 FAIL,
                 任一查无此测即 STRUCTURAL。口径 = `coding-standards.md`「一个 AC 允许多条载体」。
  python3 tests/smoke/run_smoke.py --require-full         # 3 => 1
"""

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
MD_PATH = os.path.join(HERE, "critical-paths.md")
JSON_PATH = os.path.join(HERE, "critical_paths.json")

EXIT_PASS, EXIT_FAIL, EXIT_STRUCTURAL, EXIT_INCOMPLETE = 0, 1, 2, 3


def parse_md_ids(path):
    """取 md 里 `^<n>. ` 形式的条目编号(即 A–D 的 22 条)。"""
    if not os.path.isfile(path):
        return None, f"清单文件不存在:{path}"
    ids = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            m = re.match(r"^(\d+)\.\s+\S", line.rstrip("\n"))
            if m:
                ids.append(int(m.group(1)))
    return ids, None


def load_registry(path):
    if not os.path.isfile(path):
        return None, f"机读表不存在:{path}"
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)
    items = data.get("items")
    if not isinstance(items, list) or not items:
        return None, "机读表 items 为空"
    return items, None


def structural_errors(md_ids, items):
    """双向差集 + 每条必备字段。返回错误列表(空 = 结构完好)。"""
    errs = []
    reg_ids = [it.get("id") for it in items]

    dup = sorted({i for i in reg_ids if reg_ids.count(i) > 1})
    if dup:
        errs.append(f"机读表 id 重复:{dup}")

    missing_in_reg = sorted(set(md_ids) - set(i for i in reg_ids if isinstance(i, int)))
    extra_in_reg = sorted(set(i for i in reg_ids if isinstance(i, int)) - set(md_ids))
    if missing_in_reg:
        errs.append(f"md 有条目但机读表没有:{missing_in_reg}(新条目须同批登记)")
    if extra_in_reg:
        errs.append(f"机读表有条目但 md 没有:{extra_in_reg}(条目被删后表未同步)")

    for it in items:
        iid = it.get("id")
        st = it.get("status")
        if st not in ("test", "manual", "none"):
            errs.append(f"#{iid} status 非法:{st!r}(只允许 test / manual / none)")
            continue
        if st == "test":
            cs = checks_of(it)
            if not cs:
                errs.append(f"#{iid} status=test 但没有 check(测试全名;单条写字符串,复合写数组)")
            # 2026-10-31:数组元素不得为空串 —— 空串会让 --emit-filter 产出 ";;" 而静默少跑
            elif isinstance(it.get("check"), list) and len(cs) != len(it["check"]):
                errs.append(f"#{iid} check 数组里有空元素")
        if st == "manual" and not (it.get("protocol") or "").strip():
            errs.append(f"#{iid} status=manual 但没有 protocol(谁 / 何时跑)")
        if st == "none" and not (it.get("reason") or "").strip():
            errs.append(f"#{iid} status=none 但没有 reason(为什么无载体)")
        if not (it.get("title") or "").strip():
            errs.append(f"#{iid} 缺 title")
    return errs


def checks_of(item):
    """取一条表项的载体测试全名列表。

    ⚠️ 2026-10-31(`S21-10` 收口后补):`check` **可以是字符串,也可以是字符串数组**。
    单条判据(如「校验和回退」)写字符串;复合判据(如「跨格发一条 **且** 连续移动不发」)
    写数组 —— 与 `coding-standards.md` 的「**一个 AC 允许多条载体**」同口径。
    只认字符串时,复合判据只能二选一:要么拆细判据迁就载体(改判据 = 假绿),
    要么整条记无载体(把已存在的证据扔掉)。两种都不对。
    """
    raw = item.get("check")
    if raw is None:
        return []
    if isinstance(raw, str):
        return [raw.strip()] if raw.strip() else []
    if isinstance(raw, list):
        return [str(x).strip() for x in raw if str(x).strip()]
    return []      # 其它类型 ⇒ 空 ⇒ 上层报 STRUCTURAL,不静默


def load_results(path):
    """Unity 测试结果 XML → {fullname: result}。解析失败返回 (None, 原因)。"""
    try:
        tree = ET.parse(path)
    except Exception as exc:  # noqa: BLE001 —— 结果件损坏必须显式报,不静默
        return None, f"结果件解析失败:{path}:{exc}"
    out = {}
    for tc in tree.iter("test-case"):
        full = tc.get("fullname") or ""
        if full:
            out[full] = tc.get("result") or ""
    return out, None


def main():
    ap = argparse.ArgumentParser(description="critical-paths 可执行入口(TD-smoke-01)")
    ap.add_argument("--results", help="Unity -runTests 产出的 XML 结果件")
    ap.add_argument("--emit-filter", action="store_true", help="只打印 -testFilter 串")
    ap.add_argument("--require-full", action="store_true", help="没有可执行判据时退 1 而不是 3")
    args = ap.parse_args()

    md_ids, md_err = parse_md_ids(MD_PATH)
    items, reg_err = load_registry(JSON_PATH)
    if md_err or reg_err:
        print(f"[STRUCTURAL] {md_err or reg_err}")
        return EXIT_STRUCTURAL

    errs = structural_errors(md_ids, items)
    if errs:
        print("[STRUCTURAL] 清单 ↔ 机读表 不一致:")
        for e in errs:
            print("  -", e)
        return EXIT_STRUCTURAL

    tests = [it for it in items if it["status"] == "test"]

    if args.emit_filter:
        print(";".join(c for it in tests for c in checks_of(it)))
        return EXIT_PASS

    results = None
    if args.results:
        results, rerr = load_results(args.results)
        if rerr:
            print(f"[STRUCTURAL] {rerr}")
            return EXIT_STRUCTURAL

    rows, failures, missing_in_results = [], [], []
    for it in sorted(items, key=lambda x: x["id"]):
        iid, st = it["id"], it["status"]
        if st == "test":
            cs = checks_of(it)
            if results is None:
                verdict = "NOT-RUN(需 --results)"
            else:
                absent = [c for c in cs if c not in results]
                if absent:
                    verdict = "STRUCTURAL(结果件查无此测)"
                    missing_in_results.extend(absent)
                else:
                    bad = [(c, results[c]) for c in cs if results[c] != "Passed"]
                    if bad:
                        verdict = f"FAIL({bad[0][1]})"
                        failures.extend((iid, c, r) for c, r in bad)
                    else:
                        # 复合判据把「几条载体」打在判定列上,别让人以为一条=一条判据
                        verdict = "PASS" if len(cs) == 1 else f"PASS({len(cs)} 载/全绿)"
        elif st == "manual":
            verdict = "MANUAL(已具名协议)"
        else:
            verdict = "NO-CARRIER(无名)"

        rows.append((iid, it["group"], st, verdict, it["title"]))

    width = max(len(r[4]) for r in rows)
    print(f"{'#':>3}  {'组':<2} {'状态':<7} {'判定':<32} 条目")
    print("-" * (width + 60))
    for iid, grp, st, verdict, title in rows:
        print(f"{iid:>3}  {grp:<2} {st:<7} {verdict:<32} {title}")

    # ⚠️ 2026-10-31:复合判据的判定串是 "PASS(n 载/全绿)" —— 用 == "PASS" 会把它们**漏计**,
    #    于是「合计」说 7 而明细里明明有 8 条 PASS。判定与计数必须同一判据:startswith。
    auto_pass = sum(1 for r in rows if r[3].startswith("PASS"))
    auto_fail = sum(1 for r in rows if r[3].startswith("FAIL"))
    auto_notrun = sum(1 for r in rows if r[3].startswith("NOT-RUN"))
    manual = sum(1 for r in rows if r[3].startswith("MANUAL"))
    noc = sum(1 for r in rows if r[3].startswith("NO-CARRIER"))

    print()
    print(f"合计 {len(rows)} 条 | 自动 PASS {auto_pass} · 自动 FAIL {auto_fail} · "
          f"未跑(需结果件) {auto_notrun} · 具名人工 {manual} · 无名 {noc}")

    if noc:
        print("\n**无载体条目(逐条具名 —— 这些不是「通过」,是没有判据)**")
        for iid, _g, _s, _v, title in rows:
            if _v.startswith("NO-CARRIER"):
                reason = next(it["reason"] for it in items if it["id"] == iid)
                print(f"  #{iid} {title}\n       理由:{reason}")

    if missing_in_results:
        print("\n[STRUCTURAL] 机读表绑定的测试在结果件里查无 —— 测试被改名 / 删除,或结果件不是这一批:")
        for c in missing_in_results:
            print("  -", c)
        return EXIT_STRUCTURAL

    if auto_fail:
        print("\n[FAIL] 执行过且失败:")
        for iid, c, r in failures:
            print(f"  - #{iid} {c} -> {r}")
        return EXIT_FAIL

    if auto_pass == len(rows):
        print("\n[PASS] 22 条全部由可执行判据给出 PASS。")
        return EXIT_PASS

    print(f"\n[INCOMPLETE] 无失败,但 {len(rows) - auto_pass} 条没有可执行判据"
          f"(具名人工 {manual} · 无名 {noc} · 未跑 {auto_notrun})。"
          "\n  ⚠️ 3 不是 0 —— 「没有判据」与「通过了」是两件事。")
    return EXIT_FAIL if args.require_full else EXIT_INCOMPLETE


if __name__ == "__main__":
    sys.exit(main())
