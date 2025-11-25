using Engine.Core;
using Engine.Graphics;
using Engine.Math;

namespace Engine.Physics;

public class MeshCollider : Component
{
    private Engine.Math.Vector3 _lastScale = Engine.Math.Vector3.One;

    public Engine.Math.Vector3 Size { get; private set; } = Engine.Math.Vector3.One;

    protected override void OnAttached()
    {
        base.OnAttached();
        _lastScale = Transform?.Scale ?? Engine.Math.Vector3.One;
        RefreshSize(force: true);
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (Transform == null)
            return;

        var scale = Transform.Scale;
        if (scale != _lastScale)
        {
            _lastScale = scale;
            RefreshSize(force: true);
        }
    }

    public void RefreshSize(bool force = false)
    {
        var newSize = Transform?.Scale ?? Engine.Math.Vector3.One;
        if (!force && newSize == Size)
            return;

        Size = newSize;
        _lastScale = newSize;

        var physics = GameObject?.GetComponent<PhysicsComponent>();
        physics?.UpdateColliderFromMesh(Size);
    }

    protected override void OnDetached()
    {
        base.OnDetached();
        var fallbackSize = GameObject?.Transform?.Scale ?? Engine.Math.Vector3.One;
        GameObject?.GetComponent<PhysicsComponent>()?.UpdateColliderFromMesh(fallbackSize);
    }
}

