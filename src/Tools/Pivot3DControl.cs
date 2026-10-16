using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Project.Launch.Tools;
using SkiaSharp;

namespace Project.Launch.Views
{
    public class Pivot3DControl : Control
    {
        public static readonly StyledProperty<Bitmap?> SkinProperty =
            AvaloniaProperty.Register<Pivot3DControl, Bitmap?>(nameof(Skin));

        public Bitmap? Skin
        {
            get => GetValue(SkinProperty);
            set => SetValue(SkinProperty, value);
        }

        private List<SkinFace> _faces;
        private SkinModelType _currentModelType = SkinModelType.Classic;
        private SKImage? _skinImage;
        private float _yaw = 0f;
        private float _pitch = 0f;

        private bool _isDragging;
        private Point _dragStart;
        private float _dragStartYaw;
        private float _dragStartPitch;
        private DateTime _lastInteraction = DateTime.MinValue;

        private DispatcherTimer? _autoRotateTimer;

        public Pivot3DControl()
        {
            _faces = MinecraftSkinModel.Build(SkinModelType.Classic);
            Focusable = true;
            ClipToBounds = true;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _lastInteraction = DateTime.Now;
            _autoRotateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _autoRotateTimer.Tick += OnAutoRotateTick;
            _autoRotateTimer.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (_autoRotateTimer != null)
            {
                _autoRotateTimer.Stop();
                _autoRotateTimer.Tick -= OnAutoRotateTick;
                _autoRotateTimer = null;
            }
            _skinImage?.Dispose();
            _skinImage = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SkinProperty)
            {
                _skinImage?.Dispose();
                _skinImage = BitmapToSkImage(Skin);

                if (Skin != null)
                {
                    var detected = DetectSkinType(Skin);
                    if (detected != _currentModelType)
                    {
                        _currentModelType = detected;
                        _faces = MinecraftSkinModel.Build(detected);

                        LogHelper.Write(LauncherPaths.AppLog,
                            $"皮肤检测: {detected}",
                            $"Skin detected: {detected}");
                    }
                }

                InvalidateVisual();
            }
        }

        // ============================================================
        // 检测皮肤类型（Classic / Slim）
        //
        // 方案：
        //   1. 看皮肤宽度决定是普通（64）还是 HD（128）
        //   2. 按比例缩放检测坐标
        //   3. 检测该列是否有颜色：
        //        - 有颜色 → Steve (Classic)
        //        - 全透明 → Alex (Slim)
        //
        // 基准：0-based (54, 20)，长度 12 行（对应 64×64）
        // ============================================================
        private static SkinModelType DetectSkinType(Bitmap bitmap)
        {
            try
            {
                int w = bitmap.PixelSize.Width;
                int h = bitmap.PixelSize.Height;

                // 老版 64×32 皮肤一律按 Classic
                if (w < 64 || h < 32) return SkinModelType.Classic;

                // 计算比例：64 → 1，128 → 2，256 → 4
                int ratio = w / 64;

                // 缩放检测坐标和长度
                int checkX = 54 * ratio;
                int checkY = 20 * ratio;
                int checkH = 12 * ratio;

                // 边界保护
                if (checkX >= w || checkY + checkH > h)
                    return SkinModelType.Classic;

                var rect = new PixelRect(checkX, checkY, 1, checkH);
                byte[] pixels = new byte[checkH * 4];
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        bitmap.CopyPixels(rect, (IntPtr)p, pixels.Length, 4);
                    }
                }

                // 有任一不透明像素 → Classic
                for (int i = 0; i < checkH; i++)
                {
                    if (pixels[i * 4 + 3] != 0)
                    {
                        LogHelper.Write(LauncherPaths.AppLog,
                            $"检测: 尺寸={w}x{h}, ratio={ratio}, 位置=({checkX},{checkY}), 结果=Classic",
                            $"Detect: size={w}x{h}, ratio={ratio}, pos=({checkX},{checkY}), result=Classic");
                        return SkinModelType.Classic;
                    }
                }

