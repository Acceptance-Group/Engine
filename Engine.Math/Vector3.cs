using System;
using System.Runtime.InteropServices;

namespace Engine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(float value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    public Vector3(Vector2 xy, float z)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
    }

    public static Vector3 Zero => new Vector3(0, 0, 0);
    public static Vector3 One => new Vector3(1, 1, 1);
    public static Vector3 UnitX => new Vector3(1, 0, 0);
    public static Vector3 UnitY => new Vector3(0, 1, 0);
    public static Vector3 UnitZ => new Vector3(0, 0, 1);
    public static Vector3 Up => new Vector3(0, 1, 0);
    public static Vector3 Down => new Vector3(0, -1, 0);
    public static Vector3 Forward => new Vector3(0, 0, -1);
    public static Vector3 Backward => new Vector3(0, 0, 1);
    public static Vector3 Left => new Vector3(-1, 0, 0);
    public static Vector3 Right => new Vector3(1, 0, 0);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
    public float LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3 Normalized()
    {
        float length = Length;
        if (length > 0.00001f)
            return new Vector3(X / length, Y / length, Z / length);
        return Zero;
    }

    public void Normalize()
    {
        float length = Length;
        if (length > 0.00001f)
        {
            X /= length;
            Y /= length;
            Z /= length;
        }
    }

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        return a + (b - a) * t;
    }

    public static float Dot(Vector3 a, Vector3 b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );
    }

    public static float Distance(Vector3 a, Vector3 b)
    {
        return (a - b).Length;
    }

    public static float DistanceSquared(Vector3 a, Vector3 b)
    {
        return (a - b).LengthSquared;
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 v) => new Vector3(-v.X, -v.Y, -v.Z);
    public static Vector3 operator *(Vector3 v, float scalar) => new Vector3(v.X * scalar, v.Y * scalar, v.Z * scalar);
    public static Vector3 operator *(float scalar, Vector3 v) => new Vector3(v.X * scalar, v.Y * scalar, v.Z * scalar);
    public static Vector3 operator /(Vector3 v, float scalar) => new Vector3(v.X / scalar, v.Y / scalar, v.Z / scalar);
    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";
}

