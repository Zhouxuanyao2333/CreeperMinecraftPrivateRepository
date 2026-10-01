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
        // 登录滑块动画
        // ============================================================

        /// <summary>
        /// 滑块移动总时长（毫秒）
        /// 后期做"动画倍速"功能时，通过缩放这个值来实现
        /// </summary>
        public static TimeSpan SliderMoveDuration { get; set; } = TimeSpan.FromMilliseconds(700);

        /// <summary>
        /// 滑块移动过程中放大的峰值倍数（1.0 = 不放大）
        /// </summary>
        public static double SliderPeakScale { get; set; } = 1.2;

        /// <summary>
        /// 梯形曲线的"上升段结束"位置（0~1）
        /// 0 ~ 此值：缩放从 0 涨到峰值
        /// </summary>
        public static double SliderScaleRiseEnd { get; set; } = 0.25;

        /// <summary>
        /// 梯形曲线的"平台段结束"位置（0~1）
        /// 此值 ~ 1：缩放从峰值回落到 0
        /// 两者之间：保持峰值
        /// </summary>
        public static double SliderScaleFallStart { get; set; } = 0.75;

        /// <summary>
        /// 滑块移动过程中的最大模糊值（0 = 不模糊，0.6 = 中等模糊）
        /// </summary>
        public static double SliderBlurPeak { get; set; } = 1.4;

        /// <summary>
        /// 滑块宽度（用于缩放中心补偿，必须和 XAML 里的 Width 一致）
        /// </summary>
        public static double SliderWidth { get; set; } = 114;

        /// <summary>
        /// 滑块高度（用于缩放中心补偿，必须和 XAML 里的 Height 一致）
        /// </summary>
        public static double SliderHeight { get; set; } = 36;

        // ============================================================
        // 输入面板淡出淡入
        // ============================================================

        /// <summary>
        /// 输入面板淡出时长（毫秒）
        /// </summary>
        public static TimeSpan InputPanelFadeDuration { get; set; } = TimeSpan.FromMilliseconds(250);
    }
}