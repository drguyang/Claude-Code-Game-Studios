#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""draft_gate —— 「增量落盘」判据的**执行体**(`TD-s13-01` · sprint-21 `S21-11` · 2026-10-31)。

═══════════════════════════════════════════════════════════════════════════
为什么存在
═══════════════════════════════════════════════════════════════════════════
`code-review` SKILL §Phase 7b ③ 要求每位评审专家在轮次耗尽前**边勘察边落盘**,
判据 = 「草稿文件已存在且非空,且覆盖其『判定清单过半』(≥ 清单条数的一半有结论)」。

**在此之前,这条判据只有散文形态**:
    「可复算:`wc -l <草稿路径>` > 0 且 草稿内结论行数 ≥ 应核条数 / 2」
⇒ 「结论行数」怎么数、什么算一条结论,全凭编排方**眼看**。
本项目对这类「有判据、但判据是人眼的」形态有过多次教训(自述顶替门 = 假绿)。
本脚本把那一句变成**可执行的退出码**,与 `tools/debtcount/` 同构。

═══════════════════════════════════════════════════════════════════════════
它**只认表格式草稿**(刻意,不是偷懒)
═══════════════════════════════════════════════════════════════════════════
Phase 7b ③ 规定草稿格式为:

    | ID | 判定 | 落点 | 证据 |

**结论行 = 该表的「数据行」**,定义(全部可机械判定):
  · 行以 `|` 开头;
  · **不是**分隔行(`|---` / `|:--` / `|-:-` —— 特征是去掉 `|`、`-`、`:`
    、空格后为空);
  · **不是**表头行(单元格里出现 `ID` / `判定` 且不含其它列的判定词)。

⇒ 用散文写的草稿一律判 **STRUCTURAL** 并给出改写指引 ——
**不做**模糊启发式(「数出现多少次 ✅」之类):那种计数会把「表格里提到 FAIL 一次」
也当成一条结论,正是本债要消灭的「看起来可复算」形态。

═══════════════════════════════════════════════════════════════════════════
退出码
═══════════════════════════════════════════════════════════════════════════
  0  PASS       草稿存在、非空、结论行 ≥ --min-conclusions
  1  FAIL       草稿存在但**空**或**结论行不足**(判据未达标)
  2  STRUCTURAL 草稿不存在 / 不可读 / 参数非法 / 找不到表格式结论行

⚠️ **0 不等于「这份评审做完了」** —— 它只表示「增量落盘这条判据满足了」。
   评审质量另由正式报告(`review-*.md`)与评审模式门负责。
⚠️ **本脚本不改变 `TD-s13-01` 的「有效性仍前瞻」这个事实** ——
   它只把**判据本身**从散文变成执行体;「这条机制能不能真的让 agent 不丢交付物」
   仍须下一次真实重评实测,当下不可自证(禁借绿)。

用法:
    python3 tools/review/draft_gate.py <draft.md> --min-conclusions N
    python3 tools/review/draft_gate.py <draft.md> --min-conclusions N --quiet
"""

import argparse
import os
import re
import sys

EXIT_PASS, EXIT_FAIL, EXIT_STRUCTURAL = 0, 1, 2

_SEP = re.compile(r"^[\s|:\-]+$")
_ID_CELLS = {"id", "编号", "检查项", "条目"}
_VERDICT_CELLS = {"判定", "结论", "verdict", "状态", "结果"}


def is_separator(line):
    """`|---|:--|` 一类分隔行。"""
    return "|" in line and _SEP.match(line) is not None


def cells(line):
    return [c.strip().lower() for c in line.strip().strip("|").split("|")]


def is_header(line):
    """表头行 = 同时含「标识列」与「判定列」的列名。

    ⚠️ 用**整格精确匹配**,不是子串 —— 子串匹配会把 `valid` / `provider`
    这类数据行误判成表头(实测过:那样会**少算**结论行,把 PASS 判成 FAIL)。
    """
    cs = set(cells(line))
    return bool(cs & _ID_CELLS) and bool(cs & _VERDICT_CELLS)


def count_conclusions(text):
    """统计表数据行数。返回 (结论行数, 是否看到过表头)。"""
    n = 0
    saw_header = False
    for raw in text.splitlines():
        line = raw.strip()
        if not line.startswith("|") or is_separator(line):
            continue
        if is_header(line):
            saw_header = True
            continue
        # 至少两列才算一条结论(单格 `| x |` 多半是误用)
        if line.count("|") >= 2 and line.strip("|").strip():
            n += 1
    return n, saw_header


def main():
    ap = argparse.ArgumentParser(description="增量落盘判据执行体(TD-s13-01)")
    ap.add_argument("draft", help="草稿文件路径(Phase 7b ③ 规定的 output 路径)")
    ap.add_argument("--min-conclusions", type=int, required=True,
                    help="应核条数的一半(向上取整);编排方按清单条数算好传入")
    ap.add_argument("--quiet", action="store_true", help="只给退出码,不打印明细")
    args = ap.parse_args()

    if args.min_conclusions < 0:
        print(f"[STRUCTURAL] --min-conclusions 不得为负:{args.min_conclusions}")
        return EXIT_STRUCTURAL

    if not os.path.isfile(args.draft):
        print(f"[STRUCTURAL] 草稿不存在:{args.draft}\n"
              "  ⇒ 按 Phase 7b ③,交付物 = **草稿文件已存在且非空**,不是「对话里贴了报告」。")
        return EXIT_STRUCTURAL

    try:
        with open(args.draft, encoding="utf-8") as fh:
            text = fh.read()
    except Exception as exc:  # noqa: BLE001 —— 读不了必须显式报,不静默当空
        print(f"[STRUCTURAL] 草稿不可读:{args.draft}:{exc}")
        return EXIT_STRUCTURAL

    lines = [l for l in text.splitlines() if l.strip()]
    n, saw_header = count_conclusions(text)

    if not lines:
        print(f"[FAIL] 草稿为空(0 行非空内容):{args.draft}")
        return EXIT_FAIL

    if not saw_header:
        print(f"[STRUCTURAL] 在 {args.draft} 里找不到表格式结论行。\n"
              "  Phase 7b ③ 规定的格式是 `| ID | 判定 | 落点 | 证据 |`;"
              "散文草稿本脚本**不做**模糊计数。\n"
              f"  (非空行 {len(lines)} · 识别到的数据行 {n} · 见到表头 {saw_header})")
        return EXIT_STRUCTURAL

    if n < args.min_conclusions:
        print(f"[FAIL] 结论行不足:{n} < {args.min_conclusions}(清单过半未达)")
        return EXIT_FAIL

    if not args.quiet:
        print(f"[PASS] 草稿非空({len(lines)} 行)且结论行 {n} ≥ {args.min_conclusions}")
    return EXIT_PASS


if __name__ == "__main__":
    sys.exit(main())
