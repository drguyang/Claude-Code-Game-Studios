# ADR-042 兑现轮 —— 存档 v6(落笔时刻)· 锁搬到写侧 · 存档册行列形态

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-06 |
| **用户指令** | ①「存档每行最前的序号没必要」②「每行最后增加一半大小字体的真实世界时间」③「已锁换成灰色背景进行区分」④「点击只显示读入,覆写显示灰色暗示不可覆盖存档」;澄清:「已锁不保留小字」·「时刻加在行末尾」·「把锁改到写侧」· 时间来源 = 「写进存档头」·「现有存档数据全部删除,方便一会测试」·「真包截图看实际效果再调整时刻字号大小和灰色的对比度」 |
| **三元组** | 截至 `2026-11-06T02:00Z` · 批次 `ADR-042 兑现轮` · commit `<待填>` + 工作树 |

---

## 1. 改了什么(按面)

| 面 | 落点 | 内容 |
|---|---|---|
| 存档格式 | `Sim.Codec/SaveHeader.cs` · `SaveHeaderCodec.cs` | v6:头部定长段末尾 +`SavedAtUtcTicks`(i64);`FixedSize 92 → 100`、`HeaderSize 124 → 132`、`CurrentSaveVersion 5 → 6`;`FixedSizeFor` / `HeaderSizeFor` 各加一档 |
| 迁移 | `Sim.Codec/SaveMigrationV5ToV6.cs`(新) | 在 92 处插 8 个 **0**(未知)、版本写 6、正文逐位不动 |
| 写侧 | `Gameplay.Boot/SaveComposer.cs` · `CompositionRoot.cs` | `Compose` 增可选形参 `savedAtUtcTicks`;生产恒传 `DateTime.UtcNow.Ticks`;链注册 v5→v6 |
| 读侧 | `Sim.Codec/SaveSlotProbe.cs` | `SaveSlotSummary.SavedAtUtcTicks`;读取上限抬到 `HeaderSizeV6` |
| 锁语义 | `Sim.Codec/SaveService.cs` · `SaveSlotLedger.cs` · `SlotProjections.cs` | 读门摘锁、写门加锁(门 ①′);两投影按新公式改写 |
| 42 呈现 | `SaveSlotView.cs` · `SaveSlotItem.cs` · `SaveSlotsScreen.cs` · `SkeuoPaper.uss` · `SkeuoThemeVariables.uss` | 去行首序号;行末半字号本地时刻;已锁整行灰底(无小字);已锁进二段态、重写灰置不可获焦 |
| 文档 | ADR-042(新)· ADR-040 §D4 窄修订 · ADR-041 §D3 改判 · `persistence-service.md` · `save-slot-ui.md` · `save-slots-7b.md` · `accessibility-requirements.md` · `technical-preferences.md` | —— |

## 2. 回归

| 套件 | 结果 | 等式 / 对照 |
|---|---|---|
| EditMode | **`6947 / 6910 / 0 / 0 / 37`**(`unity/Logs/ed-adr042e.xml` · 终轮:探针已删、`BootRoot` 复原后的实跑) | 基线 `6941 / 6904 / 0 / 0 / 37`(`ed-tdsk17.xml`)⇒ **+6 / +6**:`save_v6_saved_at_test` 4 条 + `save_slot_rows_legibility_test` 2 条(时刻往返 / 半字号与未知值),零回归 |
| PlayMode | **`342 / 338 / 0 / 2 / 2`**(`unity/Logs/pm-adr042e.xml`) | 与上一轮逐位相同(本轮零新增 PlayMode 测试,只改走查件的判据口径)⇒ 零回归 |

## 3. 真包截图(用户指令:看实际效果再调字号 / 对比度)

真包 = `unity/builds/Linux-x64/大医精诚.x86_64`(2026-11-06 01:37 构建 · `Result: Succeeded`),
1280×720 窗口,`XTEST` 驱动。截图存 `production/qa/evidence/adr042-2026-11-06/`。

| # | 帧 | 看到什么 |
|---|---|---|
| 01 | `01-book-row-with-time.png` | 册子摊开:行文字 `第壹日 昼`,**行末**小字 `2026-11-06 01:38:38`;行首**已无序号** |
| 02 | `02-two-stage-...png` | 二段态:`第壹日 昼 [启封][重写] 2026-11-06 01:38:38` —— 墨字插在文字与时刻之间,**时刻恒在行末** |
| 03 | `03-locked-row-grey-0.28.png` | 读档后该行**整行灰底**,行上**零字说明**;另一行(未读)仍是纸色 |
| 04 | `04-locked-vs-unlocked-3x.png` | 3× 放大对照:上(已锁,灰底)vs 下(未锁,纸色) |
| 05 | `05-time-6x.png` | 6× 放大:12px 时刻在 1× 下略小、放大后可读 |

### 3.1 灰底对比度:0.14 **不可辨** ⇒ 本轮抬到 0.28

第一版取 `rgba(43,36,22,0.14)`。真包实测(两行同一纸面,一条已锁一条未锁):

| 版本 | 已锁行平均色 | 未锁行平均色 | 差 |
|---|---|---|---|
| `0.14` | 未形成可辨差(实测两行同值) | — | ≈ 0 |
| **`0.28`(现行)** | **(206,196,175)** | **(227,219,200)** | **≈ 21/通道** |

⇒ 0.14 在纸纹上读不出灰(纸纹本身明度起伏就有这个量级),0.28 才「一眼看得出是另一页」。
**值已落在 `SkeuoThemeVariables.uss` 的 `--skeuo-paper-bg-locked`**,注释里记了这两组实测。

### 3.2 时刻字号:12px(= 纸面 24 的一半,用户裁定)

1× 下略小但可读(05 的 6× 放大是同一帧裁的);它**低于** `accessibility-requirements:85`
的 24px 下限 ⇒ 已在门里登记**唯一具名例外**(见 §4.1)。**是否回调等用户实拍裁定。**

## 4. 未做 / 边界

1. **字号 12px 低于 `accessibility-requirements:85` 的 24px 下限** —— 已在门里登记**唯一具名例外**
   (`skeuo_readability_test.FontFloorExemptSelectors`,带规模守卫 + 陈旧例外守卫)并写进无障碍件。
   用户指令「真包截图看实际效果再调整」⇒ 最终字号待实拍裁定。
2. **已锁行状态只走色彩通道**(用户明确要求「不保留小字」)⇒ 色觉障碍玩家读不出「已锁」;
   仍可分辨的是按下去的反应(重写灰置不可获焦)。补非色彩通道须先改该裁定。
3. **"读可反复"** 是裁定的显式后果,不是遗漏:反 scum 只挡「读→改写→再读」的写那一半。
