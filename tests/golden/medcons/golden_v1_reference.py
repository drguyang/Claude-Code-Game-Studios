#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
golden_v1_reference.py —— medcons(53)确定性黄金夹具 golden-v1 的**独立参考实现**
(S12-9 · story-002 · AC-53-02 / AC-53-15)

权威规格(本文件按其手写,与 C# 实现**零共享代码**):
  · GDD design/gdd/medical-consequences.md §F-53.1(Outcome = Judge(P,C,D,U) · 双轴 · 枚举闭集)
  · GDD 规则三(U = SplitMix64(WorldSeed, "case-salt", salted_key) · ADR-008 §五)
  · OQ-53-9(试药史 = 同一 patient 连续 DrugTreatmentApplied、无 JudgmentRecorded 支撑;
    run 计数 + 支持性判断打断;阈值留槽)
  · ADR-012(双级黄金夹具 · 版本化刷新 · 禁单平台独签)
  · SplitMix64 链式 avalanche 折叠(D-1 乙案,2026-09-22 用户裁定):state = 首输入;
    对每个后续输入 w:state = Avalanche(state + w);终态再 Avalanche 一次。
    加法 mod 2^64 回绕;tag 编码块 = [字节数] + 每 8 字节**大端**补零块。

复现 = python3 golden_v1_reference.py(确定性输出,写 golden-v1.txt 同目录)。

⚠️ 「有符号右移负夹具」(N01):以 signed 算术右移变体计算同输入的哈希 ——
   它与正确值**必须不同**(逐位纪律 ADR-012 F7:全部算术落 ulong + unchecked,
   无符号回绕是定义性语义;signed >> 30/27/31 在高位为 1 时符号扩展 ⇒ 漂移)。
   测试断言 computed != N01,防将来有人把位移改回 signed 语义而夹具仍绿。
"""

import struct

MASK64 = (1 << 64) - 1


def u64(x):
    """C# ulong 语义:mod 2^64 回绕(定义性,非错误)。"""
    return x & MASK64


# ── SplitMix64 原语(与 C# SplitMix64.cs 零共享代码的独立转述)──────────────

GAMMA = 0x9E3779B97F4A7C15
MUL1 = 0xBF58476D1CE4E5B9
MUL2 = 0x94D049BB133111EB


def avalanche(z):
    z = u64((z ^ (z >> 30)) * MUL1)
    z = u64((z ^ (z >> 27)) * MUL2)
    return u64(z ^ (z >> 31))


def fold(state, w):
    return avalanche(u64(state + w))


def to_u64_signed(x):
    """C# (ulong)x 的位型转换:long 按位重解释(负哨兵 → 高位全 1)。"""
    return u64(struct.unpack('<Q', struct.pack('<q', x))[0])


def fold_tag(state, tag):
    b = tag.encode('ascii')
    state = fold(state, len(b) & 0xFFFFFFFF)
    for i in range(0, len(b), 8):
        chunk = 0
        for j in range(8):
            v = b[i + j] if i + j < len(b) else 0
            chunk = u64((chunk << 8) | v)      # 大端:首字节落高位
        state = fold(state, chunk)
    return state


def hash_tagged(a, tag, b):
    state = fold_tag(to_u64_signed(a), tag)
    return avalanche(fold(state, to_u64_signed(b)))


def signed_shift_variant(a, tag, b):
    """负夹具:avalanche 内部位移按 signed 算术右移语义(错误实现)。
    正确实现全部位移落 ulong(无符号);本变体在高位为 1 时符号扩展 ⇒ 漂移。
    测试断言 正确值 != 本变体值(且 == golden 的 N01 行),防位移被改回 signed 语义。"""

    def s_avalanche(z):
        sz = z - (1 << 64) if z >> 63 else z
        sz = u64((sz ^ (sz >> 30)) * MUL1)          # 算术右移(符号扩展)
        sz = u64((sz ^ (sz >> 27)) * MUL2)
        sz = u64(sz ^ (sz >> 31))
        return sz

    state = fold_tag(to_u64_signed(a), tag)
    return s_avalanche(fold(state, to_u64_signed(b)))


# ── U 派生(GDD F-53.1:U ∈ Fix[0,1);取哈希高 16 位 = Q16.16 raw)────────────

def u_raw(seed, salted_key):
    return hash_tagged(seed, "case-salt", salted_key) >> 48


def u_raw_degraded():
    """退化路径(未认出模式):显式中点 1/2(Q16.16 raw = 0x8000 = 32768)。非 0、非静默。"""
    return 32768


# ── 判定表键域(story-002 用户裁定 3×3×2 + U 档列;夹具符号值,行值归数值轮)──

P_LANES = ("empty", "suspected", "confirmed")
C_LANES = ("none", "applied_unmatched", "applied_matched")
D_LANES = ("recovered", "not_recovered")


def fixture_outcome(p, c, d):
    """夹具符号映射(文档化于 medcons_outcome_judge_test.cs,与 C# 镜像):
      · recovered 且 c != matched              → no_echo(自愈无痕 —— 误诊自愈判据)
      · c == matched 且 p == confirmed          → no_echo(对因且确诊 = 治好无痕)
      · c == matched 且 p != confirmed          → residual
      · not_recovered 且 c != matched           → confirmed ? recurrence : residual
    结局轴(PatientLives/Loses)由 9 结算,不进判定表(F-53.2 定义域收窄同构)。"""
    if d == "recovered" and c != "applied_matched":
        return "no_echo"
    if c == "applied_matched":
        return "no_echo" if p == "confirmed" else "residual"
    return "recurrence" if p == "confirmed" else "residual"


# 18 行全组合(构建期校验 ① 的覆盖域;行值 = 上述符号规则)
TABLE_ROWS = {(p, c, d): fixture_outcome(p, c, d)
              for p in P_LANES for c in C_LANES for d in D_LANES}

TRIAL_THRESHOLD = 3   # 留槽:数值轮(story-002 Implementation Note 6)


def judge(p, c, d, has_trial, has_salt, seed, salted_key):
    """Judge 参考形(与 C# OutcomeJudge 镜像):
      ① 试药史痕迹覆盖表行(OQ-53-9 形状;阈值留槽,夹具以布尔痕迹位驱动);
      ② U 档切分:band_count = 1(P0 默认)⇒ band 恒 0 —— U 只进派生夹具,
        不改变判定行(数值轮扩档后此处才分支);
      ③ has_salt=False(未认出模式)⇒ U 走退化中点(32768)—— 与真实路径同 band 语义。"""
    if has_trial:
        return "trial_history"          # OQ-53-9:试药史痕迹覆盖表行(符号形状)
    band = 0                            # band_count = 1(P0 默认档)⇒ band 恒 0
    return TABLE_ROWS[(p, c, d)] if band == 0 else "no_echo"


# ── 20 例 tuple 夹具(AC-53-02 的 Given:同一 (WorldSeed, salted_key, 判断, 处置, 病程))──
# (name, seed, salted_key|None, p, c, d, has_trial)
TUPLES = [
    ("B01", 1234, 7,  "confirmed", "applied_matched",   "recovered",     False),
    ("B02", 1234, 7,  "empty",     "applied_matched",   "recovered",     False),
    ("B03", 1234, 7,  "suspected", "applied_matched",   "not_recovered", False),
    ("B04", 1234, 7,  "confirmed", "none",              "recovered",     False),
    ("B05", 1234, 7,  "confirmed", "none",              "not_recovered", False),
    ("B06", 1234, 7,  "empty",     "none",              "not_recovered", False),
    ("B07", 1234, 7,  "suspected", "applied_unmatched", "recovered",     False),
    ("B08", 1234, 7,  "confirmed", "applied_unmatched", "not_recovered", False),
    ("B09", 999,  0,  "empty",     "none",              "recovered",     False),
    ("B10", -5,   -1, "confirmed", "applied_matched",   "recovered",     False),
    ("B11", (1 << 63) - 1, (1 << 63) - 1, "suspected", "none", "not_recovered", False),
    ("B12", 1234, 7,  "confirmed", "applied_matched",   "recovered",     True),
    # ⚠️ B13 的 c 路不可为 none:trial=True 要求 ≥3 条连续 Drug 行 ⇒ C 路必有处置
    #   (C# 侧从同一 Inputs53 同时算两路,组合须可实现;trial 覆盖表行 ⇒ outcome 不变)
    ("B13", 1234, 7,  "empty",     "applied_unmatched", "recovered",     True),
    ("B14", -5,   -1, "suspected", "applied_unmatched", "not_recovered", True),
    ("B15", 777,  0xFFFFFFFF, "confirmed", "none", "not_recovered", False),
    ("B16", 777,  0x7FFFFFFFFFFFFFFF, "empty", "applied_unmatched", "recovered", False),
    ("B17", -(1 << 63), 0xFFFFFFFFFFFFFFFF, "suspected", "applied_matched", "not_recovered", False),
    ("B18", 42,   1234567890123, "confirmed", "applied_unmatched", "recovered", False),
    ("B19", 1234, 7,  "suspected", "none",              "recovered",     False),
    ("B20", 0,    None, "empty", "none", "recovered", False),   # 退化路径(无 salted_key)
]

# A 组:case-salt 原语黄金向量(键序 / 混盐写法逐位钉)
A_VECTORS = [
    ("A01", 0, 0),
    ("A02", 1234, 7),
    ("A03", -5, -1),
    ("A04", (1 << 63) - 1, (1 << 63) - 1),
    ("A05", 0xABCD, 3),
]


def main():
    lines = []
    lines.append("# golden-v1 —— medcons(53)确定性黄金夹具(S12-9 · story-002 · ADR-012 双级)")
    lines.append("#")
    lines.append("# 产出:独立 Python 参考实现 golden_v1_reference.py(本目录;与 C# 实现零共享代码)")
    lines.append("#       按 GDD F-53.1 + SplitMix64 D-1 乙案 + story-002 键域裁定手写。2026-10-15。")
    lines.append("#       复现 = python3 golden_v1_reference.py(确定性输出)。")
    lines.append("# 刷新 = 全体平台同时重签 + 旧版保留回归对比(禁单平台独签,ADR-012 §三)。")
    lines.append("#")
    lines.append("# 值域语义:哈希行 = u64 十六进制;U 行 = Q16.16 raw 十进制;判定行 = Outcome 枚举名。")
    lines.append("# T 组(18 行)= 判定表逐行值,由 TABLE_ROWS 产出(`TD-medcons-02` 结案 · 2026-10-30)。")
    lines.append("")

    for name, seed, key in A_VECTORS:
        h = hash_tagged(seed, "case-salt", key)
        lines.append(f"{name}_casesalt_seed{seed}_key{key} {h:016X}")
        if name == "A02":
            lines.append(f"N01_signedshift_variant_sameInputsAsA02 {signed_shift_variant(seed, 'case-salt', key):016X}")
        lines.append(f"{name}_uraw {hash_tagged(seed, 'case-salt', key) >> 48}")

    lines.append(f"C01_degraded_uraw {u_raw_degraded()}")
    lines.append("")

    for name, seed, key, p, c, d, trial in TUPLES:
        outcome = judge(p, c, d, trial, key is not None, seed, key or 0)
        lines.append(f"{name}_outcome {outcome}")

    lines.append("")

    # ── T 组:判定表 18 行(`TD-medcons-02` 结案件 · 2026-10-30)────────────────────
    # 为什么要有这一段:在此之前 golden 里**一行判定表都没有**,而 story-002 写的是
    # 「表值与 Python 参考实现同表」—— 那句当时只能由 C# 测试里**手写的夹具镜像**
    # `FixtureOutcome` 背书,即「规则在 .py 写一次、在 .cs 里再抄一次」的**两份真源**。
    # 抄错了两边一起错,测试照绿。现在 18 行由本参考实现直接产出,C# 读 golden 断言真表,
    # 手写镜像已删(`medcons_outcome_judge_test.cs`)。
    # ⚠️ 本段是**纯增量**:A / N / C / B 各组行逐字节未动(ADR-012 §三 的「全平台重签」
    #    针对值变更,此处既有值一位未改)。
    for p in P_LANES:
        for c in C_LANES:
            for d in D_LANES:
                lines.append(f"T_{p}_{c}_{d}_outcome {TABLE_ROWS[(p, c, d)]}")

    lines.append("")

    out_path = "golden-v1.txt"
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"written {out_path}: {len(lines)} lines")
    for l in lines:
        if l and not l.startswith("#"):
            print(" ", l)


if __name__ == "__main__":
    main()
