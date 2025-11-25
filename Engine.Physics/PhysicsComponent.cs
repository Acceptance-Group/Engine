using Engine.Core;
using Engine.Math;

namespace Engine.Physics;

public class PhysicsComponent : Component
{
    private PhysicsBody? _body;
    private PhysicsWorld? _world;
    private Engine.Math.Vector3 _lastScale = Engine.Math.Vector3.One;

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

        var size = GetColliderSize();
        _lastScale = size;
        ColliderShape ??= new BoxColliderShape(size);

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

        if (_body != null && Transform != null)
        {
            bool simulating = _world?.IsSimulating ?? false;
            if (IsKinematic || !simulating)
            {
                _body.Position = Transform.Position;
                _body.Rotation = Transform.Rotation;
                if (simulating)
                {
                    _world?.UpdateBodyPose(_body);
                }
            }
            else
            {
                Transform.Position = _body.Position;
                Transform.Rotation = _body.Rotation;
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
        _lastScale = size;
        ColliderShape = new BoxColliderShape(size);

        if (_world != null && _world.IsSimulating)
            return;

        if (_body != null)
        {
            _body.ColliderShape = ColliderShape;
            _body.Position = Transform?.Position ?? _body.Position;
            _body.Rotation = Transform?.Rotation ?? _body.Rotation;
        }
    }

    private Engine.Math.Vector3 GetColliderSize()
    {
        var meshCollider = GameObject?.GetComponent<MeshCollider>();
        if (meshCollider != null)
            return meshCollider.Size;

        return Transform?.Scale ?? Engine.Math.Vector3.One;
    }
}
