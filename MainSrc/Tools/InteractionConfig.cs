namespace Project.Launch.Tools
{
    /// <summary>
    /// 交互配置——鼠标拖拽、手势等
    /// 后期"设置"页面可在这里读写
    /// </summary>
    public static class InteractionConfig
    {
        /// <summary>
        /// 3D 皮肤拖拽：水平方向反转
        /// false = 鼠标向右拖 → 人物向右转（当前默认）
        /// true  = 鼠标向右拖 → 人物向左转
        /// </summary>
        public static bool PivotInvertX { get; set; } = false;

        /// <summary>
        /// 3D 皮肤拖拽：垂直方向反转
        /// false = 鼠标向上拖 → 人物向上看（当前默认）
        /// true  = 鼠标向上拖 → 人物向下看
        /// </summary>
        public static bool PivotInvertY { get; set; } = false;

        /// <summary>
        /// 3D 皮肤拖拽灵敏度（0.5 = 鼠标 1 像素转 0.5°）
        /// </summary>
        public static float PivotSensitivity { get; set; } = 0.5f;
    }
}