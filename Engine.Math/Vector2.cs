using System;
using System.Runtime.InteropServices;

namespace Engine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    public float X;
    public float Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2(float value)
    {
        X = value;
        Y = value;
    }

    public static Vector2 Zero => new Vector2(0, 0);
    public static Vector2 One => new Vector2(1, 1);
    public static Vector2 UnitX => new Vector2(1, 0);
    public static Vector2 UnitY => new Vector2(0, 1);

    public float Length => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;

    public Vector2 Normalized()
    {
        float length = Length;
        if (length > 0.00001f)
            return new Vector2(X / length, Y / length);
        return Zero;
    }

    public void Normalize()
    {
        float length = Length;
        if (length > 0.00001f)
        {
            X /= length;
            Y /= length;
        }
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        return a + (b - a) * t;
    }

    public static float Dot(Vector2 a, Vector2 b)
    {
        return a.X * b.X + a.Y * b.Y;
    }

    public static float Distance(Vector2 a, Vector2 b)
    {
        return (a - b).Length;
    }

    public static float DistanceSquared(Vector2 a, Vector2 b)
    {
        return (a - b).LengthSquared;
    }

    public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 v) => new Vector2(-v.X, -v.Y);
    public static Vector2 operator *(Vector2 v, float scalar) => new Vector2(v.X * scalar, v.Y * scalar);
    public static Vector2 operator *(float scalar, Vector2 v) => new Vector2(v.X * scalar, v.Y * scalar);
    public static Vector2 operator /(Vector2 v, float scalar) => new Vector2(v.X / scalar, v.Y / scalar);
    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}

