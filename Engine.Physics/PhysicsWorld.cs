using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using BepuPhysics.Trees;
using Engine.Math;
using EngineVector3 = Engine.Math.Vector3;
using EngineQuaternion = Engine.Math.Quaternion;

namespace Engine.Physics;

public class PhysicsWorld : IDisposable
{
    private const float FixedTimeStep = 1f / 60f;
    private const float MaxDeltaTime = 1f / 10f;
    private const int MaxSubstepsPerFrame = 8;

    private readonly BufferPool _bufferPool = new();
    private ThreadDispatcher? _threadDispatcher;
    private Simulation? _simulation;
    private float _timeAccumulator;
    private System.Numerics.Vector3 _gravity = new(0, -9.81f, 0);
    private readonly HashSet<PhysicsBody> _registeredBodies = new();
    private readonly Dictionary<PhysicsBody, BodyHandle> _dynamicBodyHandles = new();
    private readonly Dictionary<BodyHandle, PhysicsBody> _handleToBody = new();
    private readonly Dictionary<PhysicsBody, StaticHandle> _staticBodyHandles = new();
    private readonly Dictionary<StaticHandle, PhysicsBody> _staticHandleToBody = new();

    public EngineVector3 Gravity
    {
        get => new EngineVector3(_gravity.X, _gravity.Y, _gravity.Z);
        set => _gravity = new System.Numerics.Vector3(value.X, value.Y, value.Z);
    }

    public bool IsSimulating { get; private set; }

    public void StartSimulation()
    {
        if (IsSimulating)
            return;

        DestroySimulation();

        if (_threadDispatcher == null)
        {
            var workerCount = System.Math.Max(1, Environment.ProcessorCount - 1);
            _threadDispatcher = new ThreadDispatcher(workerCount, 65536);
        }

        _simulation = Simulation.Create(
            _bufferPool,
            new PhysicsNarrowPhaseCallbacks(),
            new PhysicsPoseIntegratorCallbacks(_gravity),
            new SolveDescription(8, 1));
        _timeAccumulator = 0f;

        foreach (var body in _registeredBodies)
        {
            CreateRuntimeBody(body);
        }

        IsSimulating = true;
    }

    public void StopSimulation()
    {
        if (!IsSimulating)
            return;

        IsSimulating = false;
        DestroySimulation();
        _timeAccumulator = 0f;
    }

    public void RegisterBody(PhysicsBody body)
    {
        if (_registeredBodies.Add(body) && IsSimulating && _simulation != null)
        {
            CreateRuntimeBody(body);
        }
    }

    public void UnregisterBody(PhysicsBody body)
    {
        _registeredBodies.Remove(body);
        if (IsSimulating)
        {
            RemoveRuntimeBody(body);
        }
    }

    private void CreateRuntimeBody(PhysicsBody body)
    {
        if (_simulation == null)
            return;

        var shape = CreateShape(body);
        var pose = CreatePose(body);
        var treatAsStatic = !body.IsActive || body.Mass <= 0;
        if (treatAsStatic)
        {
            var staticDescription = new StaticDescription(pose.Position, pose.Orientation, shape.ShapeIndex);
            var staticHandle = _simulation.Statics.Add(staticDescription);
            _staticBodyHandles[body] = staticHandle;
            _staticHandleToBody[staticHandle] = body;
            body.ShapeIndex = shape.ShapeIndex;
            body.AttachStaticHandle(this, staticHandle);
            return;
        }

        var collidable = new CollidableDescription(shape.ShapeIndex, 0.1f);
        var activity = new BodyActivityDescription(0.01f);
        BodyHandle handle;
        if (body.IsKinematic)
        {
            var description = BodyDescription.CreateKinematic(pose, collidable, activity);
            handle = _simulation.Bodies.Add(description);
        }
        else
        {
            var description = BodyDescription.CreateDynamic(pose, shape.Inertia, collidable, activity);
            handle = _simulation.Bodies.Add(description);
        }

        _dynamicBodyHandles[body] = handle;
        _handleToBody[handle] = body;
        body.ShapeIndex = shape.ShapeIndex;
        body.AttachHandle(this, handle);
    }

