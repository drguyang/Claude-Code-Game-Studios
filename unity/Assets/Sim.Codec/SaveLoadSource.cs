// 读档**用了哪一份字节** —— `TD-persist-22` 日志半边的载体(2026-11-04)。
//
// 病因:`LoadBytes` 回退 `.bak` 时**零日志**(`noEngineReferences: true` ⇒ 本程序集
// 没有日志面),玩家读到的是**上一版**而屏上、控制台都没有任何痕迹。
// 处置(与 `LastSaveQueryError` 同款先例):本类只**陈述事实**,记录由装配层做 ——
// `LoadSequencer.Load` 读完档后读这两个属性,`UnityEngine.Debug.Log` 一条。
//
// ⚠️ 这两个枚举是**存储层的判定结果**,不是 UI 语义:它们回答「这份字节从哪个文件来」,
//    不回答「该不该告诉玩家」。后者归 42(见 `TD-persist-22` 的册子半边,仍 open)。

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>一次读档最终用了哪一份字节。</summary>
    public enum SaveLoadSource
    {
        /// <summary>未读 / 读档失败(<c>None</c> 是 <c>default</c>,与「读过」在类型上可分)。</summary>
        None = 0,

        /// <summary>正式档(`save_N`)—— 头部与校验和都过。</summary>
        Final = 1,

        /// <summary>回退档(`save_N.bak`)—— **读到的是上一版**。</summary>
        Bak = 2,
    }

    /// <summary>为什么回退到 `.bak`(<see cref="SaveLoadSource.Bak"/> 时才非 <c>None</c>)。</summary>
    public enum SaveLoadFallback
    {
        /// <summary>未回退。</summary>
        None = 0,

        /// <summary>正式档**在**,但校验和不过(正文 / 尾部被写坏)。</summary>
        FinalCorrupt = 1,

        /// <summary>正式档**缺失**,而该槽的 `.bak` 算占用(保留槽 1 / 2 的形态)。</summary>
        FinalMissing = 2,
    }
}
