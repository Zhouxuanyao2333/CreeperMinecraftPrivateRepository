using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Project.Launch.Tools.JavaConfig
{
    /// <summary>
    /// 皮肤裁切工具：从皮肤 PNG 裁出方形头像
    /// </summary>
    public static class SkinCropper
    {
        /// <summary>
        /// 同步裁切：返回 (face 头像, hat 帽子层)
        /// 支持 64×64 / 128×128 / 256×256 等任意倍率皮肤
        /// </summary>
        public static (Bitmap? face, Bitmap? hat) Crop(Stream stream)
        {
            try
            {
                using var src = new Bitmap(stream);
                int width = src.PixelSize.Width;
                int height = src.PixelSize.Height;

                // 动态计算比例
                int scale = width / 64;
                if (scale < 1) return (null, null);

                int faceX = 8 * scale;
                int faceY = 8 * scale;
                int faceSize = 8 * scale;
                int hatX = 40 * scale;
                int hatY = 8 * scale;

                var faceCropped = new CroppedBitmap(src, new PixelRect(faceX, faceY, faceSize, faceSize));
                var hatCropped = new CroppedBitmap(src, new PixelRect(hatX, hatY, faceSize, faceSize));

                var faceBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = faceBmp.CreateDrawingContext())
                    ctx.DrawImage(faceCropped, new Rect(0, 0, faceSize, faceSize));

                var hatBmp = new RenderTargetBitmap(new PixelSize(faceSize, faceSize), new Vector(96, 96));
                using (var ctx = hatBmp.CreateDrawingContext())
                    ctx.DrawImage(hatCropped, new Rect(0, 0, faceSize, faceSize));

                return (faceBmp, hatBmp);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// 从资源 URI 同步裁切 face 头像
        /// </summary>
        public static Bitmap? CropFaceFromAsset(string assetUri)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                var (face, _) = Crop(stream);
                return face;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从资源 URI 同步裁切 face + hat
        /// </summary>
        public static (Bitmap? face, Bitmap? hat) CropFromAsset(string assetUri)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(assetUri));
                return Crop(stream);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}