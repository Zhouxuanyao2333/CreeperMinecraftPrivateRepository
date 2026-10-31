namespace Project.Launch.Tools.AppConstants
{
    /// <summary>
    /// ★ 启动器全局常量 —— 版本号 / 应用名的**唯一来源**。
    ///
    /// 想改版本号或应用标题，**只改这个文件**，其余位置全部自动跟随：
    ///   · <see cref="Version"/>            → 登录窗左下角显示的版本号
    ///                                      + 传给 Minecraft 的 ${launcher_version}
    ///   · <see cref="AppName"/>             → 主窗标题（任务栏） + 自绘标题栏文字
    ///   · <see cref="DisplayName"/>         → 需要好看排版的展示名（带空格）
    ///   · <see cref="LoginHeader"/>         → 登录窗顶部大字（"登录 " + DisplayName）
    ///   · <see cref="LauncherName"/>        → 传给 Minecraft 的 ${launcher_name}（游戏日志里可见）
    ///
    /// 注意：这些是编译期常量（const），改完必须重新编译才生效。
    /// </summary>
    public static class SetConstants
    {
        // ============================================================
        //  ★★★ 只需修改下面这几行 ★★★
        // ============================================================

        /// <summary>版本号 —— 登录窗左下角显示；同时作为 Minecraft 的 ${launcher_version}</summary>
        public const string Version = "rc-8";
        //更改下面这一行是应用名字，标题以及任务栏将显示
        public const string AppName = "Kilim Minecraft Launcher Demo前瞻";

        // ============================================================
        //  以下由上面派生，通常不需要改
        // ============================================================

        /// <summary>展示名（含空格，排版更好看）</summary>
        public const string DisplayName = "KML";

        /// <summary>登录窗顶部大字</summary>
        public const string LoginHeader = "登录 " + DisplayName;

        /// <summary>传给 Minecraft 的 ${launcher_name}（显示在游戏日志/崩溃报告中）</summary>
        public const string LauncherName = "Kilim";
    }
}
