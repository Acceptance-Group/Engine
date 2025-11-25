using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using Engine.Core;
using Engine.Math;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace Engine.Physics;

public class PhysicsBody : Disposable
{
    private NumericsVector3 _originalPosition;
    private NumericsQuaternion _originalRotation = NumericsQuaternion.Identity;
    private BodyHandle? _handle;
    private StaticHandle? _staticHandle;
    private PhysicsWorld? _world;

    public Engine.Math.Vector3 Position
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var position = body.Pose.Position;
                return new Engine.Math.Vector3(position.X, position.Y, position.Z);
            }
            if (_staticHandle.HasValue && _world != null && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                var position = staticRef.Pose.Position;
                return new Engine.Math.Vector3(position.X, position.Y, position.Z);
            }

            return new Engine.Math.Vector3(_originalPosition.X, _originalPosition.Y, _originalPosition.Z);
        }
        set
        {
            _originalPosition = new NumericsVector3(value.X, value.Y, value.Z);
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                body.Pose.Position = new NumericsVector3(value.X, value.Y, value.Z);
            }
            else if (_staticHandle.HasValue && _world != null && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                staticRef.Pose.Position = new NumericsVector3(value.X, value.Y, value.Z);
                _world.UpdateStaticBounds(_staticHandle.Value);
            }
        }
    }

    public Engine.Math.Quaternion Rotation
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var orientation = body.Pose.Orientation;
                return new Engine.Math.Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
            }
            if (_staticHandle.HasValue && _world != null && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                var orientation = staticRef.Pose.Orientation;
                return new Engine.Math.Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
            }

            return new Engine.Math.Quaternion(_originalRotation.X, _originalRotation.Y, _originalRotation.Z, _originalRotation.W);
        }
        set
        {
            _originalRotation = new NumericsQuaternion(value.X, value.Y, value.Z, value.W);
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                body.Pose.Orientation = new NumericsQuaternion(value.X, value.Y, value.Z, value.W);
            }
            else if (_staticHandle.HasValue && _world != null && _world.TryGetStaticReference(_staticHandle.Value, out var staticRef))
            {
                staticRef.Pose.Orientation = new NumericsQuaternion(value.X, value.Y, value.Z, value.W);
                _world.UpdateStaticBounds(_staticHandle.Value);
            }
        }
    }

    public Engine.Math.Vector3 Velocity
    {
        get
        {
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
            {
                var linear = body.Velocity.Linear;
                return new Engine.Math.Vector3(linear.X, linear.Y, linear.Z);
            }
            if (_staticHandle.HasValue)
            {
                return Engine.Math.Vector3.Zero;
            }

            return Engine.Math.Vector3.Zero;
        }
        set
        {
            if (_handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
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
    internal TypedIndex? ShapeIndex { get; set; }

    internal void AttachHandle(PhysicsWorld world, BodyHandle handle)
    {
        _world = world;
        _handle = handle;
        _staticHandle = null;
        _originalPosition = new NumericsVector3(Position.X, Position.Y, Position.Z);
        var rotation = Rotation;
        _originalRotation = new NumericsQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
    }

    internal void AttachStaticHandle(PhysicsWorld world, StaticHandle handle)
    {
        _world = world;
        _staticHandle = handle;
        _handle = null;
        _originalPosition = new NumericsVector3(Position.X, Position.Y, Position.Z);
        var rotation = Rotation;
        _originalRotation = new NumericsQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
    }

    internal void DetachHandle()
    {
        _handle = null;
        _staticHandle = null;
        _world = null;
    }

    public void ResetToOriginal()
    {
        Position = new Engine.Math.Vector3(_originalPosition.X, _originalPosition.Y, _originalPosition.Z);
        Rotation = new Engine.Math.Quaternion(_originalRotation.X, _originalRotation.Y, _originalRotation.Z, _originalRotation.W);
        Velocity = Engine.Math.Vector3.Zero;
    }

    public virtual void Update(float deltaTime)
    {
    }

    public virtual bool IntersectRay(Engine.Math.Vector3 from, Engine.Math.Vector3 to, out float distance)
    {
        distance = -1;
        return false;
    }

    public void AddForce(Engine.Math.Vector3 force)
    {
        if (!IsKinematic && IsActive && _handle.HasValue && _world != null && _world.TryGetBodyReference(_handle.Value, out var body))
        {
            var linear = body.Velocity.Linear;
            body.Velocity.Linear = linear + new NumericsVector3(force.X, force.Y, force.Z);
        }
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
    public abstract bool IntersectRay(Engine.Math.Vector3 from, Engine.Math.Vector3 to, Engine.Math.Vector3 position, Engine.Math.Quaternion rotation, out float distance);
}

public class BoxColliderShape : ColliderShape
{
    public Engine.Math.Vector3 Size { get; set; }

    public BoxColliderShape(Engine.Math.Vector3 size)
    {
        Size = size;
    }

    public override bool IntersectRay(Engine.Math.Vector3 from, Engine.Math.Vector3 to, Engine.Math.Vector3 position, Engine.Math.Quaternion rotation, out float distance)
    {
        distance = -1;
        return false;
    }
}

public class SphereColliderShape : ColliderShape
{
    public float Radius { get; set; }

    public SphereColliderShape(float radius)
    {
        Radius = radius;
    }

    public override bool IntersectRay(Engine.Math.Vector3 from, Engine.Math.Vector3 to, Engine.Math.Vector3 position, Engine.Math.Quaternion rotation, out float distance)
    {
        var direction = (to - from).Normalized();
        var oc = from - position;
        var a = Engine.Math.Vector3.Dot(direction, direction);
        var b = 2.0f * Engine.Math.Vector3.Dot(oc, direction);
        var c = Engine.Math.Vector3.Dot(oc, oc) - Radius * Radius;
        var discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            distance = -1;
            return false;
        }

        distance = (-b - MathF.Sqrt(discriminant)) / (2.0f * a);
        return distance >= 0;
    }
}

