using System;
using System.Diagnostics.CodeAnalysis;

namespace Castaway.Math
{
    [SuppressMessage("ReSharper", "ArgumentsStyleOther")]
    public readonly struct Quaternion
    {
        public readonly float W, X, Y, Z;

        public Vector3 XYZ => new(X, Y, Z);

        public Quaternion(float w, float x, float y, float z)
        {
            W = w;
            X = x;
            Y = y;
            Z = z;
        }

        public Quaternion(float w, Vector3 v) : this(w, v.X, v.Y, v.Z) {}

        public static Quaternion operator +(Quaternion a, Quaternion b) => new(
            a.W + b.W, a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Quaternion operator *(Quaternion a, Quaternion b) => new(
            w: a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
            x: a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            y: a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            z: a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X);

        public static float Inner(Quaternion a, Quaternion b) =>
            a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public Quaternion Conjugate() => new(W, -X, -Y, -Z);
        public float Norm() => MathF.Sqrt(W*W + X*X + Y*Y + Z*Z);

        public Quaternion Normalize()
        {
            var norm = Norm();
            return new Quaternion(W / norm, X / norm, Y / norm, Z / norm);
        }

        public Matrix3 ToMatrix()
        {
            return new(
                xx: 1 - 2 * (Y*Y + Z*Z),
                xy: 2 * (X * Y - W * Z),
                xz: 2 * (W * Y + X * Z),
                yx: 2 * (X * Y + W * Z),
                yy: 1 - 2 * (X*X + Z*Z),
                yz: 2 * (-W * X + Y * Z),
                zx: 2 * (-W * Y + X * Z),
                zy: 2 * (W * X + Y * Z),
                zz: 1 - 2 * (X*X + Y*Y));
        }

        public Matrix4 ToMatrix4()
        {
            var m = ToMatrix();
            return new Matrix4(
                new Vector4(m.X, 0),
                new Vector4(m.Y, 0),
                new Vector4(m.Z, 0),
                new Vector4(0, 0, 0, 1));
        }

        public static Quaternion Rotation(float x, float y, float z)
        {
            var cosYaw = MathF.Cos(z * .5f);
            var sinYaw = MathF.Sin(z * .5f);
            var cosPitch = MathF.Cos(y * .5f);
            var sinPitch = MathF.Sin(y * .5f);
            var cosRoll = MathF.Cos(x * .5f);
            var sinRoll = MathF.Sin(x * .5f);
            return new Quaternion(
                w: cosRoll * cosPitch * cosYaw + sinRoll * sinPitch * sinYaw,
                x: sinRoll * cosPitch * cosYaw + cosRoll * sinPitch * sinYaw,
                y: cosRoll * sinPitch * cosYaw + sinRoll * cosPitch * sinYaw,
                z: cosRoll * cosPitch * sinYaw + sinRoll * sinPitch * cosYaw);
        }

        public static Quaternion Rotation(Vector3 vector) => Rotation(vector.X, vector.Y, vector.Z);

        public static Quaternion DegreesRotation(float x, float y, float z) =>
            Rotation(MathEx.ToRadians(x), MathEx.ToRadians(y), MathEx.ToRadians(z));
        
        public static Quaternion DegreesRotation(Vector3 v) =>
            Rotation(MathEx.ToRadians(v));

        public Vector3 ToEulerAngles()
        {
            var vector = new Vector3();

            var xa = 2 * (W * X + Y * Z);
            var xb = 1 - 2 * (X * X + Y * Y);
            vector.X = MathF.Atan2(xa, xb);

            var ya = 2 * (W * Y - Z * X);
            vector.Y = MathF.Abs(ya) >= 1 
                ? MathF.CopySign(MathF.PI / 2, ya) 
                : MathF.Asin(ya);

            var za = 2 * (W * Z + X * Y);
            var zb = 1 - 2 * (Y * Y + Z * Z);
            vector.Z = MathF.Atan2(za, zb);

            return vector;
        }

        public static Quaternion Rotation(Vector3 axis, float angle) => new(
            w: MathF.Cos(angle / 2),
            x: MathF.Sin(angle / 2) * axis.X,
            y: MathF.Sin(angle / 2) * axis.Y,
            z: MathF.Sin(angle / 2) * axis.Z);

        public static Quaternion DegreesRotation(Vector3 axis, float angle) =>
            Rotation(axis, MathEx.ToRadians(angle));

        public static Vector3 operator *(Quaternion a, Vector3 b)
        {
            var t = Vector3.Cross(a.XYZ * 2, b);
            var v = b + t * a.W + Vector3.Cross(a.XYZ, t);
            return v;
        }
    }
}