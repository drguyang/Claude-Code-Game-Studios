#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
golden_v1_reference.py —— item-database 确定性黄金夹具 golden-v1 的**独立参考实现**
(AC-21a-28;Story 011 · production/epics/item-database/story-011-determinism-golden-fixtures.md)

权威规格(本文件按其手写,与 C# 实现**零共享代码**):
  · GDD design/gdd/item-database.md §Formulas F1 / F2 / F5(经 RecipeSettlementSolver /
    QualityTimelineSolver 的公开契约转述)
  · ADR-006 §Decision 三(ROUND_HALF_AWAY_FROM_ZERO)· ADR-012(双级黄金夹具 · 版本化刷新)
  · ADR-010 / ADR-006 §五(codec = tag(1B) + 定宽小端;按字段名 tag 升序)
  · SplitMix64(Sim.Contracts:Gamma/Avalanche/Fold,链式 avalanche 折叠 D-1 乙案)

**产出纪律(AC-28 防自指)**:golden-v1.txt 由本文件生成 —— 本文件不经由任何 C# 代码、
不读取任何 C# 运行结果;C# 侧测试(determinism_golden_fixtures_test.cs)**只读** golden,
永不回写。复现:`python3 golden_v1_reference.py`(确定性,输出逐位相同)。

canonical 哈希口径(值级):state = u64(v0);逐后续 w: state = Avalanche(state + w);
                        终态再 Avalanche 一次。
canonical 哈希口径(字节级):state = 0;逐 byte b: state = Avalanche(state + b);
                        终态再 Avalanche 一次。
输出:golden-v1.txt(本目录;golden-vN 版本化,刷新须全体平台同时重签 —— ADR-012 裁决③)。
"""
import struct
import sys

MASK = (1 << 64) - 1
ONE = 1 << 16                 # Fix.OneRaw(Q16.16)
GAMMA = 0x9E3779B97F4A7C15
MUL1 = 0xBF58476D1CE4E5B9
MUL2 = 0x94D049BB133111EB

EVENTKIND_CRAFT = 23           # EventKind 枚举序(病史 13 + 病例 5 + 世界第 6 支)
PROCESSING_DRIED = 1           # ProcessingState.Dried


# ─────────────── 整数域原语(ADR-006 §Decision 三)───────────────

def round_half_away(dividend: int, divisor: int) -> int:
    """C# FixParse.RoundHalfAwayFromZero 的逐语义移植(向零截断 + 中点远离零)。"""
    q = abs(dividend) // abs(divisor)
    if (dividend < 0) != (divisor < 0):
        q = -q
    r = dividend - q * divisor
    if r == 0:
        return q
    step = 1 if (r > 0) == (divisor > 0) else -1
    if abs(r) * 2 >= abs(divisor):
        return q + step
    return q


def fix_ratio(n: int, d: int) -> int:
    """FixParse.FromRatio:(n << 16) / d,half-away。"""
    return round_half_away(n << 16, d)


def mul_raw(a: int, b: int) -> int:
    """Fix.MulRaw(Q16.16 中间乘):先乘后舍、幅度域舍入后一次回符号。

    C# 形 = 无符号量积 >>16 + (bit15 ? +1) —— 幅度域 half-away = away-from-zero。
    Python 大整数与该语义逐位等价(mag < 2^80 域内无溢出路径;夹具值远在域内)。
    """
    neg = (a < 0) ^ (b < 0)
    mag = abs(a) * abs(b)
    if mag >> 80:
        raise OverflowError("MulRaw: 中间乘积超出 Q16.16 域")
    q = mag >> 16
    if (mag >> 15) & 1:
        q += 1
    return -q if neg else q


def fix_round(raw: int) -> int:
    """Fix.Round():Q16.16 → 整数,half-away。"""
    return round_half_away(raw, ONE)


def clamp(v: int, lo: int, hi: int) -> int:
    return lo if v < lo else (hi if v > hi else v)


# ─────────────── SplitMix64(链式 avalanche,D-1 乙案)───────────────

def avalanche(z: int) -> int:
    z = ((z ^ (z >> 30)) * MUL1) & MASK
    z = ((z ^ (z >> 27)) * MUL2) & MASK
    return z ^ (z >> 31)


def fold(state: int, w: int) -> int:
    return avalanche((state + w) & MASK)


def hash_values(values) -> str:
    """值级 canonical:首输入为 state,逐后续 Fold,终态 Avalanche 一次。"""
    it = iter(values)
    state = next(it) & MASK
    for w in it:
        state = fold(state, w)
    return f"{avalanche(state):016x}"


def hash_bytes(bs: bytes) -> str:
    """字节级 canonical:state = 0,逐 byte Fold,终态 Avalanche 一次。"""
    state = 0
    for b in bs:
        state = fold(state, b)
    return f"{avalanche(state):016x}"


# ─────────────── F1/F2:唯一配方结算求解器的规格移植 ───────────────

class Constants:
    def __init__(self, qty_min, qty_max, skill_cap_mod, qual_cap_mod, equip_cap,
                 env_min, env_max, ret_min, ret_max, eff_min, eff_max, max_q, skill_cap):
        self.qty_min = qty_min
        self.qty_max = qty_max
        self.skill_cap_mod = skill_cap_mod
        self.qual_cap_mod = qual_cap_mod
        self.equip_cap = equip_cap
        self.env_min = env_min
        self.env_max = env_max
        self.ret_min = ret_min
        self.ret_max = ret_max
        self.eff_min = eff_min
        self.eff_max = eff_max
        self.max_q = max_q
        self.skill_cap = skill_cap


FULL = Constants(
    fix_ratio(1, 4), fix_ratio(2, 1), fix_ratio(1, 10), fix_ratio(1, 10),
    fix_ratio(1, 10), fix_ratio(-1, 1), fix_ratio(1, 2),
    fix_ratio(1, 2), fix_ratio(1, 1), fix_ratio(1, 2), fix_ratio(1, 1),
    max_q=5, skill_cap=60)

ZERO = Constants(
    fix_ratio(1, 4), fix_ratio(2, 1), 0, 0, 0,       # 所有修正 cap 取 0(skill/qual/equip)
    fix_ratio(-1, 1), fix_ratio(1, 2),
    fix_ratio(1, 2), fix_ratio(1, 1), fix_ratio(1, 2), fix_ratio(1, 1),
    max_q=5, skill_cap=60)


def curve_scaled(cap_raw, amount, cap_value):
    """cap × amount / capValue(先乘后除一次性舍入;amount 先钳入 [0, capValue])。"""
    if cap_value <= 0:
        raise ValueError("曲线分母 ≤ 0")
    bounded = 0 if amount < 0 else (cap_value if amount > cap_value else amount)
    return round_half_away(cap_raw * bounded, cap_value)


def interpolate(lo, hi, level, cap):
    """lo + (hi − lo) × level / cap(level 先钳入 [0, cap])。"""
    if cap <= 0:
        raise ValueError("插值分母 ≤ 0")
    bounded = 0 if level < 0 else (cap if level > cap else level)
    return lo + round_half_away((hi - lo) * bounded, cap)


def retain(c, skill):
    return interpolate(c.ret_min, c.ret_max, skill, c.skill_cap)


def efficiency(c, skill):
    return interpolate(c.eff_min, c.eff_max, skill, c.skill_cap)


def output_quality(c, in_q, skill):
    if in_q < 1:
        raise ValueError("InputQuality < 1")
    rounded = fix_round(mul_raw(in_q << 16, retain(c, skill)))
    if rounded > in_q:
        rounded = in_q
    if rounded < 1:
        rounded = 1
    return rounded


def actual_consumed(base, eff_raw):
    if eff_raw <= 0:
        raise ValueError("EFF ≤ 0")
    num = base * ONE
    return (num + eff_raw - 1) // eff_raw        # Ceil(恒正域)


def output_qty(base, qmult_raw):
    raw = mul_raw(base << 16, qmult_raw)
    rounded = fix_round(raw)
    return rounded if rounded >= 1 else 1


def solve(c, outputs, inputs, skill, in_q, equip, climate, clinic):
    """返回 canonical 值向量(顺序见 golden 文件头):
    [outQuality] ++ outputQty[m] ++ actual[n] ++ [eff, qtyMult, env, sigma](raw)。"""
    env = clamp(climate + clinic, c.env_min, c.env_max)
    skill_mod = curve_scaled(c.skill_cap_mod, skill, c.skill_cap)
    qual_mod = curve_scaled(c.qual_cap_mod, in_q - 1, c.max_q - 1)
    sigma = skill_mod + qual_mod + equip + env
    qmult = clamp(ONE + sigma, c.qty_min, c.qty_max)
    eff = efficiency(c, skill)
    if eff <= 0:
        raise ValueError("EFF ≤ 0")

    out_q = [output_qty(b, qmult) for b in outputs]
    actual = [actual_consumed(b, eff) for b in inputs]
    out_quality = output_quality(c, in_q, skill)

    return [out_quality] + out_q + actual + [eff, qmult, env, sigma]


# ─────────────── F5:品级 → 时间轴 ───────────────

PROFILE_INDEX = {"onset": 0, "peak": 1, "half_life": 2, "elimination": 3}


def f5_vector(profile_axes, axis, offsets, quality):
    """profile_axes = (onset, peak, half_life, elimination) 各 raw 或 None(profile 字段序)。
    axis=None 或 offsets 空 ⇒ 四轴原样透传;否则作用轴 += offset[quality−1]。
    输出四元素向量,None → -1(C# 侧 EffectiveTimeline 可空字段同序对位)。"""
    axes = list(profile_axes)
    if axis is not None and offsets:
        idx = quality - 1
        if idx < 0 or idx >= len(offsets):
            raise ValueError("offset 越界")
        axes[PROFILE_INDEX[axis]] = axes[PROFILE_INDEX[axis]] + offsets[idx]
    return [-1 if v is None else v for v in axes]


# ─────────────── codec(tag 1B + 定宽小端;ADR-010)───────────────

def i32(v): return struct.pack("<i", v)
def i64(v): return struct.pack("<q", v)


def craft_payload_bytes():
    """CraftPayload(1, 6, 320, 40, in{3001,3002}, actual{1,2}, outQty{3},
    outQ{1}, outIds{4001}, tool(2,2,2)) —— tag 1..10 升序。"""
    b = b""
    b += bytes([1]) + i32(1)
    b += bytes([2]) + i32(6)
    b += bytes([3]) + i64(320)
    b += bytes([4]) + i64(40)
    b += bytes([5]) + i32(2) + i64(3001) + i64(3002)
    b += bytes([6]) + i32(2) + i32(1) + i32(2)
    b += bytes([7]) + i32(1) + i32(3)
    b += bytes([8]) + i32(1) + i32(1)
    b += bytes([9]) + i32(1) + i64(4001)
    b += bytes([10]) + i32(2) + i32(2) + i32(2)
    return b


def item_instance_bytes():
    """ItemInstance(7, ("willow_bark", Dried), quality=3, qty=5, children{8,9})。
    tag 1 id · 2 base_id(UTF-8)· 3 state · 4 quality · 5 qty · 6 children。"""
    base = "willow_bark".encode("utf-8")
    b = b""
    b += bytes([1]) + i64(7)
    b += bytes([2]) + i32(len(base)) + base
    b += bytes([3]) + i32(PROCESSING_DRIED)
    b += bytes([4]) + i32(3)
    b += bytes([5]) + i32(5)
    b += bytes([6]) + i32(2) + i64(8) + i64(9)
    return b


def sim_event_header_bytes():
    """SimEvent(tick=100, patient=7, seq=5, kind=Craft(23), payload(0,0,30))。
    tag 1 Tick i64 · 2 Patient i32 · 3 Seq i64 · 4 Kind i32 · 5 PayloadRef 3×i32。"""
    b = b""
    b += bytes([1]) + i64(100)
    b += bytes([2]) + i32(7)
    b += bytes([3]) + i64(5)
    b += bytes([4]) + i32(EVENTKIND_CRAFT)
    b += bytes([5]) + i32(0) + i32(0) + i32(30)
    return b


# ─────────────── 场景表(与 C# 侧测试逐名对应;输入漂移会被 golden 比对抓红)───────────────
# (name, constants, skill, quality, outputs, inputs, equip, climate, clinic)
ENV_NEG1 = fix_ratio(-1, 1)
ENV_HALF = fix_ratio(1, 2)
ENV_POS1 = fix_ratio(1, 1)

SCENARIOS = [
    ("S01_full_skill0_q3_m1",        FULL, 0,  3, [2],    [3, 7, 1], 0,        0,        0),
    ("S02_full_skill60_q5_m1",       FULL, 60, 5, [2],    [3, 7, 1], 0,        0,        0),
    ("S03_full_skill0_q1_n1m1",      FULL, 0,  1, [1],    [5],       0,        0,        0),
    ("S04_full_skill30_q5_m2",       FULL, 30, 5, [2, 3], [4, 6],    0,        0,        0),
    ("S05_full_skill0_q3_envMin",    FULL, 0,  3, [1, 2], [3, 7, 1], 0,        ENV_NEG1, 0),
    ("S06_full_skill60_q5_envMax",   FULL, 60, 5, [2],    [3, 7, 1], 0,        ENV_HALF, 0),
    ("S07_full_envMinBoth_restFull", FULL, 0,  1, [1],    [5],       0,        ENV_NEG1, ENV_NEG1),
    ("S08_full_envMaxBoth_restFull", FULL, 60, 5, [2, 3], [4, 6],    ENV_HALF, ENV_HALF, ENV_HALF),
    ("S09_zero_caps_floor",          ZERO, 0,  1, [1],    [5],       0,        ENV_NEG1, 0),
    ("S10_zero_caps_envOverMax",     ZERO, 60, 5, [2, 2], [3, 3],    0,        ENV_POS1, 0),
    ("S11_zero_caps_mid",            ZERO, 30, 3, [3],    [3, 7, 1], 0,        0,        0),
    ("S12_full_skill60_q1_m1",       FULL, 60, 1, [2],    [3, 7, 1], 0,        0,        0),
]

# F5 场景(profile 轴四元 = (onset, peak, half_life, elimination);None 表缺席)
P1_AXES = (fix_ratio(1, 1), fix_ratio(2, 1), fix_ratio(5, 1), fix_ratio(10, 1))
P1_OFFSETS = [fix_ratio(0, 1), fix_ratio(3, 8), fix_ratio(-1, 4),
              fix_ratio(1, 4), fix_ratio(1, 2)]

F5_SCENARIOS = [
    # (name, profile_axes, axis, offsets, quality) —— 未给 axis/offsets 的走原样透传
    ("S13_f5_halfLife_q1_offset0",     P1_AXES, "half_life", P1_OFFSETS, 1),
    ("S14_f5_halfLife_q5_offsetMax",   P1_AXES, "half_life", P1_OFFSETS, 5),
    ("S15_f5_halfLife_q3_offsetNeg",   P1_AXES, "half_life", P1_OFFSETS, 3),
    ("S16_f5_noAxis_passthrough",      P1_AXES, None,        None,       3),
]

BYTE_SCENARIOS = [
    ("B01_craft_payload_bytes",    craft_payload_bytes),
    ("B02_item_instance_bytes",    item_instance_bytes),
    ("B03_sim_event_header_bytes", sim_event_header_bytes),
]


def main():
    lines = []
    lines.append("# golden-v1 —— item-database 确定性黄金夹具(AC-21a-28 · Story 011 · ADR-012 双级)")
    lines.append("#")
    lines.append("# 产出:独立 Python 参考实现 golden_v1_reference.py(本目录;与 C# 实现零共享代码)")
    lines.append("#       按 GDD F1/F2/F5 + ADR-006 舍入 + ADR-010 codec 布局手写。2026-09-24。")
    lines.append("#       复现 = python3 golden_v1_reference.py(确定性输出)。")
    lines.append("#       防自指:C# 测试只读本文件、永不回写;金标准非由 C# 首跑生成(AC-28)。")
    lines.append("# 评审:入库即提交评审(提交历史可证);刷新须升 golden-v2、全体平台同时重签、")
    lines.append("#       禁单平台独签、旧版保留回归对比(ADR-012 裁决③)。")
    lines.append("#")
    lines.append("# canonical(值级): state=u64(v0);逐后续 w: state=Avalanche(state+w);终态 Avalanche 一次。")
    lines.append("# canonical(字节级): state=0;逐 byte: state=Avalanche(state+b);终态 Avalanche 一次。")
    lines.append("# S 系列向量序: [outQuality] ++ OutputQty[m] ++ ActualConsumed[n] ++")
    lines.append("#                [Efficiency.Raw, QtyMultiplier.Raw, EnvModTotal.Raw, SumOfModifiers.Raw]")
    lines.append("# S13–S16 向量序: [onset, peak, half_life, elimination](profile 字段序;缺席 = -1)")
    lines.append("# 格式: <name> <hex16>")
    lines.append("#")
    lines.append("# 场景边界覆盖: caps 满/0 · ENV_MOD MIN/MAX/两极叠加钳制 · quality 1/MAX ·")
    lines.append("#               EFF 两极(skill 0/60)+ 中点(30)· m=1/m>1 · F5 偏移 0/负/正/透传。")

    for name, c, skill, in_q, outs, ins, equip, climate, clinic in SCENARIOS:
        vec = solve(c, outs, ins, skill, in_q, equip, climate, clinic)
        lines.append(f"{name} {hash_values(vec)}")

    for name, axes, axis, offsets, quality in F5_SCENARIOS:
        vec = f5_vector(axes, axis, offsets, quality)
        lines.append(f"{name} {hash_values(vec)}")

    for name, fn in BYTE_SCENARIOS:
        lines.append(f"{name} {hash_bytes(fn())}")

    out_path = sys.argv[1] if len(sys.argv) > 1 else "golden-v1.txt"
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"wrote {out_path} ({len([l for l in lines if not l.startswith('#')])} entries)")


if __name__ == "__main__":
    main()
