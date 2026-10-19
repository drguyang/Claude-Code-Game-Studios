#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""golden-v1 —— 51 遥测(telemetry-analytics)确定性黄金夹具参考实现。

出处:story-008 AC-51-C3(可进 ADR-012 黄金夹具)· ADR-012 §一(双级黄金夹具)·
      ADR-006 §三(ROUND_HALF_AWAY_FROM_ZERO 唯一舍入)· GDD telemetry-analytics R7(整数纪律)。

⚠️ **与 C# 实现零共享代码**:本文件按 GDD / ADR 的公式**手写**,不参考、不调用、不生成
   C# 侧的等价物。C# 侧只**读**本文件产出的 `golden-v1.txt`,不写(防自指:金标准若由
   C# 首跑生成 ⇒ 恒过,那不是夹具)。
⚠️ **刷新纪律**(ADR-012 §三):改公式 ⇒ 升 `golden-v2` + 变更日志 + 全体平台同时重签;
   **禁**就地改老数。本文件输出即金标准本体,任何一次输出变化都须走上述流程。

覆盖的求值面(全部为纯整数,无浮点):
  T1  RoundHalfAwayFromZero(num, den)   —— 统一舍入(ADR-006 §三)
  T2  ToQ1616(num, den)                 —— Q16.16 标量导出(`(num<<16)` 后显式舍入)
  T3  F1 指标(F1a 判断准确率 / F1b 改写率)—— reducible 判定 + 整数对
  T4  TickFoldEngine.Fold(ticks)        —— 空白与退化场景的五元组折叠

