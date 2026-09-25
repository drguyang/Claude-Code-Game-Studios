# integration/input_system/

按系统分目录的集成测试(命名 `[system]_[feature]_test.cs`)。集成类故事的 BLOCKING
证据落点(coding-standards §Testing Evidence by Story Type)。

## Story 003(绑重 overrides sidecar 持久化 —— AC-3-A2 / A5 / E3⑥)—— 落点说明

故事头登记的证据路径(账本路径)为
`tests/integration/input_system/overrides_sidecar_test.cs`,但该路径在仓库根,
**Unity 不编译 `unity/Assets/` 之外的代码**(承 item-database Story 001–010 同一先例)。
真身 =

**`unity/Assets/Tests/EditMode/InputSystem/overrides_sidecar_test.cs`**

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/InputSystem/overrides_sidecar_test.cs` |
| 装配 | `unity/Assets/Tests/EditMode/EditMode.asmdef`(`Sim.Contracts.Tests`,已含 `Gameplay.Input` 引用) |
| 被测生产件 | `unity/Assets/Gameplay.Input/BindingsStore.cs` |

**为何落 EditMode 而非 PlayMode**:三条 AC 判的全是**文件面与静态面**(字节往返 /
写盘点扫描 / 读序出口),无输入设备交互、无帧循环 —— EditMode 足判,且与同 epic
Story 001/002 的测试同装配,套件一次跑完。

**用例 → AC 映射(24 [Test];首版 17 + code review 批追加 7,登记见故事文件
实现期登记块)**:

| AC | 用例 |
|---|---|
| A2 往返(11) | `..._saveLoad_roundTrip_perKeyIncludingComposite`(主 + 复合 part + 同动作两物理输入)· `..._emptyOverrideSet_roundTripsClean` · `..._load_firstLaunchNoFiles_returnsNoSidecar` · `..._neg_corruptPayload_...`(负例)· `..._hashMismatch_doesNotFeed...` · `..._halfSidecar_treatedAsMismatch` · `..._load_disablesAsset_ruleFiveStep1`(规则五 step1 实证)· `..._headerMissingSchemaHash_mismatchNotSilent` · `..._headerOnly_treatedAsMismatch` · `..._headerContent_writtenFields` · `..._removedOverride_roundTripsDeleted`(删半边) |
| A5 落盘面(7) | `test_writeSurface_scan_gameplayInput_onlyWhitelistedWriteSites`(主)· `..._playerPrefsFixture_flagsRed`(负例)· `..._nonWhitelistedWriteTarget_flagsRed` · `..._synonymAlias_passesAndRenameBypass_flagsRed`(同义词表 Edge)· `..._save_writeFailure_logsOnlyKeepsWritePoints`(写失败 Edge)· `..._headerWriteFailure_payloadAlone_mismatchNextLoad`(反向半边)· `test_writeSurface_scan_writeApiVariants_flagsRed`(五种写法盲区负例) |
| E3⑥ 字节(6) | `test_sidecar_payloadFileBytes_equalSaveBytesVerbatim`(主)· `..._load_feedsSavedBytes_verbatimRoundTrip` · `..._source_hasNoPayloadParsingCalls`(全程序集扫描辅助 + payload 手术谓词)· `..._nonAsciiOverridePath_byteRoundTrip` · `..._doubleSave_idempotentBytes` · `..._neg_beautifiedRewrite_differsFromSavedBytes`(牙齿证明,适用面见故事登记 Q7) |

**WriteSurface 扫描器**(AC-3-A5 的执行体)住测试文件内(`ScanWriteSurface` 一族):
注释剥离(状态机,逐字串 `@"…"` 按 `""` 转义;字符串/字符字面量保留)→ 拒绝清单符号
在**去字面量**文本上逐词命中(日志串提 `PlayerPrefs` 一词不误报)→ 写调用
(`.WriteAll*/Append*/CreateText/OpenWrite` 带可选 `Async` 后缀 · `.Create/Open/Move/Delete/Copy/Replace`
· `new StreamWriter/FileStream`,**不锚定 `File.` 前缀** —— `using IOFile = System.IO.File;`
换名别名同红)实参经**同义词表定点闭包**(白名单字面量的常量 → 表达式/引用链)解析到白名单
文件,解析不到即违例 —— 换名/拼接绕过同红;**Move/Copy 第二实参一并解析**
(`File.Move(白名单, evil)` 型绕过拦下)。
夹具源码以字符串内联(负例不落文件 —— 落盘的负例夹具会被对
`unity/Assets/Gameplay.Input/` 的真扫描漏掉,内联才保证负例跑在同一个谓词上)。

**已知 fail-closed 约束(review Q4 登记,方向均为红非漏报绿)**:
① 同义词表按**单文件**建 —— 他文件 `const` 别名解析不到 ⇒ 误报红(放宽须扩到程序集级
常量扫描);② 插值字符串 `$"{dir}/bindings.overrides.json"` 里的字面量不被识别 ⇒ 误报红;
③ 通用同名调用(`Array.Copy` / `string.Replace`)首参解析不到白名单 ⇒ 误报红。
三者误报方向安全;真源(本程序集)当前零误报 —— 主扫描 `2 写点 · 0 违例`。

**跨故事协调点(review F3)**:`writeTargets.Count == 2` 维持 AC「仅…两处」原文 ——
Story 005 在本程序集增写点(改名备份 `File.Move`/`File.Delete`)时须同批扩展白名单与
该断言,红 = 正确信号。
