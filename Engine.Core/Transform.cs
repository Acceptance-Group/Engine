using System;
using System.Collections.Generic;
using Engine.Math;

namespace Engine.Core;

public class Transform : Component
{
    private Vector3 _position = Vector3.Zero;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;
    private Transform? _parent;
    private readonly List<Transform> _children = new List<Transform>();
    private Matrix4? _localMatrix;
    private Matrix4? _worldMatrix;
    private bool _dirty = true;

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            MarkDirty();
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value) return;
            _rotation = value;
            MarkDirty();
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_scale == value) return;
            _scale = value;
            MarkDirty();
        }
    }

    public Vector3 EulerAngles
    {
        get => _rotation.ToEulerAngles();
        set => Rotation = Quaternion.FromEulerAngles(value);
    }

    public Transform? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value) return;
            if (_parent != null)
                _parent._children.Remove(this);
            _parent = value;
            if (_parent != null)
                _parent._children.Add(this);
            MarkDirty();
        }
    }

    public IReadOnlyList<Transform> Children => _children.AsReadOnly();

    public Vector3 Forward => (Rotation * Vector3.Forward).Normalized();
    public Vector3 Right => (Rotation * Vector3.Right).Normalized();
    public Vector3 Up => (Rotation * Vector3.Up).Normalized();

    public Matrix4 LocalMatrix
    {
        get
        {
            if (_localMatrix == null || _dirty)
            {
                _localMatrix = Matrix4.CreateTransform(_position, _rotation, _scale);
                _dirty = false;
            }
            return _localMatrix.Value;
        }
    }

    public Matrix4 WorldMatrix
    {
        get
        {
            if (_worldMatrix == null || _dirty)
            {
                if (_parent != null)
                    _worldMatrix = _parent.WorldMatrix * LocalMatrix;
                else
                    _worldMatrix = LocalMatrix;
                _dirty = false;
            }
            return _worldMatrix.Value;
        }
    }

    public Vector3 WorldPosition
    {
        get
        {
            Matrix4 world = WorldMatrix;
            return new Vector3(world.M14, world.M24, world.M34);
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
        _localMatrix = null;
        _worldMatrix = null;
        foreach (var child in _children)
            child.MarkDirty();
    }

    public void Translate(Vector3 translation)
    {
        Position += translation;
    }

    public void Rotate(Vector3 eulerAngles)
    {
        Rotation = Rotation * Quaternion.FromEulerAngles(eulerAngles);
    }

    public void Rotate(Vector3 axis, float angle)
    {
        Rotation = Rotation * Quaternion.FromAxisAngle(axis, angle);
    }

    public void LookAt(Vector3 target, Vector3 up)
    {
        Vector3 direction = (target - WorldPosition).Normalized();
        if (direction.LengthSquared < 0.0001f)
            return;

        Vector3 right = Vector3.Cross(up, direction).Normalized();
        Vector3 newUp = Vector3.Cross(direction, right).Normalized();

        Matrix4 rotationMatrix = new Matrix4(
            right.X, right.Y, right.Z, 0,
            newUp.X, newUp.Y, newUp.Z, 0,
            direction.X, direction.Y, direction.Z, 0,
            0, 0, 0, 1
        );

        Rotation = Quaternion.FromEulerAngles(rotationMatrix.ToEulerAngles());
    }
}

