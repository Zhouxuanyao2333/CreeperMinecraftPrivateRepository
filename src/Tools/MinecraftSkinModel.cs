using System.Collections.Generic;

namespace Project.Launch.Tools
{
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public struct Vec2
    {
        public float U, V;
        public Vec2(float u, float v) { U = u; V = v; }
    }

    public class SkinFace
    {
        public Vec3 V0, V1, V2, V3;
        public Vec2 T0, T1, T2, T3;
        public string Part = "";
        public bool Overlay;

        public SkinFace(Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3,
                        Vec2 t0, Vec2 t1, Vec2 t2, Vec2 t3,
                        string part, bool overlay)
        {
            V0 = v0; V1 = v1; V2 = v2; V3 = v3;
            T0 = t0; T1 = t1; T2 = t2; T3 = t3;
            Part = part;
            Overlay = overlay;
        }
    }

    public static class MinecraftSkinModel
    {
        private const float TEX_W = 64f;
        private const float TEX_H = 64f;

        public static List<SkinFace> Build()
        {
            var faces = new List<SkinFace>();

            // ============ 头部 8×8×8，中心 (0, 28, 0) ============
            AddBox(faces, "head", false,
                x0: -4f, y0: 24f, z0: -4f, x1: 4f, y1: 32f, z1: 4f,
                front:  (8, 8, 8, 8),
                back:   (24, 8, 8, 8),
                right:  (0, 8, 8, 8),
                left:   (16, 8, 8, 8),
                top:    (8, 0, 8, 8),
                bottom: (16, 0, 8, 8));

            // 帽子层（第二层）
            AddBox(faces, "hat", true,
                x0: -4.25f, y0: 23.75f, z0: -4.25f, x1: 4.25f, y1: 32.25f, z1: 4.25f,
                front:  (40, 8, 8, 8),
                back:   (56, 8, 8, 8),
                right:  (32, 8, 8, 8),
                left:   (48, 8, 8, 8),
                top:    (40, 0, 8, 8),
                bottom: (48, 0, 8, 8));

            // ============ 身体 8×12×4，中心 (0, 18, 0) ============
            AddBox(faces, "body", false,
                x0: -4f, y0: 12f, z0: -2f, x1: 4f, y1: 24f, z1: 2f,
                front:  (20, 20, 8, 12),
                back:   (32, 20, 8, 12),
                right:  (16, 20, 4, 12),
                left:   (28, 20, 4, 12),
                top:    (20, 16, 8, 4),
                bottom: (28, 16, 8, 4));

            AddBox(faces, "bodyOverlay", true,
                x0: -4.25f, y0: 11.75f, z0: -2.25f, x1: 4.25f, y1: 24.25f, z1: 2.25f,
                front:  (20, 36, 8, 12),
                back:   (32, 36, 8, 12),
                right:  (16, 36, 4, 12),
                left:   (28, 36, 4, 12),
                top:    (20, 32, 8, 4),
                bottom: (28, 32, 8, 4));

            // ============ 右臂 4×12×4，中心 (-6, 18, 0) ============
            AddBox(faces, "rightArm", false,
                x0: -8f, y0: 12f, z0: -2f, x1: -4f, y1: 24f, z1: 2f,
                front:  (44, 20, 4, 12),
                back:   (52, 20, 4, 12),
                right:  (40, 20, 4, 12),
                left:   (48, 20, 4, 12),
                top:    (44, 16, 4, 4),
                bottom: (48, 16, 4, 4));

            AddBox(faces, "rightArmOverlay", true,
                x0: -8.25f, y0: 11.75f, z0: -2.25f, x1: -3.75f, y1: 24.25f, z1: 2.25f,
                front:  (44, 36, 4, 12),
                back:   (52, 36, 4, 12),
                right:  (40, 36, 4, 12),
                left:   (48, 36, 4, 12),
                top:    (44, 32, 4, 4),
                bottom: (48, 32, 4, 4));

            // ============ 左臂 4×12×4，中心 (6, 18, 0) ============
            AddBox(faces, "leftArm", false,
                x0: 4f, y0: 12f, z0: -2f, x1: 8f, y1: 24f, z1: 2f,
                front:  (36, 52, 4, 12),
                back:   (44, 52, 4, 12),
                right:  (32, 52, 4, 12),
                left:   (40, 52, 4, 12),
                top:    (36, 48, 4, 4),
                bottom: (40, 48, 4, 4));

            AddBox(faces, "leftArmOverlay", true,
                x0: 3.75f, y0: 11.75f, z0: -2.25f, x1: 8.25f, y1: 24.25f, z1: 2.25f,
                front:  (52, 52, 4, 12),
                back:   (60, 52, 4, 12),
                right:  (48, 52, 4, 12),
                left:   (56, 52, 4, 12),
                top:    (52, 48, 4, 4),
                bottom: (56, 48, 4, 4));

            // ============ 右腿 4×12×4，中心 (-2, 6, 0) ============
            AddBox(faces, "rightLeg", false,
                x0: -4f, y0: 0f, z0: -2f, x1: 0f, y1: 12f, z1: 2f,
                front:  (4, 20, 4, 12),
                back:   (12, 20, 4, 12),
                right:  (0, 20, 4, 12),
                left:   (8, 20, 4, 12),
                top:    (4, 16, 4, 4),
                bottom: (8, 16, 4, 4));

            AddBox(faces, "rightLegOverlay", true,
                x0: -4.25f, y0: -0.25f, z0: -2.25f, x1: 0.25f, y1: 12.25f, z1: 2.25f,
                front:  (4, 36, 4, 12),
                back:   (12, 36, 4, 12),
                right:  (0, 36, 4, 12),
                left:   (8, 36, 4, 12),
                top:    (4, 32, 4, 4),
                bottom: (8, 32, 4, 4));

            // ============ 左腿 4×12×4，中心 (2, 6, 0) ============
            AddBox(faces, "leftLeg", false,
                x0: 0f, y0: 0f, z0: -2f, x1: 4f, y1: 12f, z1: 2f,
                front:  (20, 52, 4, 12),
                back:   (28, 52, 4, 12),
                right:  (16, 52, 4, 12),
                left:   (24, 52, 4, 12),
                top:    (20, 48, 4, 4),
                bottom: (24, 48, 4, 4));

            AddBox(faces, "leftLegOverlay", true,
                x0: -0.25f, y0: -0.25f, z0: -2.25f, x1: 4.25f, y1: 12.25f, z1: 2.25f,
                front:  (4, 52, 4, 12),
                back:   (12, 52, 4, 12),
                right:  (0, 52, 4, 12),
                left:   (8, 52, 4, 12),
                top:    (4, 48, 4, 4),
                bottom: (8, 48, 4, 4));

            return faces;
        }