运行:`python3 golden_v1_reference.py`(确定性输出到 stdout)
校验:`python3 golden_v1_reference.py --check <computed.txt>`(逐行比对,不符则非零退出)
"""

import sys

# ── T1: ROUND_HALF_AWAY_FROM_ZERO ──────────────────────────────────────────
# 逐字对 ADR-006 §三:C# 的 `/` 是截断除法,须显式舍入;禁 ties-to-even。
# 实现口径与 GDD 一致:`q = (2|n| + |d|) // (2|d|)`,符号 = 分子分母异或。


def round_half_away(num: int, den: int) -> int:
    if den == 0:
        return 0            # 分母 0 ⇒ 调用方走 DenominatorZero 分支,不除零
    n = -num if num < 0 else num
    d = -den if den < 0 else den
    q = (2 * n + d) // (2 * d)
    return -q if (num < 0) ^ (den < 0) else q


# ── T2: Q16.16 标量导出 ────────────────────────────────────────────────────
def to_q1616(num: int, den: int):
    if den == 0:
        return 0, True      # 分母 0 ⇒ true,且返回 0(不除零)
    return round_half_away(num << 16, den), False


# ── T3: F1 指标 ────────────────────────────────────────────────────────────
# Scorable = 有真值 **且** 有判断(GDD F1 逐字);no-judgement 不进分母,
# 「落笔判空集」进分母且判错 —— 两种情形在输入里以 None / frozenset() 区分。
def f1_metrics(cases):
    scorable = len(cases)
    if scorable == 0:
        return (0, 0, 0, 0, True)
    correct = sum(1 for c in cases if c["judged"] == c["truth"])
    revised = sum(1 for c in cases if c["revised"])
    return (correct, scorable, revised, scorable, False)


# ── T4: 五元组计数折叠(TickFoldEngine)────────────────────────────────────
def tick_fold(ticks):
    if not ticks:
        return (0, 0, 0, 0, 0)
    return (len(ticks), len(ticks), sum(ticks), min(ticks), max(ticks))


# ── 夹具输入 ───────────────────────────────────────────────────────────────
# ⚠️ 输入表与 C# 侧夹具**逐字对应**;任一侧改表 ⇒ 两侧都须重签(golden-v2)。

ROUND_CASES = [
    ("R01_positive_exact", 7, 2),          # 3.5  ⇒ 4(ties 远离零)
    ("R02_positive_below", 5, 2),          # 2.5  ⇒ 3(ties-to-even 会给 2 —— 被禁模式)
    ("R03_negative_exact", -7, 2),         # -3.5 ⇒ -4
    ("R04_negative_below", -5, 2),         # -2.5 ⇒ -3(ties-to-even 会给 -2)
    ("R05_zero_numerator", 0, 7),          # 0
    ("R06_zero_denominator", 5, 0),        # 分母 0 ⇒ 0
    ("R07_both_negative", -3, -4),         # 0.75 ⇒ 1
    ("R08_neg_over_pos", -2, 3),           # -0.667 ⇒ -1
    ("R09_third", 1, 3),                   # 0.333 ⇒ 0
    ("R10_two_thirds", 2, 3),              # 0.667 ⇒ 1
    ("R11_large", 123456789, 1000),
    ("R12_tie_away_at_scale", 999999999999, 2),
]

Q1616_CASES = [
    ("Q01_two_thirds", 2, 3),              # 43690.66… ⇒ 43691
    ("Q02_one_third", 1, 3),               # 21845.33… ⇒ 21845
    ("Q03_full", 1, 1),                    # 65536
    ("Q04_zero", 0, 5),                    # 0
    ("Q05_zero_den", 3, 0),                # 0 + zero flag
    ("Q06_half", 1, 2),                    # 32768
    ("Q07_negative_half", -1, 2),          # -32768
    ("Q08_three_halves", 3, 2),            # 98304
]

# F1 夹具:一案一条 CaseClosed(真值)+ 若干 Judgment*。
#   truth / judged = 病种 ordinal 集合;revised = 是否有改写史。
F1_CASES = [
    {"truth": frozenset({"1"}), "judged": frozenset({"1"}), "revised": False},   # 判对
    {"truth": frozenset({"2"}), "judged": frozenset({"3"}), "revised": False},   # 判错
    {"truth": frozenset({"1"}), "judged": frozenset({"1"}), "revised": True},    # 改写后判对
]

F1_CASES_EMPTY = []                       # 分母 0 边界
F1_CASES_ALL_WRONG = [
    {"truth": frozenset({"1"}), "judged": frozenset(), "revised": True},         # 落笔判空集 ⇒ 判错
    {"truth": frozenset({"2"}), "judged": frozenset({"1"}), "revised": False},
]

TICK_CASES = [
    ("K01_typical", [10, 20, 30, 40]),
    ("K02_empty", []),
    ("K03_single", [7]),
    ("K04_negative", [-5, 0, 5]),
    ("K05_duplicates", [3, 3, 3]),
    ("K06_large", [1, 1000000000, 999999999]),
]


def main(argv):
    lines = []
    lines.append("# golden-v1 —— 51 遥测确定性黄金夹具(story-008 AC-51-C3 · ADR-012 双级)")
    lines.append("# 产出:独立 Python 参考实现 golden_v1_reference.py(与 C# 零共享代码)")
    lines.append("# 刷新:升 golden-v2 + 全体平台同时重签(禁单平台独签,禁就地改老数)")
    lines.append("# 值域:R 行 / Q 行 = 十进制整数;Qz 行 = 分母零标记;F1 行 = num,den 对;K 行 = 五元组")

    for name, num, den in ROUND_CASES:
        lines.append("%s %d" % (name, round_half_away(num, den)))

    for name, num, den in Q1616_CASES:
        q, zero = to_q1616(num, den)
        lines.append("%s %d" % (name, q))
        lines.append("%s_z %d" % (name, 1 if zero else 0))

    for label, cases in (("F1_typical", F1_CASES),
                         ("F1_empty", F1_CASES_EMPTY),
                         ("F1_allWrong", F1_CASES_ALL_WRONG)):
        a, ad, b, bd, z = f1_metrics(cases)
        lines.append("%s_a %d,%d" % (label, a, ad))
        lines.append("%s_b %d,%d" % (label, b, bd))
        lines.append("%s_z %d" % (label, 1 if z else 0))

    for name, ticks in TICK_CASES:
        lines.append("%s %s" % (name, ",".join(str(v) for v in tick_folds(ticks))))

    out = "\n".join(lines) + "\n"

    if len(argv) >= 3 and argv[1] == "--check":
        with open(argv[2], "r", encoding="utf-8") as f:
            got = [l.rstrip("\n") for l in f if l.strip() and not l.startswith("#")]
        want = [l for l in out.splitlines() if l.strip() and not l.startswith("#")]
        if got == want:
            print("golden-v1: %d 行全部一致" % len(want))
            return 0
        for i, (g, w) in enumerate(zip(got, want)):
            if g != w:
                print("golden-v1: 首个差异在第 %d 行\n  want: %s\n  got : %s" % (i + 1, w, g))
                break
        else:
            print("golden-v1: 行数不一致 want=%d got=%d" % (len(want), len(got)))
        return 1

    sys.stdout.write(out)
    return 0


def tick_folds(ticks):
    return tick_fold(ticks)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
