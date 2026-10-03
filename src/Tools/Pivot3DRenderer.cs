using System;
using System.Collections.Generic;
using System.Numerics;

namespace Project.Launch.Tools
{
    public class ProjectedFace
    {
        public float X0, Y0, X1, Y1, X2, Y2, X3, Y3;
        public Vec2 T0, T1, T2, T3;
        public float Depth;
        public float Shade;
        public bool Overlay;
    }

    public static class Pivot3DRenderer
    {
        public static List<ProjectedFace> Render(
            List<SkinFace> faces,
            float yawDeg, float pitchDeg,
            float viewW, float viewH,
            float scaleFactor = 0.75f)
        {
            var result = new List<ProjectedFace>(faces.Count);
            if (faces.Count == 0 || viewW <= 0 || viewH <= 0) return result;

            var center = new Vector3(0f, 16f, 0f);
            const float cameraDist = 60f;
            float scale = viewH * scaleFactor / 32f;
            float cx = viewW / 2f;
            float cy = viewH / 2f;

            float yawRad = yawDeg * MathF.PI / 180f;
            float pitchRad = pitchDeg * MathF.PI / 180f;
            float cosY = MathF.Cos(yawRad), sinY = MathF.Sin(yawRad);
            float cosP = MathF.Cos(pitchRad), sinP = MathF.Sin(pitchRad);

            foreach (var f in faces)
            {
                var p0 = Transform(f.V0, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p1 = Transform(f.V1, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p2 = Transform(f.V2, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);
                var p3 = Transform(f.V3, center, cosY, sinY, cosP, sinP, cameraDist, scale, cx, cy);

                float avgZ = (p0.Z + p1.Z + p2.Z + p3.Z) * 0.25f;

                // 背面剔除（逆时针为正面）
                float cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross <= 0f) continue;

                float shade = EstimateShade(f);

                result.Add(new ProjectedFace
                {
                    X0 = p0.X, Y0 = p0.Y,
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    X3 = p3.X, Y3 = p3.Y,
                    T0 = f.T0, T1 = f.T1, T2 = f.T2, T3 = f.T3,
                    Depth = avgZ,
                    Shade = shade,
                    Overlay = f.Overlay
                });
            }

            // 画家算法：远的先画，近的覆盖远的
            result.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            return result;
        }

        private static Vector3 Transform(
            Vec3 v, Vector3 center,
            float cosY, float sinY, float cosP, float sinP,
            float cameraDist, float scale, float cx, float cy)
        {
            float x = v.X - center.X;
            float y = v.Y - center.Y;
            float z = v.Z - center.Z;

            // Yaw（绕 Y 轴）
            float x1 = x * cosY + z * sinY;
            float z1 = -x * sinY + z * cosY;

            // Pitch（绕 X 轴）
            float y1 = y * cosP - z1 * sinP;
            float z2 = y * sinP + z1 * cosP;

            // ★ 关键修正：相机在 +Z 方向看向 -Z，+Z 面（正脸）离相机更近
            float depth = cameraDist - z2;
            if (depth < 1f) depth = 1f;
            float persp = cameraDist / depth;

            float sx = cx + x1 * scale * persp;
            float sy = cy - y1 * scale * persp;

            return new Vector3(sx, sy, depth);
        }

        private static float EstimateShade(SkinFace f)
        {
            // 面法线（叉乘两条边）
            float ax = f.V1.X - f.V0.X, ay = f.V1.Y - f.V0.Y, az = f.V1.Z - f.V0.Z;
            float bx = f.V2.X - f.V0.X, by = f.V2.Y - f.V0.Y, bz = f.V2.Z - f.V0.Z;
            float nx = ay * bz - az * by;
            float ny = az * bx - ax * bz;
            float nz = ax * by - ay * bx;

            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 0.0001f) return 1.0f;
            nx /= len; ny /= len; nz /= len;

            // 光照方向：从相机方向略偏左上
            float lx = 0.3f, ly = 0.6f, lz = 0.7f;
            float llen = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= llen; ly /= llen; lz /= llen;

            float dot = nx * lx + ny * ly + nz * lz;

            // 环境光 0.6 + 漫反射
            return 0.6f + 0.4f * MathF.Max(0f, dot);
        }
    }
}