    private void RemoveRuntimeBody(PhysicsBody body)
    {
        if (_simulation == null)
            return;

        if (_dynamicBodyHandles.TryGetValue(body, out var handle))
        {
            _simulation.Bodies.Remove(handle);
            _dynamicBodyHandles.Remove(body);
            _handleToBody.Remove(handle);
        }
        else if (_staticBodyHandles.TryGetValue(body, out var staticHandle))
        {
            _simulation.Statics.Remove(staticHandle);
            _staticBodyHandles.Remove(body);
            _staticHandleToBody.Remove(staticHandle);
        }

        if (body.ShapeIndex.HasValue)
        {
            _simulation.Shapes.Remove(body.ShapeIndex.Value);
            body.ShapeIndex = null;
        }

        body.DetachHandle();
    }

    public void Update(float deltaTime)
    {
        if (!IsSimulating || _simulation == null)
            return;

        var clampedDelta = System.MathF.Min(deltaTime, MaxDeltaTime);
        _timeAccumulator += clampedDelta;

        var steps = 0;
        while (_timeAccumulator >= FixedTimeStep && steps < MaxSubstepsPerFrame)
        {
            StepSimulation(FixedTimeStep);
            _timeAccumulator -= FixedTimeStep;
            steps++;
        }
    }

    private void StepSimulation(float timeStep)
    {
        if (_simulation == null)
            return;

        if (_threadDispatcher != null)
        {
            _simulation.Timestep(timeStep, _threadDispatcher);
        }
        else
        {
            _simulation.Timestep(timeStep);
        }
    }

    public bool Raycast(Engine.Math.Vector3 from, Engine.Math.Vector3 to, out RaycastHit hit)
    {
        hit = new RaycastHit();
        if (_simulation == null)
            return false;

        var origin = ToNumerics(from);
        var target = ToNumerics(to);
        var direction = target - origin;
        var length = direction.Length();
        if (length <= float.Epsilon)
            return false;

        direction /= length;
        var handler = new ClosestRayHitHandler();
        _simulation.RayCast(origin, direction, length, ref handler, 0);
        if (!handler.Hit)
            return false;

        hit.Distance = handler.T;
        var point = origin + direction * handler.T;
        hit.Point = new EngineVector3(point.X, point.Y, point.Z);
        if (TryResolveBody(handler.Collidable, out var body))
        {
            hit.Body = body;
        }
        return true;

    }

    private struct ClosestRayHitHandler : IRayHitHandler
    {
        public bool Hit;
        public float T;
        public CollidableReference Collidable;

        public bool AllowTest(CollidableReference collidable) => true;

        public bool AllowTest(CollidableReference collidable, int childIndex) => true;

        public void OnRayHit(in RayData ray, ref float maximumT, float t, in System.Numerics.Vector3 normal, CollidableReference collidable, int childIndex)
        {
            if (!Hit || t < T)
            {
                Hit = true;
                T = t;
                Collidable = collidable;
                maximumT = t;
            }
        }
    }

    private bool TryResolveBody(CollidableReference collidable, out PhysicsBody? body)
    {
        body = null;
        switch (collidable.Mobility)
        {
            case CollidableMobility.Dynamic:
            case CollidableMobility.Kinematic:
                if (_handleToBody.TryGetValue(collidable.BodyHandle, out var dynamicBody))
                {
                    body = dynamicBody;
                    return true;
                }
                break;
            case CollidableMobility.Static:
                if (_staticHandleToBody.TryGetValue(collidable.StaticHandle, out var staticBody))
                {
                    body = staticBody;
                    return true;
                }
                break;
        }

        return false;
    }

    internal bool TryGetBodyReference(BodyHandle handle, out BodyReference body)
    {
        if (_simulation != null)
        {
            body = _simulation.Bodies.GetBodyReference(handle);
            return true;
        }

        body = default;
        return false;
    }

    internal bool TryGetStaticReference(StaticHandle handle, out StaticReference reference)
    {
        if (_simulation != null && _simulation.Statics.StaticExists(handle))
        {
            reference = _simulation.Statics.GetStaticReference(handle);
            return true;
        }

        reference = default;
        return false;
    }

    internal void UpdateStaticBounds(StaticHandle handle)
    {
        if (_simulation == null)
            return;

        _simulation.Statics.UpdateBounds(handle);
    }

    public void Dispose()
    {
        DestroySimulation();
        _threadDispatcher?.Dispose();
        _threadDispatcher = null;
    }

