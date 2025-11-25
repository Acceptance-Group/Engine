using Engine.Core;
using Engine.Math;

namespace Engine.Physics;

public class Rigidbody : Component
{
    private PhysicsBody? _body;
    private PhysicsWorld? _world;
    private Engine.Math.Vector3 _lastScale = Engine.Math.Vector3.One;
    private const float ScaleThreshold = 0.001f;

    public bool IsKinematic { get; set; }
    public float Mass { get; set; } = 1.0f;
    public ColliderShape? ColliderShape { get; set; }

    public PhysicsBody? Body => _body;

    protected override void OnAttached()
    {
        base.OnAttached();

        if (GameObject?.Scene == null)
            return;

        _world = GameObject.Scene.PhysicsWorld as PhysicsWorld;
        if (_world == null)
        {
            _world = new PhysicsWorld();
            GameObject.Scene.PhysicsWorld = _world;
        }

        if (ColliderShape == null)
        {
            var boxCollider = GameObject?.GetComponent<BoxCollider>();
            var sphereCollider = GameObject?.GetComponent<SphereCollider>();
            
            if (boxCollider != null)
            {
                ColliderShape = new BoxColliderShape(boxCollider.Size);
            }
            else if (sphereCollider != null)
            {
                ColliderShape = new SphereColliderShape(sphereCollider.Radius);
            }
            else
            {
                var size = GetColliderSize();
                _lastScale = size;
                ColliderShape = new BoxColliderShape(size);
            }
        }

        if (Transform != null)
        {
            _body = new PhysicsBody
            {
                Position = Transform.Position,
                Rotation = Transform.Rotation,
                Mass = Mass,
                IsKinematic = IsKinematic,
                ColliderShape = ColliderShape
            };

            _world.RegisterBody(_body);
            
            if (_world.IsSimulating)
            {
                _world.UpdateBodyPose(_body);
            }
        }
    }

    protected override void OnDetached()
    {
        base.OnDetached();

        if (_body != null && _world != null)
        {
            _world.UnregisterBody(_body);
            _body.Dispose();
            _body = null;
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (_body == null || Transform == null || _world == null)
            return;

        if (MathF.Abs(_body.Mass - Mass) > 0.001f || _body.IsKinematic != IsKinematic)
        {
            _body.Mass = Mass;
            _body.IsKinematic = IsKinematic;
            _world.UpdateBodyMass(_body);
        }

        var boxCollider = GameObject?.GetComponent<BoxCollider>();
        var sphereCollider = GameObject?.GetComponent<SphereCollider>();
        
        if (boxCollider == null && sphereCollider == null)
        {
            var currentScale = GetColliderSize();
            if (!ScaleEquals(currentScale, _lastScale))
            {
                _lastScale = currentScale;
                UpdateColliderSize(currentScale);
            }
        }

        if (!_world.IsSimulating)
            return;

        if (IsKinematic)
        {
            if (_body.Handle.HasValue && _world.TryGetBodyReference(_body.Handle.Value, out var bodyRef))
            {
                bodyRef.Pose.Position = new System.Numerics.Vector3(Transform.Position.X, Transform.Position.Y, Transform.Position.Z);
                bodyRef.Pose.Orientation = new System.Numerics.Quaternion(Transform.Rotation.X, Transform.Rotation.Y, Transform.Rotation.Z, Transform.Rotation.W);
                bodyRef.Velocity.Linear = System.Numerics.Vector3.Zero;
                bodyRef.Velocity.Angular = System.Numerics.Vector3.Zero;
            }
        }
        else
        {
            if (_body.Handle.HasValue && _world.TryGetBodyReference(_body.Handle.Value, out var bodyRef))
            {
                var pos = bodyRef.Pose.Position;
                var rot = bodyRef.Pose.Orientation;
                Transform.Position = new Engine.Math.Vector3(pos.X, pos.Y, pos.Z);
                Transform.Rotation = new Engine.Math.Quaternion(rot.X, rot.Y, rot.Z, rot.W);
            }
        }
    }

    public void SyncWithTransform()
    {
        if (_body == null || Transform == null)
            return;

        _body.Position = Transform.Position;
        _body.Rotation = Transform.Rotation;
        _world?.UpdateBodyPose(_body);
    }

    internal void UpdateColliderFromMesh(Engine.Math.Vector3 size)
    {
        if (!ScaleEquals(size, _lastScale))
        {
            _lastScale = size;
            UpdateColliderSize(size);
        }
    }

    private void UpdateColliderSize(Engine.Math.Vector3 size)
    {
        if (_body == null)
            return;

        var clampedSize = new Engine.Math.Vector3(
            MathF.Max(0.001f, MathF.Abs(size.X)),
            MathF.Max(0.001f, MathF.Abs(size.Y)),
            MathF.Max(0.001f, MathF.Abs(size.Z))
        );

        var newShape = new BoxColliderShape(clampedSize);
        ColliderShape = newShape;
        _body.ColliderShape = newShape;

        if (_world != null && _world.IsSimulating)
        {
            _world.UpdateBodyShape(_body);
        }
    }

    private Engine.Math.Vector3 GetColliderSize()
    {
        var meshCollider = GameObject?.GetComponent<MeshCollider>();
        if (meshCollider != null)
            return meshCollider.Size;

        return Transform?.Scale ?? Engine.Math.Vector3.One;
    }

    private static bool ScaleEquals(Engine.Math.Vector3 a, Engine.Math.Vector3 b)
    {
        return MathF.Abs(a.X - b.X) < ScaleThreshold &&
               MathF.Abs(a.Y - b.Y) < ScaleThreshold &&
               MathF.Abs(a.Z - b.Z) < ScaleThreshold;
    }
}

