using System;
using System.Runtime.InteropServices;

namespace Engine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Vector4 : IEquatable<Vector4>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(float value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4(Vector3 xyz, float w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    public static Vector4 Zero => new Vector4(0, 0, 0, 0);
    public static Vector4 One => new Vector4(1, 1, 1, 1);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
    public float LengthSquared => X * X + Y * Y + Z * Z + W * W;

    public Vector4 Normalized()
    {
        float length = Length;
        if (length > 0.00001f)
            return new Vector4(X / length, Y / length, Z / length, W / length);
        return Zero;
    }

    public static float Dot(Vector4 a, Vector4 b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;
    }

    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        return a + (b - a) * t;
    }

    public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    public static Vector4 operator -(Vector4 v) => new Vector4(-v.X, -v.Y, -v.Z, -v.W);
    public static Vector4 operator *(Vector4 v, float scalar) => new Vector4(v.X * scalar, v.Y * scalar, v.Z * scalar, v.W * scalar);
    public static Vector4 operator *(float scalar, Vector4 v) => new Vector4(v.X * scalar, v.Y * scalar, v.Z * scalar, v.W * scalar);
    public static Vector4 operator /(Vector4 v, float scalar) => new Vector4(v.X / scalar, v.Y / scalar, v.Z / scalar, v.W / scalar);
    public static bool operator ==(Vector4 a, Vector4 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Vector4 a, Vector4 b) => !(a == b);

    public bool Equals(Vector4 other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override bool Equals(object? obj) => obj is Vector4 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}