    private void DestroySimulation()
    {
        if (_simulation != null)
        {
            var dynamicBodies = new List<PhysicsBody>(_dynamicBodyHandles.Keys);
            foreach (var body in dynamicBodies)
            {
                RemoveRuntimeBody(body);
            }

            var staticBodies = new List<PhysicsBody>(_staticBodyHandles.Keys);
            foreach (var body in staticBodies)
            {
                RemoveRuntimeBody(body);
            }

            _simulation.Dispose();
            _simulation = null;
        }

        _dynamicBodyHandles.Clear();
        _handleToBody.Clear();
        _staticBodyHandles.Clear();
        _staticHandleToBody.Clear();
        _bufferPool.Clear();
        _timeAccumulator = 0f;
    }

    internal void UpdateBodyPose(PhysicsBody body)
    {
        if (_simulation == null)
            return;

        if (_dynamicBodyHandles.TryGetValue(body, out var handle))
        {
            var reference = _simulation.Bodies.GetBodyReference(handle);
            reference.Pose.Position = ToNumerics(body.Position);
            reference.Pose.Orientation = ToNumerics(body.Rotation);
            if (body.IsKinematic)
            {
                reference.Velocity.Linear = System.Numerics.Vector3.Zero;
                reference.Velocity.Angular = System.Numerics.Vector3.Zero;
            }
        }
        else if (_staticBodyHandles.TryGetValue(body, out var staticHandle))
        {
            var reference = _simulation.Statics.GetStaticReference(staticHandle);
            reference.Pose.Position = ToNumerics(body.Position);
            reference.Pose.Orientation = ToNumerics(body.Rotation);
            _simulation.Statics.UpdateBounds(staticHandle);
        }
    }

    private RigidPose CreatePose(PhysicsBody body)
    {
        return new RigidPose(ToNumerics(body.Position), ToNumerics(body.Rotation));
    }

    private ShapeInfo CreateShape(PhysicsBody body)
    {
        var collider = body.ColliderShape;
        if (collider is BoxColliderShape box)
        {
            if (_simulation == null)
                throw new InvalidOperationException("Simulation is not initialized.");

            var width = GetSanitizedSize(box.Size.X);
            var height = GetSanitizedSize(box.Size.Y);
            var depth = GetSanitizedSize(box.Size.Z);
            var shape = new Box(width, height, depth);
            var inertia = body.IsKinematic ? default : shape.ComputeInertia(System.MathF.Max(0.0001f, body.Mass));
            var shapeIndex = _simulation.Shapes.Add(shape);
            return new ShapeInfo(shapeIndex, inertia);
        }

        if (collider is SphereColliderShape sphere)
        {
            if (_simulation == null)
                throw new InvalidOperationException("Simulation is not initialized.");

            var radius = GetSanitizedSize(sphere.Radius);
            var shape = new Sphere(radius);
            var inertia = body.IsKinematic ? default : shape.ComputeInertia(System.MathF.Max(0.0001f, body.Mass));
            var shapeIndex = _simulation.Shapes.Add(shape);
            return new ShapeInfo(shapeIndex, inertia);
        }

        if (_simulation == null)
            throw new InvalidOperationException("Simulation is not initialized.");

        var defaultShape = new Box(1f, 1f, 1f);
        var defaultInertia = body.IsKinematic ? default : defaultShape.ComputeInertia(System.MathF.Max(0.0001f, body.Mass));
        var defaultIndex = _simulation.Shapes.Add(defaultShape);
        return new ShapeInfo(defaultIndex, defaultInertia);
    }

    private static float GetSanitizedSize(float value)
    {
        var sanitized = System.MathF.Abs(SanitizeFloat(value));
        return System.MathF.Max(0.001f, System.MathF.Min(sanitized, 1000f));
    }

    private static System.Numerics.Vector3 ToNumerics(EngineVector3 value)
    {
        return new System.Numerics.Vector3(
            SanitizeFloat(value.X),
            SanitizeFloat(value.Y),
            SanitizeFloat(value.Z));
    }

