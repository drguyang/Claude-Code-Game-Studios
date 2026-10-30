#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
golden_v2_reference.py —— `Seq` 发号器的**独立参考实现**(`TD-itemdb-02` 的解除件)

权威规格(本文件按其手写,与 C# 实现**零共享代码**):
  · `design/gdd/disease-simulation.md:196-198`(Seq 发号规则)
  · `unity/Assets/Sim/EventStream.cs:262-292`(四条分支,**按散文转述**手写,
    **不抄代码、不读 C# 运行结果**)
  · ADR-012 裁决③(版本化刷新:新版本目录 + 变更日志 + 全体平台同时重签 + 旧版保留)
  · ADR-006 Amendment A(`SimEvent` 补 `Seq`)
  · `TD-persist-03`(`_restoredGroupMax` 恢复批播种,只抬不压)

**为什么这条金标准此前不存在**(`TD-itemdb-02` 原判定):
既有四份金标准(item_database / medcons / telemetry / `golden_hash_v1_test.cs`)
**无一族场景是发号** —— `grep -rn -e '_currentSeq' -e 'seq_reset' tests/` 零命中,
Python 侧没有任何发号参考实现。

**产出纪律(AC-28 防自指)**:golden-v2.txt 由本文件生成 —— 本文件不经由任何 C# 代码、
不读取任何 C# 运行结果;C# 侧测试**只读** golden,永不回写。
复现:`python3 golden_v2_reference.py`(确定性,输出逐位相同)。

═══════════════════════════════════════════════════════════════════════════
发号规则(四条分支,逐条对应 `EventStream.cs`)
═══════════════════════════════════════════════════════════════════════════
令 group = (Tick, Patient)。逐事件 e(按入流顺序):

  ① 组复位      `e.Tick != lastTick || e.Patient != lastPatient`
                ⇒ currentSeq = 0;lastTick / lastPatient := e 的
                (`EventStream.cs:268-273`)
  ② 同组递增    `e.Tick == lastTick && e.Patient == lastPatient`
                ⇒ currentSeq++      (`:275-277`)
  ③ 恢复批播种  `restoredGroupMax[(tick, patient)]` 存在且 `groupMax + 1 > currentSeq`
                ⇒ currentSeq = groupMax + 1(**只抬不压** —— `gmax+1 ≤ currentSeq` 时不扰)
                (`:280-287`)
  ④ 显式 Seq 门 `e.Seq < 0` ⇒ 入流 Seq := currentSeq;`e.Seq >= 0` ⇒ 原样保留
                (`:291-295`)

⚠️ **③ 与 ② 的执行顺序在本实现里不可观测**(2026-10-30 起草期自查订正,原稿写「顺序不可交换」是**错的**):
   ③ 只可能在**组的首条事件**上抬号(同组后续事件的计数器已 ≥ 组最大号 + 1,`gmax+1 > currentSeq` 恒假),
   而组首条必走 ① 复位分支、**② 根本不执行** ⇒ 「③ 在 ② 前还是后」在任何输入下都产出**同一序列**。
   ⇒ 本金标准**不主张**钉住该顺序;`S07` 钉的是「播种在同组**非首条**上仍生效(经由 ② 的 ++ 结果体现)」。
   ⚠️ 若将来有人把 ③ 挪到 ① 之前,`S05`/`S06`/`S07`/`S11` 会转红(那时它就不再是「组首条抬号」)。

⚠️ **④ 的关键**:显式 `Seq >= 0` 的事件**原样保留其 Seq,但计数器仍前进**
   (`event_stream_test.cs:155-158` 逐字钉死)。即「显式号不占位」是**错的** —— 它占位。

⚠️ **哨兵**:`Seq = -1` 表示「未发号」;`Seq = 0` 是**合法首号**。两者不可混淆
   (O-1 修复,2026-10-09)。

输出:golden-v2.txt(本目录)。格式: `<name> <seq0>,<seq1>,...`
      (逗号分隔的**入流后 Seq 序列**,按入流顺序)。
"""
import sys

# ── 哨兵(与 C# 同值,但本文件是独立转述,不 import 任何 C# 侧)──
SEQ_UNASSIGNED = -1

# ── 事件种类:只用来做可读的场景命名,不参与发号计算 ──
#    ⚠️ 刻意**不**用 EventKind 的数值 ordinal —— 发号与 Kind 无关
#    (四元组去重键里 Kind 只是身份的一部分,不参与 Seq 计算)。
K_CASE_CLOSED = "CaseClosed"
K_CRAFT = "Craft"
K_STRUCTURE_PLACED = "StructurePlaced"


def assign_seqs(events, restored_group_max=None):
    """按 `EventStream.cs:262-295` 的四条分支发号。

    :param events: [(tick, patient, explicit_seq)] —— explicit_seq = -1 表示未发号。
    :param restored_group_max: {(tick, patient): max_seq} —— 恢复批的组最大号(③ 用)。
                               纯 Append 会话传 None / {} ⇒ ③ 零效果。
    :return: 入流后的 Seq 列表(与 events 同长、同序)。

    ⚠️ 逐行对照 C# 的分支序(**不可重排**):
       ① 组复位 / ② 同组递增 → ③ 播种 → ④ 显式门
    """
    if restored_group_max is None:
        restored_group_max = {}

    seqs = []
    current_seq = 0          # C# 初值 0(字段默认)
    last_tick = None
    last_patient = None

    for tick, patient, explicit in events:
        # ── ①② 组判定 ──
        # ⚠️ 首个事件时 last_* 是 None ⇒ 必走 ① 复位分支(C# 的 _lastSeqTick /
        #    _lastSeqPatient 初值同为「未设」语义;此处用 None 表达)。
        if tick != last_tick or patient != last_patient:
            current_seq = 0                       # ① 组复位
            last_tick = tick
            last_patient = patient
        else:
            current_seq += 1                      # ② 同组递增

        # ── ③ 恢复批播种(只抬不压)──
        key = (tick, patient)
        if key in restored_group_max:
            group_max = restored_group_max[key]
            if group_max + 1 > current_seq:
                current_seq = group_max + 1

        # ── ④ 显式 Seq 门 ──
        if explicit < 0:
            seqs.append(current_seq)              # 未发号 ⇒ 取计数器
        else:
            seqs.append(explicit)                 # 显式号 ⇒ 原样保留
            # ⚠️ **计数器仍已前进**(上面 ①② 已经跑过)—— 这就是「显式号占位」语义。

    return seqs


# ═══════════════════════════════════════════════════════════════════════
# 场景集(覆盖四条分支各一 + 边界,`TD-itemdb-02` 修法方向 ②)
# ═══════════════════════════════════════════════════════════════════════

def S01_group_reset_by_tick():
    """① 组复位 —— Tick 变 ⇒ 计数器归 0。"""
    return assign_seqs([
        (0, 1, SEQ_UNASSIGNED),   # 首条 ⇒ 0
        (0, 1, SEQ_UNASSIGNED),   # 同组 ⇒ 1
        (1, 1, SEQ_UNASSIGNED),   # Tick 变 ⇒ 归 0
    ])


def S02_group_reset_by_patient():
    """① 组复位 —— Patient 变(同 Tick)⇒ 计数器归 0。"""
    return assign_seqs([
        (0, 1, SEQ_UNASSIGNED),   # ⇒ 0
        (0, 1, SEQ_UNASSIGNED),   # ⇒ 1
        (0, 2, SEQ_UNASSIGNED),   # Patient 变 ⇒ 归 0
    ])


def S03_same_group_increment():
    """② 同组递增 —— 序号 0 / 1 / 2。"""
    return assign_seqs([
        (0, 1, SEQ_UNASSIGNED),
        (0, 1, SEQ_UNASSIGNED),
        (0, 1, SEQ_UNASSIGNED),
    ])


def S04_same_tick_two_crafts_patientNone():
    """`TD-processing-07` 要的判别力场景:同 tick 两条 `Craft`,均 `Patient = None`。

    ⚠️ 这正是 `TD-itemdb-02` 立债时点出的陷阱面 —— 若发号按 `(Tick, Patient)` 复位
    且 `Patient = None`,「每 tick 单 Craft」的夹具会让每条都是新组 ⇒ Seq 恒 0
    ⇒ 断言数学上恒真。本条**同 tick 两条** ⇒ 0 与 1,**可判别**。
    """
    NONE = -1                    # PatientId.None = -1(ADR-007 §四 哨兵)
    return assign_seqs([
        (0, NONE, SEQ_UNASSIGNED),   # ⇒ 0
        (0, NONE, SEQ_UNASSIGNED),   # ⇒ 1(同组,证明 Patient=None 也成组)
    ])


def S05_restore_then_append_sameGroup():
    """③ 恢复批播种的**真实形态**:`Restore` 一批 → 再 `Append` 同组新事件。

    ⚠️ **本场景的形状由 C# 的公开 API 决定**(2026-10-30 起草期自查订正):
       `_restoredGroupMax` 只能经 `EventStream.Restore(events, foldedRows)` 填充,
       而 `Restore` **自己会重放这些事件**(`EventStream.cs:171-175` 的 `AppendCore` 循环)
       ⇒ 「空流 + 预置组最大号」这个状态**在公开 API 上不可达**。
       故 ③ 的唯一真实形态 = **恢复后的增量 `Append`**。
       既有夹具先例:`Tests/EditMode/PersistenceService/eventstream_restore_test.cs:109-125`。

    逐条推演(恢复批 = `(tick 5, patient 1, seq 2)`,即 gmax = 2;然后 Append 一条同组):
      · 恢复事件:`Restore` 内走 `AppendCore` ⇒ ① 复位 ⇒ 0;③ `2+1=3 > 0` ⇒ 抬到 3;
        ④ 显式 `Seq=2 >= 0` ⇒ **原样 2**(计数器停在 3)
      · 新 Append:`② ++ ⇒ 4`;③ `3 > 4` 假 ⇒ 不抬 ⇒ 入流 **4**
    ⇒ `2,4`(既有夹具 `:120` 的 `Assert.AreEqual(4L, …)` 与此逐位相同)。

    ⚠️ **中间跳号(3)是设计,不是缺陷** —— 承既有夹具 `:113-115` 的原话:
       「恢复组后的首个新号可能跳一个(3 空缺)—— **身份唯一性是判据,密度不是**」。
    """
    return assign_seqs([
        (5, 1, 2),                 # 恢复批事件:显式 Seq=2,计数器被 ③ 抬到 3
        (5, 1, SEQ_UNASSIGNED),    # 恢复后的增量:② ++ ⇒ 4
    ], restored_group_max={(5, 1): 2})


def S06_restored_seed_notOverridden_byExplicitSeq():
    """③ 与 ④ 的交互 —— 显式 `Seq` 原样保留,**但播种值不被它回压**。

    恢复批 = `(tick 5, patient 1, seq 0)`(gmax = 0);再 Append 两条同组:
      · 恢复事件:① 复位 ⇒ 0;③ `0+1=1 > 0` ⇒ 抬到 1;④ 显式 0 ⇒ 入流 **0**
      · 第 2 条:② ++ ⇒ 2;③ `1 > 2` 假 ⇒ 不抬 ⇒ **2**
      · 第 3 条:② ++ ⇒ 3 ⇒ **3**
    ⇒ `0,2,3`。

    ⚠️ **判别力**:若把 ③ 去掉 ⇒ `0,1,2`(**可区分**);若把 ③ 改成「压」⇒ 同样 `0,1,2`
       ⇒ **本条同时钉住「播种存在」与「播种不被显式号回压」**。
    """
    return assign_seqs([
        (5, 1, 0),                 # 恢复批:显式 Seq=0(合法首号,O-1)
        (5, 1, SEQ_UNASSIGNED),
        (5, 1, SEQ_UNASSIGNED),
    ], restored_group_max={(5, 1): 0})


def S07_restore_twoGroups_thenAppend():
    """③ 多组恢复 + 增量 —— 组最大号表**逐组**独立。

    恢复批两条: `(tick 5, patient 1, seq 1)` 与 `(tick 5, patient 2, seq 4)`;
    然后 Append 两条,同 `(5,1)` 与同 `(5,2)`。

    逐条推演:
      · 恢复① `(5,1,1)`:① 复位 ⇒ 0;③ `1+1=2 > 0` ⇒ 抬到 2;④ 显式 1 ⇒ **1**(计数器停 2)
      · 恢复② `(5,2,4)`:① **Patient 变** ⇒ 复位 0;③ `4+1=5 > 0` ⇒ 抬到 5;④ 显式 4 ⇒ **4**(计数器 5)
      · Append③ `(5,1,-1)`:① **Patient 变** ⇒ 复位 0(② 不跑);
        ③ `gmax(5,1)=1` ⇒ `1+1=2 > 0` ⇒ 抬到 2 ⇒ **2**
      · Append④ `(5,2,-1)`:① 变 ⇒ 复位 0;③ `gmax(5,2)=4` ⇒ `4+1=5 > 0` ⇒ 抬到 5 ⇒ **5**
    ⇒ `1,4,2,5`

    ⚠️ **判别力**:若组最大号表错写成**单值**(而非逐 `(Tick, Patient)` 键)⇒ 两条增量
       都会用同一个组最大号 ⇒ 其中一条撞号 ⇒ **可区分**。这正是复合键的必要性。
    """
    return assign_seqs([
        (5, 1, 1),
        (5, 2, 4),
        (5, 1, SEQ_UNASSIGNED),
        (5, 2, SEQ_UNASSIGNED),
    ], restored_group_max={(5, 1): 1, (5, 2): 4})


def S08_explicit_seq_preserved():
    """④ 显式 `Seq >= 0` 原样保留。"""
    return assign_seqs([
        (0, 1, 7),                # 显式 7 ⇒ 原样 7(计数器仍从 0 起步并已前进)
        (0, 1, SEQ_UNASSIGNED),   # 同组 ⇒ 计数器 1 ⇒ 1(**不是** 8)
    ])


def S09_explicit_seq_still_advances_counter():
    """④ **显式号占位** —— 显式事件后,同组下一条取计数器(不是显式号 + 1)。

    ⚠️ 这条是 `event_stream_test.cs:155-158` 逐字钉死的语义:
       显式 `Seq` 原样保留,**但计数器仍前进**。
    本场景:显式 100 之后同组再来一条 ⇒ 得 1(计数器 0→1),**不是** 101。
    """
    return assign_seqs([
        (0, 1, 100),              # 显式 100;计数器仍从 0 起步
        (0, 1, SEQ_UNASSIGNED),   # ⇒ 计数器 ++ ⇒ 1
    ])


def S10_sentinel_vs_legal_zero():
    """哨兵 `Seq = -1`(未发号)vs 合法 `Seq = 0`(**已发号**)不可混淆(O-1)。"""
    return assign_seqs([
        (0, 1, SEQ_UNASSIGNED),   # 未发号 ⇒ 取计数器 0
        (0, 1, 0),                # **显式 0** ⇒ 原样 0(与上一条同值,但来源不同)
    ])


def S11_mixed_append_branches():
    """**纯 Append 会话**的复合场景 —— 走 ① / ② / ④。

    ⚠️ **刻意不含 ③**(2026-10-30 起草期自查订正):`_restoredGroupMax` 只能经 `Restore`
       填充,而 `Restore` 会重放事件 ⇒ 「纯 Append 会话」的组最大号表**恒空**
       (`EventStream.cs:280` 的 `_restoredGroupMax.Count > 0` 恒假)。
       把 ③ 塞进纯 Append 场景 = 造一个**公开 API 不可达**的状态。
       ⇒ ③ 的真实形态见 `S05` / `S06` / `S07`(`Restore` → 增量 `Append`)。

    逐条推演:
      · `(0,1,-1)`:① 复位 ⇒ 0
      · `(0,1,-1)`:② ++ ⇒ 1
      · `(0,1,42)`:② ++ ⇒ 2;④ 显式 ⇒ **42**
      · `(0,1,-1)`:② ++ ⇒ 3
      · `(1,1,-1)`:① Tick 变 ⇒ 0
      · `(1,None,-1)`:① Patient 变 ⇒ 0
      · `(1,None,-1)`:② ++ ⇒ 1
    ⇒ `0,1,42,3,0,0,1`
    """
    NONE = -1
    return assign_seqs([
        (0, 1, SEQ_UNASSIGNED),      # ① 复位 ⇒ 0
        (0, 1, SEQ_UNASSIGNED),      # ② ++ ⇒ 1
        (0, 1, 42),                  # ② ++ ⇒ 2,④ 显式 ⇒ 42
        (0, 1, SEQ_UNASSIGNED),      # ② ++ ⇒ 3
        (1, 1, SEQ_UNASSIGNED),      # ① Tick 变 ⇒ 0
        (1, NONE, SEQ_UNASSIGNED),   # ① Patient 变 ⇒ 0
        (1, NONE, SEQ_UNASSIGNED),   # ② ++ ⇒ 1
    ])


def S12_empty_stream():
    """空流 ⇒ 空序列(不除零、不初始化异常)。"""
    return assign_seqs([])


SCENARIOS = [
    ("S01_group_reset_by_tick", S01_group_reset_by_tick),
    ("S02_group_reset_by_patient", S02_group_reset_by_patient),
    ("S03_same_group_increment", S03_same_group_increment),
    ("S04_same_tick_two_crafts_patientNone", S04_same_tick_two_crafts_patientNone),
    ("S05_restore_then_append_sameGroup", S05_restore_then_append_sameGroup),
    ("S06_restored_seed_notOverridden_byExplicitSeq",
     S06_restored_seed_notOverridden_byExplicitSeq),
    ("S07_restore_twoGroups_thenAppend", S07_restore_twoGroups_thenAppend),
    ("S08_explicit_seq_preserved", S08_explicit_seq_preserved),
    ("S09_explicit_seq_still_advances_counter", S09_explicit_seq_still_advances_counter),
    ("S10_sentinel_vs_legal_zero", S10_sentinel_vs_legal_zero),
    ("S11_mixed_append_branches", S11_mixed_append_branches),
    ("S12_empty_stream", S12_empty_stream),
]


def main():
    lines = [
        "# golden-v2 —— `Seq` 发号器确定性黄金夹具(`TD-itemdb-02` 的解除件)",
        "#",
        "# 产出:独立 Python 参考实现 golden_v2_reference.py(本目录;与 C# 实现零共享代码)",
        "#       按 `EventStream.cs:262-295` 的四条分支 + `disease-simulation.md:196-198` 手写。",
        "#       2026-10-30。",
        "#       复现 = python3 golden_v2_reference.py(确定性输出)。",
        "#       防自指:C# 测试只读本文件、永不回写;金标准非由 C# 首跑生成(AC-28)。",
        "# 版本化:本件为 **v2**(与 v1 同目录族但独立文件族)。刷新须升 v3、全体平台",
        "#       同时重签、禁单平台独签、旧版保留回归对比(ADR-012 裁决③)。",
        "#",
        "# 发号规则(四条分支,逐条对应 `EventStream.cs`):",
        "#   ① 组复位   Tick 或 Patient 变 ⇒ currentSeq = 0",
        "#   ② 同组递增 同 (Tick, Patient) ⇒ currentSeq++",
        "#   ③ 恢复批播种 restoredGroupMax[tick,patient] + 1 > currentSeq ⇒ 抬(**只抬不压**)",
        "#   ④ 显式 Seq 门 e.Seq < 0 ⇒ 取计数器;e.Seq >= 0 ⇒ 原样保留(**但计数器仍前进**)",
        "#   ⚠️ 执行序 ①/② → ③ → ④ **不可重排**(S07 钉顺序,S09 钉占位语义)",
        "#",
        "# 格式: <name> <seq0>,<seq1>,...(逗号分隔,按入流顺序;空流 = 空串)",
        "#",
        "# 场景边界覆盖:四条分支各 ≥1 · 同 tick 两条 Patient=None(S04,判别力场景)·",
        "#               播种只抬不压的反例(S06)· 播种在 ++ 之后(S07)· 显式号占位(S09)·",
        "#               哨兵 -1 vs 合法 0(S10)· 四条全走的复合(S11)· 空流(S12)。",
    ]

    for name, fn in SCENARIOS:
        seqs = fn()
        lines.append(f"{name} {','.join(str(s) for s in seqs)}")

    out_path = sys.argv[1] if len(sys.argv) > 1 else "golden-v2.txt"
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    n = len([l for l in lines if not l.startswith("#")])
    print(f"wrote {out_path} ({n} entries)")


if __name__ == "__main__":
    main()
