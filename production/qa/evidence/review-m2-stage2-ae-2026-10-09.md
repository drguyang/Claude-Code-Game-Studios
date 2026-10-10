# 评审原件 — M2 接线轮阶段 2 · 批次 A+E(定点 Exp + 急救归因修)· 2026-10-09

> **对象**:批次 A = 手写定点 `Fix.Exp`(`Sim.Contracts/Fix.cs` +106 行;用户裁定②:整数域,
> 照 ADR-026 FixPow/FixSqrt 先例,禁 float/libm)· 批次 E = 急救事件病人归因修
> (`HostEmergencyProcessor.Process` 签名 +`PatientId` + 入口 fail-loud + 两处事件头归因;裁定④)
> **流程**:双批并行实现 → 主会话抽查 → 合批双代理恰一轮评审 → 5 项修复(注记类)→
> 合批终态全量复跑绿 → 2 发实变异恰中 → 本原件
> **前置裁定**:② 手写定点 Exp · ④ 阶段 2 内修归因(sprint-04 #5 行,2026-10-09 用户)

---

## 一、原判定(合批双代理评审 · 恰一轮 · 2026-10-09)

### 代码面(`lead-programmer`)**APPROVE**(0 BLOCKING + 3 低)

独立 Python 参考(与 C# 零共享)复算:34/34 黄金向量全中 · 全域 2,907,261 点穷举 ε 断言
**0 违例** · 小值区最大绝对 0.5946 LSB(与自标一致)。核过零问题 15 项,关键:
ln2 Q32 常量/规约/级数递推/出口双支/域常量推导全吻合 · 全程仅 `MulRaw` 一条宽乘(Amendment G)
· 门 A 未破 · E 签名无默认值防静默错位、门在 Judge/Append 前零副作用、归因分界清晰(被施救者
vs 施予者)· 越界检查:除两批 4+2 文件外零改动、registry 零触碰。

| # | 级 | 位置 | 缺陷 | 处置 |
|---|---|---|---|---|
| 码-1 | 低 | Fix.cs / fix_exp_test.cs 头注 | 「最大相对 3.12×2⁻¹⁶」口径过强:仅值≥1.0 子域成立;全域相对最大 100% 在量化下限(-772243,v=1/o=0.5),属 Q16.16 固有量化;ε 界本身全域 0 违例 | **本轮修**(三段口径措辞,界不变) |
| 码-2 | 低 | Fix.cs:274 | ln2「相对偏差 4.2e-11」实为绝对(相对 6.06e-11);误差项③量级结论不变 | **本轮修**(一词) |
| 码-3 | 低 | EventStream.cs:63 | 注释「急救两支一律带 PatientId.None」批次 E 后已为假;连带:急救两支现进 AC-15 在场/CAP 门(施救中病人离场+满 CAP ⇒ Append 抛 IOE,fail-loud 非静默,但未穿真 EventStream 验证) | **本轮修**(删例+行为注);「施救时病人离场」边界语义 → 批次 C 确认 |

### 测试面(`qa-lead`)**FIX-THEN-APPROVE**(2 S2 + 2 S3)

| # | 级 | 缺陷 | 逃逸变异 | 处置 |
|---|---|---|---|---|
| 测-1 | S2 | 黄金夹具「独立 Python 参考」**证伪**:34/34 与同算法复刻逐位全合,12/34 与真值 e^x 有差(最大域顶 ≈0.4 ulp ∈ ε)⇒ 夹具防实现漂移,**不防算法级共模**(共模防线实为分层 Math.Exp 神谕段) | 算法共模偏置下夹具零防线 | **本轮修**(头注如实降格+防线归属) |
| 测-2 | S3 | 确定性复算测判别力≈0(纯函数连调两次恒等,任何实现都过) | 任意实现错误 | **登记**(不列为「确定性已验证」证据) |
| 测-3 | S3 | 盲区核实为真:SeriesTerms 10→9 截断 ≪1 LSB 不可观测(补 4 项变异须先重标 ε 否则同样不可见) | 良性等价变异 | 登记(可接受) |
| 测-4 | S2 | 复跑证据链:3125(A 时点,含 E)与 3077(E 时点,不含 A)非同树 ⇒ 无合批终态证据 | 跨批回归互相掩护 | **本轮修**(合批终态全量复跑,§三) |

判别力结论:E 批六类变异全部可判别无逃逸;假绿扫描干净(零 Ignore/吞断言/恒真;神谕仅测试侧)。
主会话订正一处误读:「E 急保守集 4 个失败未 triage」实为 **failed 0 · skipped 4**(4 既有
NOT-RUN:FeelLatency×3 + JudgeTest×1,E 交付时已 triage)—— 测-4 的实质诉求仅剩合批终态全量。

---

## 二、修复落点(5 项 · 主会话执行 · 锚点)

F1 测-1:黄金夹具头注降格「同算法 Python 复刻」+ 共模防线=神谕段 + 域顶 0.4 ulp 属声明取舍
(`fix_exp_test.cs:14-20`)· F2 码-1:误差三段口径(值≥1.0 → 3.12 / 小值区 → 0.595 绝对 /
中间带由 3 LSB 承担;**ε 界不变**;`Fix.cs` + 测试头注同源)· F3 码-2:ln2 绝对 4.2e-11 /
相对 6.06e-11(`Fix.cs:274`)· F5 码-3:EventStream 注释删「急救两支」假例 + 批次 E 后行为注
(`EventStream.cs:63-67`)· F6:GDD F0 中间精度行窄注记(「不每步回降」在 Exp 上按
「唯一宽乘路径、逐项落回」实现,归 AC-5b Gate;`disease-simulation.md:770`)。

### 二之二、登记不修(4 项)

测-2(确定性复算测判别力≈0,不列为证据)· 测-3(10→9 等价变异盲区,ε 内不可见)·
码-3 连带(「施救时病人离场 + 满 CAP」边界语义未穿真 EventStream 验证 → 批次 C)·
Process 方法 60 行超 40 行(既有状态,建议后续提私有方法)。

### 二之三、已裁张力(不重开)

**GDD F0「Exp 中间量 128 位域不每步回降」vs 实现每步舍入** —— 主会话裁定采纳每步舍入
(① FixPow/FixSqrt 同构先例 · ② 用户裁定「照 ADR-026 先例」· ③ F0 自标「精度取舍归
Gate 待标」· ④ 实测误差在 F0 保守带内),以 F6 窄注记落档,不重开裁定。

---

## 三、验证命令与实数

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.Sim.FixExpTest" \
  --output unity/Logs/editmode_fixexp.xml
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.EmergencyProcedures" \
  --output unity/Logs/editmode_emergency_fix.xml
# 合批终态全量(修复后,两批改动都在树上)
unity test unity --mode EditMode --output unity/Logs/editmode_full_ae_combined_20261009.xml
# 变异:① k round→trunc(Fix.cs:349)② Append① 归因回 None(HostEmergencyProcessor)
```

| run | total | passed | failed | skipped | 说明 |
|---|---|---|---|---|---|
| FixExpTest(修复后复跑) | 48 | 48 | 0 | 0 | 亦为变异恢复后终验 |
| 急保守集(修复后复跑) | 123 | 119 | 0 | 4 | 4 跳全既有 NOT-RUN;亦为变异恢复终验 |
| PlayMode host_authority(E 批) | 4 | 4 | 0 | 0 | 含端到端归因断言 2 条 |
| **合批终态全量** | **3125** | **3078** | **0** | **46** | 修复后树;与 A 时点一致 ⇒ 注记修复零破坏 |
| 变异 MUT1(k round→trunc) | 48 | 42 | **6** | 0 | 恰红 6 条(规约破坏 ⇒ golden 红) |
| 变异 MUT2(Append①→None) | 123 | 118 | **1** | 0 | 恰红归因断言 |
| 变异恢复复跑 ×2 | 48+123 | 48+119 | **0** | — | python 反向恢复;Fix.cs/HostEmergencyProcessor 零 `MUT` 残留 |

⚠️ 全量慢性 exit 非零(既往同款)—— **XML 为判据**,failed=0 判绿。跑前独占:仅 unityhub,
无编辑器进程、无 UnityLockfile。

---

## 四、判定链

**双批并行实现(A ∥ E,文件面不相交)→ 主会话抽查(Fix.Exp 头注论证/域常量;E diff 全读)→
合批双代理恰一轮评审**(代码面 APPROVE 0B+3L,独立复算 34/34+全域 0 违例 · 测试面
FIX-THEN-APPROVE 2S2+2S3)→ **5 项修复全落**(注记类,ε 界不变)→ **合批终态全量复跑绿**
(3125/3078/0 红)→ **2 发变异恰中**(MUT1 红 6 · MUT2 红 1;恢复复跑绿,零残留)→
**APPROVE 收口**(登记 4 项;F0 张力已裁并落窄注记)。

**归属补登(2026-10-10,BCD 合批评审码-6 提问)**:`design/gdd/disease-simulation.md` F0
中间精度行的 1 行窄注记 = 本批 F6 修复(主会话亲手落),归属 A+E 批;本节文件清单补
`design/gdd/disease-simulation.md`(1 行)—— 此前清单漏列。

**未跑/未核声明**:「施救时病人离场 + 满 CAP」边界未穿真 EventStream(批次 C);AC-5b
「待测/尚待标定」行是否回填 4/3 为标定值归 Gate 轮(批次 C/F);黄金夹具期望值的算法级
共模防线依赖神谕段(已在测试头注明示);`PatientId` 值语义(.Value/Equals)测试面标未核实
(主会话经代码面核过 15 调用方编译期波及,行为面未专项)。