    private static System.Numerics.Quaternion ToNumerics(EngineQuaternion value)
    {
        var quaternion = new System.Numerics.Quaternion(
            SanitizeFloat(value.X),
            SanitizeFloat(value.Y),
            SanitizeFloat(value.Z),
            SanitizeFloat(value.W));

        var lengthSquared =
            quaternion.X * quaternion.X +
            quaternion.Y * quaternion.Y +
            quaternion.Z * quaternion.Z +
            quaternion.W * quaternion.W;
        if (!float.IsFinite(lengthSquared) || lengthSquared < 1e-6f)
            return System.Numerics.Quaternion.Identity;

        return System.Numerics.Quaternion.Normalize(quaternion);
    }

    private static float SanitizeFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return System.Math.Clamp(value, -1_000_000f, 1_000_000f);
    }

    private readonly struct ShapeInfo
    {
        public ShapeInfo(TypedIndex shapeIndex, BodyInertia inertia)
        {
            ShapeIndex = shapeIndex;
            Inertia = inertia;
        }

        public TypedIndex ShapeIndex { get; }
        public BodyInertia Inertia { get; }
    }
}

public class RaycastHit
{
    public float Distance { get; set; } = -1;
    public EngineVector3 Point { get; set; }
    public PhysicsBody? Body { get; set; }
}

internal struct PhysicsNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public SpringSettings SpringSettings;
    public float FrictionCoefficient;
    public float MaximumRecoveryVelocity;

    public PhysicsNarrowPhaseCallbacks(float friction = 1f, float maxRecoveryVelocity = 2f)
    {
        FrictionCoefficient = friction;
        MaximumRecoveryVelocity = maxRecoveryVelocity;
        SpringSettings = new SpringSettings(30f, 1f);
    }

    public void Initialize(Simulation simulation)
    {
    }

    public void Dispose()
    {
    }

    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        if (a.Mobility == CollidableMobility.Static && b.Mobility == CollidableMobility.Static)
            return false;

        speculativeMargin = 0.1f;
        return true;
    }

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, ref ConvexContactManifold manifold) => true;

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, ref NonconvexContactManifold manifold) => true;

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref NonconvexContactManifold manifold) => true;

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial = new PairMaterialProperties
        {
            FrictionCoefficient = FrictionCoefficient,
            MaximumRecoveryVelocity = MaximumRecoveryVelocity,
            SpringSettings = SpringSettings
        };
        return true;
    }

}

internal struct PhysicsPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public System.Numerics.Vector3 Gravity;
    public float LinearDamping;
    public float AngularDamping;

    public PhysicsPoseIntegratorCallbacks(System.Numerics.Vector3 gravity, float linearDamping = 0.01f, float angularDamping = 0.01f)
    {
        Gravity = gravity;
        LinearDamping = linearDamping;
        AngularDamping = angularDamping;
    }

    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

    public bool AllowSubstepsForUnconstrainedBodies => false;

    public bool IntegrateVelocityForKinematics => true;

    public void Initialize(Simulation simulation)
    {
    }

    public void PrepareForIntegration(float dt)
    {
    }

    public void IntegrateVelocity(System.Numerics.Vector<int> bodyIndices, Vector3Wide positions, QuaternionWide orientations, BodyInertiaWide localInertias, System.Numerics.Vector<int> integrationMask, int workerIndex, System.Numerics.Vector<float> dt, ref BodyVelocityWide velocity)
    {
        Vector3Wide gravityDt;
        Vector3Wide.Broadcast(Gravity, out gravityDt);
        Vector3Wide.Scale(gravityDt, dt, out gravityDt);
        Vector3Wide.Add(velocity.Linear, gravityDt, out velocity.Linear);

        if (LinearDamping > 0f)
        {
            var damping = System.Numerics.Vector<float>.One - new System.Numerics.Vector<float>(LinearDamping) * dt;
            damping = System.Numerics.Vector.Min(System.Numerics.Vector<float>.One, System.Numerics.Vector.Max(System.Numerics.Vector<float>.Zero, damping));
            Vector3Wide.Scale(velocity.Linear, damping, out velocity.Linear);
        }

        if (AngularDamping > 0f)
        {
            var damping = System.Numerics.Vector<float>.One - new System.Numerics.Vector<float>(AngularDamping) * dt;
            damping = System.Numerics.Vector.Min(System.Numerics.Vector<float>.One, System.Numerics.Vector.Max(System.Numerics.Vector<float>.Zero, damping));
            Vector3Wide.Scale(velocity.Angular, damping, out velocity.Angular);
        }
    }
}