        // ============================================================
        // 构建一个长方体
        // 顶点顺序：从模型外部看为逆时针（左上 → 右上 → 右下 → 左下）
        // ============================================================
        private static void AddBox(
            List<SkinFace> faces, string part, bool overlay,
            float x0, float y0, float z0, float x1, float y1, float z1,
            (float u, float v, float w, float h) front,
            (float u, float v, float w, float h) back,
            (float u, float v, float w, float h) right,
            (float u, float v, float w, float h) left,
            (float u, float v, float w, float h) top,
            (float u, float v, float w, float h) bottom)
        {
            // 前 (+Z)
            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z1),
                new Vec3(x1, y1, z1),
                new Vec3(x1, y0, z1),
                new Vec3(x0, y0, z1),
                front);

            // 后 (-Z)
            AddFace(faces, part, overlay,
                new Vec3(x1, y1, z0),
                new Vec3(x0, y1, z0),
                new Vec3(x0, y0, z0),
                new Vec3(x1, y0, z0),
                back);

            // 右面 (-X)：纹理中 "right" 面
            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z0),
                new Vec3(x0, y1, z1),
                new Vec3(x0, y0, z1),
                new Vec3(x0, y0, z0),
                right);

            // 左面 (+X)：纹理中 "left" 面
            AddFace(faces, part, overlay,
                new Vec3(x1, y1, z1),
                new Vec3(x1, y1, z0),
                new Vec3(x1, y0, z0),
                new Vec3(x1, y0, z1),
                left);

            // 顶面 (+Y)
            AddFace(faces, part, overlay,
                new Vec3(x0, y1, z0),
                new Vec3(x1, y1, z0),
                new Vec3(x1, y1, z1),
                new Vec3(x0, y1, z1),
                top);

            // 底面 (-Y)
            AddFace(faces, part, overlay,
                new Vec3(x1, y0, z0),
                new Vec3(x0, y0, z0),
                new Vec3(x0, y0, z1),
                new Vec3(x1, y0, z1),
                bottom);
        }

        private static void AddFace(
            List<SkinFace> faces, string part, bool overlay,
            Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3,
            (float u, float v, float w, float h) uv)
        {
            const float half = 0f;

            float uLeft = (uv.u + half) / TEX_W;
            float vTop = (uv.v + half) / TEX_H;
            float uRight = (uv.u + uv.w - half) / TEX_W;
            float vBottom = (uv.v + uv.h - half) / TEX_H;

            faces.Add(new SkinFace(
                v0, v1, v2, v3,
                new Vec2(uLeft, vTop),
                new Vec2(uRight, vTop),
                new Vec2(uRight, vBottom),
                new Vec2(uLeft, vBottom),
                part, overlay));
        }
    }
}