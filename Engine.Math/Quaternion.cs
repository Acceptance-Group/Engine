using System;
using System.Runtime.InteropServices;

namespace Engine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quaternion Identity => new Quaternion(0, 0, 0, 1);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
    public float LengthSquared => X * X + Y * Y + Z * Z + W * W;

    public Quaternion Normalized()
    {
        float length = Length;
        if (length > 0.00001f)
            return new Quaternion(X / length, Y / length, Z / length, W / length);
        return Identity;
    }

    public void Normalize()
    {
        float length = Length;
        if (length > 0.00001f)
        {
            X /= length;
            Y /= length;
            Z /= length;
            W /= length;
        }
    }

    public Quaternion Conjugate()
    {
        return new Quaternion(-X, -Y, -Z, W);
    }

    public Quaternion Inverse()
    {
        float lengthSq = LengthSquared;
        if (lengthSq > 0.00001f)
        {
            float invLengthSq = 1.0f / lengthSq;
            return new Quaternion(-X * invLengthSq, -Y * invLengthSq, -Z * invLengthSq, W * invLengthSq);
        }
        return Identity;
    }

    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        float halfAngle = angle * 0.5f;
        float s = MathF.Sin(halfAngle);
        float c = MathF.Cos(halfAngle);
        axis.Normalize();
        return new Quaternion(axis.X * s, axis.Y * s, axis.Z * s, c);
    }

    public static Quaternion FromEulerAngles(float pitch, float yaw, float roll)
    {
        float halfPitch = pitch * 0.5f;
        float halfYaw = yaw * 0.5f;
        float halfRoll = roll * 0.5f;

        float sp = MathF.Sin(halfPitch);
        float cp = MathF.Cos(halfPitch);
        float sy = MathF.Sin(halfYaw);
        float cy = MathF.Cos(halfYaw);
        float sr = MathF.Sin(halfRoll);
        float cr = MathF.Cos(halfRoll);

        return new Quaternion(
            cy * sp * cr + sy * cp * sr,
            sy * cp * cr - cy * sp * sr,
            cy * cp * sr - sy * sp * cr,
            cy * cp * cr + sy * sp * sr
        );
    }

    public static Quaternion FromEulerAngles(Vector3 euler)
    {
        return FromEulerAngles(euler.X, euler.Y, euler.Z);
    }

    public Vector3 ToEulerAngles()
    {
        float sinr_cosp = 2.0f * (W * X + Y * Z);
        float cosr_cosp = 1.0f - 2.0f * (X * X + Y * Y);
        float roll = MathF.Atan2(sinr_cosp, cosr_cosp);

        float sinp = 2.0f * (W * Y - Z * X);
        float pitch = MathF.Abs(sinp) >= 1.0f ? MathF.CopySign(MathF.PI / 2.0f, sinp) : MathF.Asin(sinp);

        float siny_cosp = 2.0f * (W * Z + X * Y);
        float cosy_cosp = 1.0f - 2.0f * (Y * Y + Z * Z);
        float yaw = MathF.Atan2(siny_cosp, cosy_cosp);

        return new Vector3(pitch, yaw, roll);
    }

    public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        float dot = Dot(a, b);
        if (dot < 0.0f)
        {
            b = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }

        if (dot > 0.9995f)
        {
            return Lerp(a, b, t).Normalized();
        }

        float clampedDot = dot < -1.0f ? -1.0f : (dot > 1.0f ? 1.0f : dot);
        float theta = MathF.Acos(clampedDot);
        float sinTheta = MathF.Sin(theta);
        float w1 = MathF.Sin((1.0f - t) * theta) / sinTheta;
        float w2 = MathF.Sin(t * theta) / sinTheta;

        return new Quaternion(
            a.X * w1 + b.X * w2,
            a.Y * w1 + b.Y * w2,
            a.Z * w1 + b.Z * w2,
            a.W * w1 + b.W * w2
        );
    }

    public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
    {
        return new Quaternion(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t
        );
    }

    public static float Dot(Quaternion a, Quaternion b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
    }

    public static Quaternion operator *(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );
    }

    public static Vector3 operator *(Quaternion q, Vector3 v)
    {
        float x = q.X * 2.0f;
        float y = q.Y * 2.0f;
        float z = q.Z * 2.0f;
        float xx = q.X * x;
        float yy = q.Y * y;
        float zz = q.Z * z;
        float xy = q.X * y;
        float xz = q.X * z;
        float yz = q.Y * z;
        float wx = q.W * x;
        float wy = q.W * y;
        float wz = q.W * z;

        return new Vector3(
            (1.0f - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z,
            (xy + wz) * v.X + (1.0f - (xx + zz)) * v.Y + (yz - wx) * v.Z,
            (xz - wy) * v.X + (yz + wx) * v.Y + (1.0f - (xx + yy)) * v.Z
        );
    }

    public static bool operator ==(Quaternion a, Quaternion b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

    public bool Equals(Quaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override bool Equals(object? obj) => obj is Quaternion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}

