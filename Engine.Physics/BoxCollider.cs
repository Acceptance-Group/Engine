using Engine.Core;
using Engine.Math;

namespace Engine.Physics;

public class BoxCollider : Component
{
    private Engine.Math.Vector3 _size = Engine.Math.Vector3.One;

    public Engine.Math.Vector3 Size
    {
        get => _size;
        set
        {
            _size = value;
            UpdatePhysicsShape();
        }
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (Transform != null)
        {
            _size = Transform.Scale;
        }
        
        var physics = GameObject?.GetComponent<Rigidbody>();
        if (physics != null && physics.Body == null)
        {
            physics.ColliderShape = new BoxColliderShape(_size);
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
            if (MathF.Abs(scale.X - _size.X) > 0.001f || 
                MathF.Abs(scale.Y - _size.Y) > 0.001f || 
                MathF.Abs(scale.Z - _size.Z) > 0.001f)
            {
                _size = scale;
                UpdatePhysicsShape();
            }
        }
    }

    private void UpdatePhysicsShape()
    {
        var physics = GameObject?.GetComponent<Rigidbody>();
        if (physics != null)
        {
            physics.ColliderShape = new BoxColliderShape(_size);
            if (physics.Body != null)
            {
                physics.Body.ColliderShape = new BoxColliderShape(_size);
                var world = GameObject?.Scene?.PhysicsWorld as PhysicsWorld;
                world?.UpdateBodyShape(physics.Body);
            }
        }
    }
}

