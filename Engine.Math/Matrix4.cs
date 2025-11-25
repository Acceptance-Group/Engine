using System;
using System.Runtime.InteropServices;

namespace Engine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix4 : IEquatable<Matrix4>
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public Matrix4(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    public static Matrix4 Identity => new Matrix4(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    );

    public static Matrix4 Zero => new Matrix4(
        0, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0
    );

    public float this[int row, int column]
    {
        get
        {
            return (row, column) switch
            {
                (0, 0) => M11, (0, 1) => M12, (0, 2) => M13, (0, 3) => M14,
                (1, 0) => M21, (1, 1) => M22, (1, 2) => M23, (1, 3) => M24,
                (2, 0) => M31, (2, 1) => M32, (2, 2) => M33, (2, 3) => M34,
                (3, 0) => M41, (3, 1) => M42, (3, 2) => M43, (3, 3) => M44,
                _ => throw new IndexOutOfRangeException()
            };
        }
        set
        {
            switch (row, column)
            {
                case (0, 0): M11 = value; break;
                case (0, 1): M12 = value; break;
                case (0, 2): M13 = value; break;
                case (0, 3): M14 = value; break;
                case (1, 0): M21 = value; break;
                case (1, 1): M22 = value; break;
                case (1, 2): M23 = value; break;
                case (1, 3): M24 = value; break;
                case (2, 0): M31 = value; break;
                case (2, 1): M32 = value; break;
                case (2, 2): M33 = value; break;
                case (2, 3): M34 = value; break;
                case (3, 0): M41 = value; break;
                case (3, 1): M42 = value; break;
                case (3, 2): M43 = value; break;
                case (3, 3): M44 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public static Matrix4 CreateTranslation(Vector3 position)
    {
        return new Matrix4(
            1, 0, 0, position.X,
            0, 1, 0, position.Y,
            0, 0, 1, position.Z,
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreateScale(Vector3 scale)
    {
        return new Matrix4(
            scale.X, 0, 0, 0,
            0, scale.Y, 0, 0,
            0, 0, scale.Z, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreateScale(float scale)
    {
        return CreateScale(new Vector3(scale));
    }

    public static Matrix4 CreateRotationX(float angle)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);
        return new Matrix4(
            1, 0, 0, 0,
            0, c, -s, 0,
            0, s, c, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreateRotationY(float angle)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);
        return new Matrix4(
            c, 0, s, 0,
            0, 1, 0, 0,
            -s, 0, c, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreateRotationZ(float angle)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);
        return new Matrix4(
            c, -s, 0, 0,
            s, c, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreateFromQuaternion(Quaternion q)
    {
        float xx = q.X * q.X;
        float yy = q.Y * q.Y;
        float zz = q.Z * q.Z;
        float xy = q.X * q.Y;
        float xz = q.X * q.Z;
        float yz = q.Y * q.Z;
        float wx = q.W * q.X;
        float wy = q.W * q.Y;
        float wz = q.W * q.Z;

        return new Matrix4(
            1.0f - 2.0f * (yy + zz), 2.0f * (xy - wz), 2.0f * (xz + wy), 0.0f,
            2.0f * (xy + wz), 1.0f - 2.0f * (xx + zz), 2.0f * (yz - wx), 0.0f,
            2.0f * (xz - wy), 2.0f * (yz + wx), 1.0f - 2.0f * (xx + yy), 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    public static Matrix4 CreateTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Matrix4 translation = CreateTranslation(position);
        Matrix4 rotationMatrix = CreateFromQuaternion(rotation);
        Matrix4 scaleMatrix = CreateScale(scale);
        return translation * rotationMatrix * scaleMatrix;
    }

    public static Matrix4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 z = (eye - target).Normalized();
        Vector3 x = Vector3.Cross(up, z).Normalized();
        Vector3 y = Vector3.Cross(z, x);

        return new Matrix4(
            x.X, x.Y, x.Z, -Vector3.Dot(x, eye),
            y.X, y.Y, y.Z, -Vector3.Dot(y, eye),
            z.X, z.Y, z.Z, -Vector3.Dot(z, eye),
            0, 0, 0, 1
        );
    }

    public static Matrix4 CreatePerspectiveFieldOfView(float fov, float aspectRatio, float nearPlane, float farPlane)
    {
        float f = 1.0f / MathF.Tan(fov * 0.5f);
        float range = farPlane - nearPlane;

        return new Matrix4(
            f / aspectRatio, 0, 0, 0,
            0, f, 0, 0,
            0, 0, -(farPlane + nearPlane) / range, -2.0f * farPlane * nearPlane / range,
            0, 0, -1, 0
        );
    }

    public static Matrix4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        float range = farPlane - nearPlane;
        return new Matrix4(
            2.0f / width, 0, 0, 0,
            0, 2.0f / height, 0, 0,
            0, 0, -2.0f / range, -(farPlane + nearPlane) / range,
            0, 0, 0, 1
        );
    }

    public Matrix4 Transpose()
    {
        return new Matrix4(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44
        );
    }

    public float Determinant()
    {
        float a = M11, b = M12, c = M13, d = M14;
        float e = M21, f = M22, g = M23, h = M24;
        float i = M31, j = M32, k = M33, l = M34;
        float m = M41, n = M42, o = M43, p = M44;

        float kp_lo = k * p - l * o;
        float jp_ln = j * p - l * n;
        float jo_kn = j * o - k * n;
        float ip_lm = i * p - l * m;
        float io_km = i * o - k * m;
        float in_jm = i * n - j * m;

        return a * (f * kp_lo - g * jp_ln + h * jo_kn) -
               b * (e * kp_lo - g * ip_lm + h * io_km) +
               c * (e * jp_ln - f * ip_lm + h * in_jm) -
               d * (e * jo_kn - f * io_km + g * in_jm);
    }

    public Vector3 ToEulerAngles()
    {
        float sy = MathF.Sqrt(M11 * M11 + M21 * M21);
        bool singular = sy < 0.00001f;

        float x, y, z;
        if (!singular)
        {
            x = MathF.Atan2(M32, M33);
            y = MathF.Atan2(-M31, sy);
            z = MathF.Atan2(M21, M11);
        }
        else
        {
            x = MathF.Atan2(-M23, M22);
            y = MathF.Atan2(-M31, sy);
            z = 0;
        }

        return new Vector3(x, y, z);
    }

    public Matrix4 Inverse()
    {
        float det = Determinant();
        if (MathF.Abs(det) < 0.00001f)
            return Identity;

        float invDet = 1.0f / det;

        float a = M11, b = M12, c = M13, d = M14;
        float e = M21, f = M22, g = M23, h = M24;
        float i = M31, j = M32, k = M33, l = M34;
        float m = M41, n = M42, o = M43, p = M44;

        return new Matrix4(
            (f * (k * p - l * o) - g * (j * p - l * n) + h * (j * o - k * n)) * invDet,
            -(b * (k * p - l * o) - c * (j * p - l * n) + d * (j * o - k * n)) * invDet,
            (b * (g * p - h * o) - c * (f * p - h * n) + d * (f * o - g * n)) * invDet,
            -(b * (g * l - h * k) - c * (f * l - h * j) + d * (f * k - g * j)) * invDet,
            -(e * (k * p - l * o) - g * (i * p - l * m) + h * (i * o - k * m)) * invDet,
            (a * (k * p - l * o) - c * (i * p - l * m) + d * (i * o - k * m)) * invDet,
            -(a * (g * p - h * o) - c * (e * p - h * m) + d * (e * o - g * m)) * invDet,
            (a * (g * l - h * k) - c * (e * l - h * i) + d * (e * k - g * i)) * invDet,
            (e * (j * p - l * n) - f * (i * p - l * m) + h * (i * n - j * m)) * invDet,
            -(a * (j * p - l * n) - b * (i * p - l * m) + d * (i * n - j * m)) * invDet,
            (a * (f * p - h * n) - b * (e * p - h * m) + d * (e * n - f * m)) * invDet,
            -(a * (f * l - h * j) - b * (e * l - h * i) + d * (e * j - f * i)) * invDet,
            -(e * (j * o - k * n) - f * (i * o - k * m) + g * (i * n - j * m)) * invDet,
            (a * (j * o - k * n) - b * (i * o - k * m) + c * (i * n - j * m)) * invDet,
            -(a * (f * o - g * n) - b * (e * o - g * m) + c * (e * n - f * m)) * invDet,
            (a * (f * k - g * j) - b * (e * k - g * i) + c * (e * j - f * i)) * invDet
        );
    }

    public static Matrix4 operator *(Matrix4 a, Matrix4 b)
    {
        return new Matrix4(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
            a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
        );
    }

    public static Vector4 operator *(Matrix4 m, Vector4 v)
    {
        return new Vector4(
            m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z + m.M14 * v.W,
            m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z + m.M24 * v.W,
            m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z + m.M34 * v.W,
            m.M41 * v.X + m.M42 * v.Y + m.M43 * v.Z + m.M44 * v.W
        );
    }

    public static bool operator ==(Matrix4 a, Matrix4 b) =>
        a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14 &&
        a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24 &&
        a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34 &&
        a.M41 == b.M41 && a.M42 == b.M42 && a.M43 == b.M43 && a.M44 == b.M44;

    public static bool operator !=(Matrix4 a, Matrix4 b) => !(a == b);

    public bool Equals(Matrix4 other) => this == other;
    public override bool Equals(object? obj) => obj is Matrix4 other && Equals(other);
    public override int GetHashCode()
    {
        return HashCode.Combine(
            HashCode.Combine(M11, M12, M13, M14),
            HashCode.Combine(M21, M22, M23, M24),
            HashCode.Combine(M31, M32, M33, M34),
            HashCode.Combine(M41, M42, M43, M44)
        );
    }
    public override string ToString() => $"[[{M11}, {M12}, {M13}, {M14}], [{M21}, {M22}, {M23}, {M24}], [{M31}, {M32}, {M33}, {M34}], [{M41}, {M42}, {M43}, {M44}]]";
}

