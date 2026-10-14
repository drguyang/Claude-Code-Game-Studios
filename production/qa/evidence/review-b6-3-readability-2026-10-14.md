# 评审报告原件 —— B-6-3 值区文字可读性(附 B-6-4 一并查结)

- **对象**:B-6-3(P2 · S3 主目标未达;`playtest-2026-10-13-m2-round6.md` §6)
  · 附 B-6-4(P3,「随 B-6-3 一并查」条款的执行记录)
- **评审日期**:2026-10-14
- **评审角色**:qa-lead 收口核验(承 `director-gates.md` 检查式)
- **结论**:**PASS** —— 自动化腿 7/7 + 人工腿复验「值区清晰可辨」**双半达成**,可转 Complete

## 一、原判定(逐字承出处)

> | B-6-3 | **P2 · S3 主目标未达** | 文字可读性:① 底衬与纸纹明度差过小(Δ≈0.003)⇒ 底衬形同虚设,直接压纹;② 24px 字发虚。**建议**:复测 `--skeuo-reading-backing` 值与纸纹中心的关系,重定 token(数值归用户);或给底衬加极淡边线/加深一档 | S3 后续轮(用户裁数值) |

> | B-6-4 | P3 | 「看不到字体」若是**字体资产渲染问题**(非对比),单列:TextCore 动态 SDF 在 24px 的 atlas 精度 | 随 B-6-3 一并查 |

## 二、修复落点(两条子项 × 各自根因)

### 子项① 底衬 Δ≈0.003 形同虚设 —— **换机制**(2026-10-13 二轮修定,用户实测驱动)

「靠明度差定界」被实测证死:底衬越浅阳墨越锐但与纸纹越不可辨(1.038:1),加深则阴性墨
跌破 7:1 —— 数学死结,**不是调 hex 能解**。修法两半:

- **底衬回纸色系内加深一档**:`#fbf9f3` → **`#e7e0cd`**
  (`SkeuoThemeVariables.uss:77`)。实测(线性 Rec.709):Δ = **0.0947** vs 纸面
  `--skeuo-paper-bg`(守 `test_s3_4` Δ≥0.02)、**0.238** vs 纸纹中心 #fefdfe(可辨)。
  落选值 #f2ead8(Δ 仅 0.0150)被 `test_s3_4` 断言结构性排除。
- **定界从明度差移到边线**(承 story-021「字段由线格划出,不画成按钮」同源美学):
  新 token `--skeuo-reading-rule: rgba(43,36,22,0.22)`(`SkeuoThemeVariables.uss:86`),
  `.channel-reading` / `.channel-reading-negative` **四向** border 绑定
  (`SkeuoPaper.uss:136-139/153-156`)。合成实测 1.378:1 / 1.534:1 —— 淡到不抢字、
  深到看得见格。线宽不另立 token,走既有 `--skeuo-shared-border-width`(1px)。

### 子项② 24px 字发虚 —— **三层根治**(2026-10-13,用户实验确证)

- **字号**:24 → **28px**(`--skeuo-font-size-lexeme`,`SkeuoThemeVariables.uss:187`)。
  28px 字盒 40.5 < min-height 44 ⇒ 行高恒 78.0px ⇒ 「空行/已查行等重」不动自守。
- **SDF 根因**(B-6-4 条款的查证结果):采样点 32px vs 主题默认字号 ≈14px ⇒ 缩采样不足
  = 糊。用户实验(抬 40px 即清晰)确证根因是**字号过小**,非渲染管线。四笔提交根治:
  `7773886`(SDF 预烤字形 + 切 Static)→ `0c3271b`(USS 字体引用 guid → resource())
  → `2d97bbc`(根级默认字体)→ `dc4f963`(字号,第二根因)。
- **分辨率前置**:768p 视口下 28px 仅等效 21px ⇒ 同批裁「默认分辨率 → 1920×1080」
  (`ProjectSettings.asset:45-46` 已落,本轮核验在位)。

## 三、验证命令(可证伪 · 全部实测)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# 自动化腿:S3 可读性六测 + 锚测
timeout 900 /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$(pwd)/unity" -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.SkeuomorphicUI.skeuo_readability_test" \
  -testResults "$(pwd)/unity/Logs/b63_readability2.xml" \
  -logFile "$(pwd)/unity/Logs/b63_readability2.log"
# ⛔ 不加 -quit;判据 = 结果 XML 存在 + 数字,不靠 exit=$?
```

### 实测结果(2026-10-14)

| 门 | 结果 |
|---|---|
| `skeuo_readability_test`(s3_1–s3_5 + 锚测) | **7/7 passed** |
| `test_s3_1` | 双墨 ≥7:1(negative 8.27:1 / fg 13.84:1)+ 公式锚定 |
| `test_s3_4` | 底衬 Δ = 0.0947 ≥ 0.02(含自比对判别力守卫) |
| `test_s3_5` | 边线四向绑定齐(缺向 = 退回已失败路径,恰红) |

日志:`unity/Logs/b63_readability2.xml`。

### 人工腿(收口判据的另一半)

- **日期**:2026-10-14(用户真机复验)
- **结果**:**「值区清晰可辨」** —— 字不糊不灰、底衬格从纸面可辨(边线围着)、直接判结
  原判据的两条目视红(①压纹 ②发虚)。
- ⚠️ 本项人工腿**禁借绿**:原判据即人工目视判红(第六轮 playtest C1/C5),收口亦须人工判绿。

## 四、B-6-4 一并查结(其「随 B-6-3 一并查」条款的执行记录)

B-6-4 是**条件性**缺陷(「**若是**字体资产渲染问题(非对比)」)。查证结论:

1. **是**字体资产渲染问题 —— 但根因不是「atlas 精度上限」,而是 **SDF 采样点(32px)
   与实际字号(≈14px)失配** ⇒ 缩采样不足;用户实验(40px 清晰)实证。
2. 根治四提交:`7773886` / `0c3271b` / `2d97bbc` / `dc4f963`(预烤字形 + Static +
   resource() 引用 + 根级默认字体 + 28px 档)。
3. **人工实证**:用户复验「值区清晰可辨」—— 「看不到字体」已不存在。

⇒ **B-6-4 判结**(其疑虑被根治 + 实证覆盖,无需另立 story)。

## 五、遗留

- **B-6-2**(焦点可见性 P2)仍 open,独立 story —— sprint-08 整体仍不可转 Complete。
- 本项修复未触碰 B-6-2 的任何判据面。
