using Engine.Core;
using Engine.Math;

namespace Engine.Physics;

public class SphereCollider : Component
{
    private float _radius = 0.5f;

    public float Radius
    {
        get => _radius;
        set
        {
            _radius = value;
            UpdatePhysicsShape();
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Transform != null)
        {
            var scale = Transform.Scale;
            _radius = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
        }
        
        var physics = GameObject?.GetComponent<Rigidbody>();
        if (physics != null && physics.Body == null)
        {
            physics.ColliderShape = new SphereColliderShape(_radius);
        }
        else
        {
            UpdatePhysicsShape();
        }
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        
        if (Transform != null)
        {
            var scale = Transform.Scale;
            var newRadius = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z));
            if (MathF.Abs(newRadius - _radius) > 0.0001f)
            {
                _radius = newRadius;
                UpdatePhysicsShape();
            }
        }
    }

    private void UpdatePhysicsShape()
    {
        var physics = GameObject?.GetComponent<Rigidbody>();
        if (physics != null)
        {
            physics.ColliderShape = new SphereColliderShape(_radius);
            if (physics.Body != null)
            {
                physics.Body.ColliderShape = new SphereColliderShape(_radius);
                var world = GameObject?.Scene?.PhysicsWorld as PhysicsWorld;
                world?.UpdateBodyShape(physics.Body);
            }
        }
    }
}

