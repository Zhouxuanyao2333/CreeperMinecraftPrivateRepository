using System;

namespace Project.Launch.Tools
{
    /// <summary>
    /// 全局动画参数配置
    /// 后期"设置"页面只需修改这里的属性值即可统一调整动画手感
    /// </summary>
    public static class AnimationConfig
    {
        // ============================================================
        // LoginWindow 登录滑块动画
        // ============================================================

        /// <summary>登录滑块移动总时长</summary>
        public static TimeSpan SliderMoveDuration { get; set; } = TimeSpan.FromMilliseconds(700);

        /// <summary>登录滑块放大峰值倍数</summary>
        public static double SliderPeakScale { get; set; } = 1.2;

        /// <summary>梯形曲线上升段结束位置</summary>
        public static double SliderScaleRiseEnd { get; set; } = 0.25;

        /// <summary>梯形曲线下降段开始位置</summary>
        public static double SliderScaleFallStart { get; set; } = 0.75;

        /// <summary>登录滑块最大模糊值</summary>
        public static double SliderBlurPeak { get; set; } = 1.4;

        /// <summary>登录滑块宽度</summary>
        public static double SliderWidth { get; set; } = 114;

        /// <summary>登录滑块高度</summary>
        public static double SliderHeight { get; set; } = 36;

        // ============================================================
        // LoginWindow 输入面板淡出淡入
        // ============================================================

        /// <summary>输入面板淡出时长</summary>
        public static TimeSpan InputPanelFadeDuration { get; set; } = TimeSpan.FromMilliseconds(250);

        // ============================================================
        // MainWindow 底部导航滑块动画
        // 曲线形状与 LoginWindow 完全一致（EaseInOut + 梯形缩放 + 三角形模糊）
        // ============================================================

        /// <summary>导航滑块移动总时长</summary>
        public static TimeSpan TabMoveDuration { get; set; } = TimeSpan.FromMilliseconds(700);

        /// <summary>导航滑块放大峰值倍数</summary>
        public static double TabPeakScale { get; set; } = 1.30;

        /// <summary>导航滑块梯形曲线上升段结束位置</summary>
        public static double TabScaleRiseEnd { get; set; } = 0.25;

        /// <summary>导航滑块梯形曲线下降段开始位置</summary>
        public static double TabScaleFallStart { get; set; } = 0.75;

        /// <summary>导航滑块最大模糊值</summary>
        public static double TabBlurPeak { get; set; } = 0.8;

        /// <summary>导航滑块宽度（用于缩放中心补偿）</summary>
        public static double TabWidth { get; set; } = 96;

        /// <summary>导航滑块高度（用于缩放中心补偿）</summary>
        public static double TabHeight { get; set; } = 44;

        /// <summary>导航步长（每格宽度）</summary>
        public static double TabStep { get; set; } = 104;

        // ============================================================
        // MainWindow 页面切换淡入淡出
        // ============================================================

        /// <summary>页面切换淡出/淡入时长</summary>
        public static TimeSpan TabPanelFadeDuration { get; set; } = TimeSpan.FromMilliseconds(200);
    }
}