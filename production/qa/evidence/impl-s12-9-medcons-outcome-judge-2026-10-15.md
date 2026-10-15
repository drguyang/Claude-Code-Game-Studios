# S12-9 实现证据 —— story-002(F-53.1 确定性结算 · Outcome 枚举)· 2026-10-15

> **证据性质**:实现轮原件(`.claude/docs/coding-standards.md` §Review Evidence Standards ——
> 原判定 → 修复落点 → 验证命令,可证伪)。
> **对象**:`production/epics/medical-consequences/story-002-deterministic-outcome-judge.md`
> **裁定前置**:键域 3×3×2(18 组合 + default)· 表带 U 档列默认 1 档(用户 AskUserQuestion 2026-10-15)
> **S12-9-AC-b 证据**:夹具先于实现落笔,初跑 **24 条 CS0246**(全指向待实现符号)——
> `unity/Logs/s12-9-red.log`(22:06,`grep -c "error CS"` = 24)。

## 交付面

| 类别 | 文件 | 内容 |
|---|---|---|
| golden 真源 | `tests/golden/medcons/golden_v1_reference.py` + `golden-v1.txt` | 独立 python 参考实现(**与 C# 零共享代码**);A01–A05 case-salt 黄金向量 + 20 例 tuple 判定 + N01 signed-shift 负夹具;**A05 = `9104567A4D5A819C` 与既有 C# case-salt golden 逐位一致(交叉验证)** |
| 枚举闭集 | `unity/Assets/Sim/Consequences/Outcome53.cs` | `Outcome` 恰 6 员(双轴 2+3 + NoEcho 显式成员);键域三轴(JudgmentLane 3 / CareLane 3 / RecoveryLane 2);`JudgeInputs`(恰 6 字段,零 verdict/零 disease_id/零生死位);`Judge.Evaluate`(纯函数按值传,出口恒枚举);`OutcomeJudgeDerive.URaw`(HashTagged 高 16 位)+ 退化常量 32768 |
| 试药史 | `unity/Assets/Sim/Consequences/TrialHistoryPredicate.cs` | OQ-53-9 形状:有序扫描 + run 计数 + 同病人 Judgment 打断;`TrialRunThreshold = 3` 留槽公开常量 |
| 判定表 | `assets/data/53_outcome_table.json` | 18 组合 + default;符号值;**逐格与 golden 符号规则对齐**(python 校验脚本实测「不一致行:无」) |
| 烘焙 | `OutcomeTableBinder/Baker(OutcomeTable53)/CookedWriter` + 菜单项 | 四条构建期校验(①覆盖 ②闭集 ③零浮点(Float token 结构性拒)④极性列出现即硬失败);行键升序落盘确定性 |
| 门 | `Editor.Tools.Gates/MedConsFloatGate.cs` + `AssemblyGates.RunAll` 挂接 | AC-53-03(注释剥离后浮点 token 扫描)+ AC-53-12(31 类 token 扫描),扫 `Assets/Sim/Consequences/*.cs` |
| 测试 | `unity/Assets/Tests/EditMode/MedConsequences/medcons_outcome_judge_test.cs` | 23 条:AC-53-01 闭集反射 ×2 · AC-53-08 类型面 · 表 18 行符号一致 · default 行 · AC-53-15 黄金向量 + 负夹具 + uraw + 退化 · AC-53-02 20 例逐位 + 纯函数性 · 双轴零生死 · OQ-53-9 谓词 ×3 · 端到端 ×2 · 烘焙四校验 ×5 · AC-53-12 零 31 符号 |

## 迭代记录(全红如实)

| 轮 | 结果 | 根因 | 修复 |
|---|---|---|---|
| red | 24 × CS0246 | 符号未实现(AC-b 纪律的预期红) | 落实现 |
| green1-2 | CS0050/51/52 | internal `OutcomeTableBinder` 包 public `BindResult` | 外层类转 public |
| green3 | CS0103×6 · CookedFormat×18 · Missing×12 · CreateTable×6 · CS0266×12 · CS1503×18 | `Bind` 改名漏改调用点 · 缺 using · `JsonNodeKind.Missing` 不存在(null 判)· internal 跨程序集 · `JsonNode.Int` 是 long · tuple int→enum | 逐类修 |
| green4 | 测试 18 错 | `OutcomeTable53` 缺 using · `URaw` long/ulong 字面量 · CS0826 | using + 显型数组 + 4 处字面量;**同轮发现 JSON 2 行与符号规则不一致 → 逐格对齐(python 校验)** |
| green5 | 5 红(测试侧口径) | 枚举 `ToString()`=PascalCase vs golden snake_case;嵌套 `BakeValidationException` 被顶层遮蔽 | `GoldenNameOf` 映射;删嵌套类用统一顶层异常 |
| green6 | **1 红** | key 拼错负例在行解析层报「非法」(覆盖校验是 errors 清空后第二道)——行为正确,断言过窄 | 断言收宽(非法/覆盖/缺失任一) |
| **final** | **3444 / 3398 / 0 / 46 —— 全绿,零回归** | | |

## 回归终值

**`3444 / 3398 / 0 / 46`**(`unity/Logs/s12-9-final.xml`,2026-10-15,EXIT=0)——
基线 `3421 / 3375 / 0 / 46`(commit a5ef7c5)⇒ **+23 新测试 / +23 passed / 0 failed / 46 skipped 不变,零回归**。
本 story 23 条全绿(逐条核对于终轮 XML,`MedconsOutcomeJudgeTest` 全 Passed)。
**验证命令**:
`grep -o 'total="3444" passed="3398" failed="0" skipped="46"' unity/Logs/s12-9-final.xml`

## 门保持

门 A / b2(Sim 引用集不变 —— Outcome53 零新依赖)· b6(零 `new PayloadRef(`)·
AC-53-05(五路闭集)· b3(无新 asmdef)。

## NOT-RUN 如实登记(S12-9-AC-c · 禁借绿)

- **AC-53-02 / AC-53-15 的 IL2CPP 三格**(Linux-ARM64-IL2CPP / Windows-x64-IL2CPP / Apple Silicon):
  **NOT-RUN** —— 本机仅 Mono;挂 ADR-012 矩阵,CI 执行。
- **golden 刷新协议**:改动 SplitMix64 / Judge 形状 ⇒ 全体平台同时重签 + 旧版保留回归对比(禁单平台独签)。

## 登记项(本批不做)

- 运行期 `IDataProvider` 装载接线(判定表进 `data-core` 组)—— 归装配轮
- 行值 / 阈值 / U 档数数值 —— 归数值轮(卡面 Note 6)
- C 轴命中位的生产真源(9 注册表 `treatable_by` 装载面,`ProgressionEvaluator` 恒 null)—— 归 9 轮
- Story 003(DelayTable + `ConsequenceResolved` 写流)/ 004(呈现)—— 后续 story
