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

**用例 → AC 映射(17 [Test])**:

| AC | 用例 |
|---|---|
| A2 往返 | `..._saveLoad_roundTrip_perKeyIncludingComposite`(主 + 复合 part + 同动作两物理输入)· `..._emptyOverrideSet_roundTripsClean` · `..._load_firstLaunchNoFiles_returnsNoSidecar` · `..._neg_corruptPayload_...`(负例)· `..._hashMismatch_doesNotFeed...` · `..._halfSidecar_treatedAsMismatch`(后两条 = 实现期登记,读序/半截忠实扩展) |
| A5 落盘面 | `test_writeSurface_scan_gameplayInput_onlyWhitelistedWriteSites`(主)· `..._playerPrefsFixture_flagsRed`(负例)· `..._nonWhitelistedWriteTarget_flagsRed` · `..._synonymAlias_passesAndRenameBypass_flagsRed`(同义词表 Edge)· `..._save_writeFailure_logsOnlyKeepsWritePoints`(写失败 Edge) |
| E3⑥ 字节 | `test_sidecar_payloadFileBytes_equalSaveBytesVerbatim`(主)· `..._load_feedsSavedBytes_verbatimRoundTrip` · `..._source_hasNoPayloadParsingCalls`(扫描辅助)· `..._nonAsciiOverridePath_byteRoundTrip` · `..._doubleSave_idempotentBytes` · `..._neg_beautifiedRewrite_differsFromSavedBytes`(负例) |

**WriteSurface 扫描器**(AC-3-A5 的执行体)住测试文件内(`ScanWriteSurface` 一族):
注释剥离(状态机,字符串/字符字面量保留)→ 拒绝清单符号逐词命中 → 写调用
(`File.Write*/Append*/Create/Open(write)/Move/Delete/Copy` + `new StreamWriter` +
`File.Open(...FileMode.Write 系)`)首参经**同义词表定点闭包**(白名单字面量的常量 →
表达式/引用链)解析到白名单文件,解析不到即违例 —— 换名/拼接绕过同红。
夹具源码以字符串内联(负例不落文件 —— 落盘的负例夹具会被对
`unity/Assets/Gameplay.Input/` 的真扫描漏掉,内联才保证负例跑在同一个谓词上)。