                LogHelper.Write(LauncherPaths.AppLog,
                    $"检测: 尺寸={w}x{h}, ratio={ratio}, 位置=({checkX},{checkY}), 结果=Slim",
                    $"Detect: size={w}x{h}, ratio={ratio}, pos=({checkX},{checkY}), result=Slim");
                return SkinModelType.Slim;
            }
            catch (Exception ex)
            {
                LogHelper.Write(LauncherPaths.AppLog,
                    $"检测异常: {ex.Message}",
                    $"Detect exception: {ex.Message}");
                return SkinModelType.Classic;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            _dragStartYaw = _yaw;
            _dragStartPitch = _pitch;
            _lastInteraction = DateTime.Now;
            e.Pointer.Capture(this);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var cur = e.GetPosition(this);
            float dx = (float)(cur.X - _dragStart.X);
            float dy = (float)(cur.Y - _dragStart.Y);

            if (InteractionConfig.PivotInvertX) dx = -dx;
            if (InteractionConfig.PivotInvertY) dy = -dy;

            float sensitivity = InteractionConfig.PivotSensitivity;
            _yaw = _dragStartYaw + dx * sensitivity;
            _pitch = Math.Clamp(_dragStartPitch + dy * sensitivity, -30f, 30f);
            _lastInteraction = DateTime.Now;
            InvalidateVisual();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            _lastInteraction = DateTime.Now;
            e.Pointer.Capture(null);
        }

        private void OnAutoRotateTick(object? sender, EventArgs e)
        {
            if (_isDragging) return;
            if ((DateTime.Now - _lastInteraction).TotalSeconds < 2.0) return;
            _yaw += 0.4f;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var b = Bounds;
            if (b.Width <= 0 || b.Height <= 0) return;

            context.FillRectangle(Brushes.Transparent, new Rect(b.Size));

            var projected = Pivot3DRenderer.Render(
                _faces, _yaw, _pitch,
                (float)b.Width, (float)b.Height,
                0.78f);

            context.Custom(new Pivot3DDrawOperation(
                new Rect(b.Size), projected, _skinImage));
        }

        private static SKImage? BitmapToSkImage(Bitmap? bmp)
        {
            if (bmp == null) return null;
            try
            {
                int w = bmp.PixelSize.Width;
                int h = bmp.PixelSize.Height;
                if (w <= 0 || h <= 0) return null;

                var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                byte[] pixels = new byte[w * h * 4];
                unsafe
                {
                    fixed (byte* p = pixels)
                    {
                        bmp.CopyPixels(new PixelRect(0, 0, w, h), (IntPtr)p, pixels.Length, w * 4);
                    }
                }
                return SKImage.FromPixelCopy(info, pixels, w * 4);
            }
            catch { return null; }
        }

        private class Pivot3DDrawOperation : ICustomDrawOperation
        {
            private readonly List<ProjectedFace> _faces;
            private readonly SKImage? _skin;

            public Rect Bounds { get; }

            public Pivot3DDrawOperation(Rect bounds, List<ProjectedFace> faces, SKImage? skin)
            {
                Bounds = bounds;
                _faces = faces;
                _skin = skin;
            }

            public void Dispose() { }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation? other) => false;

            public void Render(ImmediateDrawingContext context)
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null) return;

                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;

                if (_skin == null || _faces.Count == 0) return;

                float texW = _skin.Width;
                float texH = _skin.Height;

                foreach (var f in _faces)
                {
                    var positions = new SKPoint[]
                    {
                        new SKPoint(f.X0, f.Y0),
                        new SKPoint(f.X1, f.Y1),
                        new SKPoint(f.X2, f.Y2),
                        new SKPoint(f.X0, f.Y0),
                        new SKPoint(f.X2, f.Y2),
                        new SKPoint(f.X3, f.Y3),
                    };

                    var texs = new SKPoint[]
                    {
                        new SKPoint(f.T0.U * texW, f.T0.V * texH),
                        new SKPoint(f.T1.U * texW, f.T1.V * texH),
                        new SKPoint(f.T2.U * texW, f.T2.V * texH),
                        new SKPoint(f.T0.U * texW, f.T0.V * texH),
                        new SKPoint(f.T2.U * texW, f.T2.V * texH),
                        new SKPoint(f.T3.U * texW, f.T3.V * texH),
                    };

                    byte s = (byte)Math.Clamp(f.Shade * 255f, 0f, 255f);
                    var colors = new SKColor[]
                    {
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                        new SKColor(s, s, s, 255),
                    };

                    using var shader = SKShader.CreateImage(
                        _skin,
                        SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp);

                    using var paint = new SKPaint
                    {
                        IsAntialias = false,
#pragma warning disable CS0618
                        FilterQuality = SKFilterQuality.None,
#pragma warning restore CS0618
                        Shader = shader
                    };

                    canvas.DrawVertices(SKVertexMode.Triangles, positions, texs, colors, paint);
                }
            }
        }
    }
}