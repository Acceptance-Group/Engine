using System.Numerics;
using BepuPhysics;
using Engine.Core;
using Engine.Math;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace Engine.Physics;

public class PhysicsBody : Disposable
{
    private NumericsVector3 _position;
    private NumericsQuaternion _rotation = NumericsQuaternion.Identity;
    private NumericsVector3 _originalPosition;
    private NumericsQuaternion _originalRotation = NumericsQuaternion.Identity;
    private BodyHandle? _handle;
    private StaticHandle? _staticHandle;
    private PhysicsWorld? _world;

    public Engine.Math.Vector3 Position
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var pos = body.Pose.Position;
                _position = pos;
                return new Engine.Math.Vector3(pos.X, pos.Y, pos.Z);
            }
            if (_staticHandle.HasValue && _world != null && _world.IsSimulating && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                var pos = staticRef.Pose.Position;
                _position = pos;
                return new Engine.Math.Vector3(pos.X, pos.Y, pos.Z);
            }
            return new Engine.Math.Vector3(_position.X, _position.Y, _position.Z);
        }
        set
        {
            _position = new NumericsVector3(value.X, value.Y, value.Z);
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                body.Pose.Position = _position;
            }
            else if (_staticHandle.HasValue && _world != null && _world.IsSimulating && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                staticRef.Pose.Position = _position;
            }
        }
    }

    public Engine.Math.Quaternion Rotation
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var rot = body.Pose.Orientation;
                _rotation = rot;
                return new Engine.Math.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            }
            if (_staticHandle.HasValue && _world != null && _world.IsSimulating && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                var rot = staticRef.Pose.Orientation;
                _rotation = rot;
                return new Engine.Math.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            }
            return new Engine.Math.Quaternion(_rotation.X, _rotation.Y, _rotation.Z, _rotation.W);
        }
        set
        {
            _rotation = new NumericsQuaternion(value.X, value.Y, value.Z, value.W);
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                body.Pose.Orientation = _rotation;
            }
            else if (_staticHandle.HasValue && _world != null && _world.IsSimulating && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                staticRef.Pose.Orientation = _rotation;
            }
        }
    }

    public Engine.Math.Vector3 Velocity
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var vel = body.Velocity.Linear;
                return new Engine.Math.Vector3(vel.X, vel.Y, vel.Z);
            }
            return Engine.Math.Vector3.Zero;
        }
        set
        {
            if (_handle.HasValue && _world != null && _world.IsSimulating && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                body.Velocity.Linear = new NumericsVector3(value.X, value.Y, value.Z);
            }
        }
    }

    public float Mass { get; set; } = 1.0f;
    public bool IsKinematic { get; set; }
    public bool IsActive { get; set; } = true;
    public ColliderShape? ColliderShape { get; set; }

    internal BodyHandle? Handle => _handle;
    internal StaticHandle? StaticHandle => _staticHandle;

    internal void AttachHandle(PhysicsWorld world, BodyHandle handle)
    {
        _world = world;
        _handle = handle;
        _staticHandle = null;
        _originalPosition = _position;
        _originalRotation = _rotation;
    }

    internal void AttachStaticHandle(PhysicsWorld world, StaticHandle handle)
    {
        _world = world;
        _staticHandle = handle;
        _handle = null;
        _originalPosition = _position;
        _originalRotation = _rotation;
    }

    public void ResetToOriginal()
    {
        Position = new Engine.Math.Vector3(_originalPosition.X, _originalPosition.Y, _originalPosition.Z);
        Rotation = new Engine.Math.Quaternion(_originalRotation.X, _originalRotation.Y, _originalRotation.Z, _originalRotation.W);
        Velocity = Engine.Math.Vector3.Zero;
    }

    internal void DetachHandle()
    {
        _handle = null;
        _staticHandle = null;
        _world = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _handle = null;
            _staticHandle = null;
            _world = null;
        }
    }
}

public abstract class ColliderShape
{
}

public class BoxColliderShape : ColliderShape
{
    public Engine.Math.Vector3 Size { get; set; }

    public BoxColliderShape(Engine.Math.Vector3 size)
    {
        Size = size;
    }
}

public class SphereColliderShape : ColliderShape
{
    public float Radius { get; set; }

    public SphereColliderShape(float radius)
    {
        Radius = radius;
    }
}
