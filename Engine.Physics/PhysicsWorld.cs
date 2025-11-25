using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using Engine.Math;
using Engine.Core;
using EngineVector3 = Engine.Math.Vector3;
using EngineQuaternion = Engine.Math.Quaternion;

namespace Engine.Physics;

public class PhysicsWorld : IDisposable
{
    private const float FixedTimeStep = 1f / 60f;
    private const float MaxDeltaTime = 0.1f;

    private readonly BufferPool _bufferPool = new();
    private Simulation? _simulation;
    private float _timeAccumulator;
    private readonly HashSet<PhysicsBody> _registeredBodies = new();
    private readonly Dictionary<PhysicsBody, BodyHandle> _bodyHandles = new();
    private readonly Dictionary<BodyHandle, PhysicsBody> _handleToBody = new();
    private readonly Dictionary<PhysicsBody, StaticHandle> _staticHandles = new();
    private readonly Dictionary<StaticHandle, PhysicsBody> _staticHandleToBody = new();
    private readonly Dictionary<PhysicsBody, TypedIndex> _bodyShapeIndices = new();

    public EngineVector3 Gravity { get; set; } = new EngineVector3(0, -9.81f, 0);
    public bool IsSimulating { get; private set; }

    public void StartSimulation()
    {
        if (IsSimulating)
            return;

        StopSimulation();

        var gravity = new System.Numerics.Vector3(Gravity.X, Gravity.Y, Gravity.Z);
        _simulation = Simulation.Create(
            _bufferPool,
            new SimpleNarrowPhaseCallbacks(),
            new SimplePoseIntegratorCallbacks(gravity),
            new SolveDescription(8, 1));

        foreach (var body in _registeredBodies)
        {
            if (body.ColliderShape == null)
            {
                body.ColliderShape = new BoxColliderShape(EngineVector3.One);
            }

            var position = ToNumerics(body.Position);
            var rotation = ToNumerics(body.Rotation);
            
            if (!ValidatePosition(position))
            {
                Logger.Warning($"Invalid position for body, resetting to zero");
                body.Position = EngineVector3.Zero;
                position = System.Numerics.Vector3.Zero;
            }
            
            if (!ValidateQuaternion(rotation))
            {
                Logger.Warning($"Invalid rotation for body, resetting to identity");
                body.Rotation = EngineQuaternion.Identity;
                rotation = System.Numerics.Quaternion.Identity;
            }

            if (body.Mass <= 0)
            {
                AddStaticToSimulation(body);
            }
            else
            {
                var handle = AddBodyToSimulation(body);
                if (handle.HasValue && body.IsKinematic)
                {
                    var reference = _simulation.Bodies.GetBodyReference(handle.Value);
                    reference.LocalInertia = default;
                    reference.BecomeKinematic();
                }
            }
        }

        _timeAccumulator = 0f;
        IsSimulating = true;
    }

    public void StopSimulation()
    {
        if (!IsSimulating && _simulation == null)
            return;

        IsSimulating = false;

        if (_simulation != null)
        {
            var bodiesToRemove = new List<PhysicsBody>(_bodyHandles.Keys);
            foreach (var body in bodiesToRemove)
            {
                if (_bodyHandles.TryGetValue(body, out var handle))
                {
                    try
                    {
                        _simulation.Bodies.Remove(handle);
                    }
                    catch { }
                    _bodyHandles.Remove(body);
                    _handleToBody.Remove(handle);
                    body.DetachHandle();
                }
            }

            var staticsToRemove = new List<PhysicsBody>(_staticHandles.Keys);
            foreach (var body in staticsToRemove)
            {
                if (_staticHandles.TryGetValue(body, out var staticHandle))
                {
                    try
                    {
                        _simulation.Statics.Remove(staticHandle);
                    }
                    catch { }
                    _staticHandles.Remove(body);
                    _staticHandleToBody.Remove(staticHandle);
                    body.DetachHandle();
                }
            }

            try
            {
                _simulation.Dispose();
            }
            catch { }

            _simulation = null;
        }

        _bodyHandles.Clear();
        _handleToBody.Clear();
        _staticHandles.Clear();
        _staticHandleToBody.Clear();
        _bodyShapeIndices.Clear();
        _timeAccumulator = 0f;
    }

    public void RegisterBody(PhysicsBody body)
    {
        if (!_registeredBodies.Add(body))
            return;

        if (IsSimulating && _simulation != null)
        {
            if (body.Mass <= 0)
            {
                AddStaticToSimulation(body);
            }
            else
            {
                var handle = AddBodyToSimulation(body);
                if (handle.HasValue && body.IsKinematic)
                {
                    var reference = _simulation.Bodies.GetBodyReference(handle.Value);
                    reference.LocalInertia = default;
                    reference.BecomeKinematic();
                }
            }
        }
    }

    public void UnregisterBody(PhysicsBody body)
    {
        if (!_registeredBodies.Remove(body))
            return;

        if (_bodyHandles.TryGetValue(body, out var handle))
        {
            if (_simulation != null)
            {
                try
                {
                    _simulation.Bodies.Remove(handle);
                }
                catch { }
            }
            _bodyHandles.Remove(body);
            _handleToBody.Remove(handle);
            _bodyShapeIndices.Remove(body);
            body.DetachHandle();
        }
        else if (_staticHandles.TryGetValue(body, out var staticHandle))
        {
            if (_simulation != null)
            {
                try
                {
                    _simulation.Statics.Remove(staticHandle);
                }
                catch { }
            }
            _staticHandles.Remove(body);
            _staticHandleToBody.Remove(staticHandle);
            _bodyShapeIndices.Remove(body);
            body.DetachHandle();
        }
    }

    public void UpdateBodyShape(PhysicsBody body)
    {
        if (_simulation == null || !IsSimulating)
            return;

        if (body.ColliderShape == null)
            return;

        try
        {
            System.Numerics.Vector3 savedPosition = System.Numerics.Vector3.Zero;
            System.Numerics.Quaternion savedRotation = System.Numerics.Quaternion.Identity;
            System.Numerics.Vector3 savedLinearVelocity = System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 savedAngularVelocity = System.Numerics.Vector3.Zero;

            var wasDynamic = _bodyHandles.ContainsKey(body);
            var wasStatic = _staticHandles.ContainsKey(body);

            if (wasDynamic)
            {
                if (_bodyHandles.TryGetValue(body, out var handle))
                {
                    var reference = _simulation.Bodies.GetBodyReference(handle);
                    savedPosition = reference.Pose.Position;
                    savedRotation = reference.Pose.Orientation;
                    savedLinearVelocity = reference.Velocity.Linear;
                    savedAngularVelocity = reference.Velocity.Angular;

                    _simulation.Bodies.Remove(handle);
                    _bodyHandles.Remove(body);
                    _handleToBody.Remove(handle);
                    if (_bodyShapeIndices.TryGetValue(body, out var oldIndex))
                    {
                        try
                        {
                            _simulation.Shapes.Remove(oldIndex);
                        }
                        catch { }
                        _bodyShapeIndices.Remove(body);
                    }
                    body.DetachHandle();
                }
            }
            else if (wasStatic)
            {
                if (_staticHandles.TryGetValue(body, out var staticHandle))
                {
                    var reference = _simulation.Statics.GetStaticReference(staticHandle);
                    savedPosition = reference.Pose.Position;
                    savedRotation = reference.Pose.Orientation;

                    _simulation.Statics.Remove(staticHandle);
                    _staticHandles.Remove(body);
                    _staticHandleToBody.Remove(staticHandle);
                    if (_bodyShapeIndices.TryGetValue(body, out var oldIndex))
                    {
                        try
                        {
                            _simulation.Shapes.Remove(oldIndex);
                        }
                        catch { }
                        _bodyShapeIndices.Remove(body);
                    }
                    body.DetachHandle();
                }
            }

            body.Position = new EngineVector3(savedPosition.X, savedPosition.Y, savedPosition.Z);
            body.Rotation = new EngineQuaternion(savedRotation.X, savedRotation.Y, savedRotation.Z, savedRotation.W);

            if (body.Mass <= 0)
            {
                AddStaticToSimulation(body);
            }
            else
            {
                var handle = AddBodyToSimulation(body);
                if (handle.HasValue)
                {
                    var reference = _simulation.Bodies.GetBodyReference(handle.Value);
                    if (wasDynamic)
                    {
                        reference.Velocity.Linear = savedLinearVelocity;
                        reference.Velocity.Angular = savedAngularVelocity;
                    }
                    if (body.IsKinematic)
                    {
                        reference.LocalInertia = default;
                        reference.BecomeKinematic();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update body shape");
        }
    }

    private BodyHandle? AddBodyToSimulation(PhysicsBody body)
    {
        if (_simulation == null || _bodyHandles.ContainsKey(body))
            return null;

        if (body.ColliderShape == null)
        {
            body.ColliderShape = new BoxColliderShape(EngineVector3.One);
        }

        try
        {
            var shapeIndex = CreateShape(body);
            if (!shapeIndex.HasValue)
                return null;

            var position = ToNumerics(body.Position);
            var rotation = ToNumerics(body.Rotation);

            if (!ValidatePosition(position) || !ValidateQuaternion(rotation))
            {
                Logger.Warning("Invalid pose for body, using default");
                position = System.Numerics.Vector3.Zero;
                rotation = System.Numerics.Quaternion.Identity;
            }

            var inertia = ComputeInertia(body, shapeIndex.Value);
            var maxSize = GetMaxSize(body.ColliderShape);
            var speculativeMargin = MathF.Max(0.01f, maxSize * 0.1f);
            var description = BodyDescription.CreateDynamic(position, inertia, shapeIndex.Value, speculativeMargin);
            if (rotation != System.Numerics.Quaternion.Identity)
            {
                description.Pose.Orientation = rotation;
            }
            var handle = _simulation.Bodies.Add(description);

            _bodyHandles[body] = handle;
            _handleToBody[handle] = body;
            _bodyShapeIndices[body] = shapeIndex.Value;
            body.AttachHandle(this, handle);
            return handle;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add body to simulation");
            return null;
        }
    }

    private void AddStaticToSimulation(PhysicsBody body)
    {
        if (_simulation == null || _staticHandles.ContainsKey(body))
            return;

        if (body.ColliderShape == null)
        {
            body.ColliderShape = new BoxColliderShape(EngineVector3.One);
        }

        try
        {
            var shapeIndex = CreateShape(body);
            if (!shapeIndex.HasValue)
            {
                Logger.Warning("Failed to create shape for static body");
                return;
            }

            var position = ToNumerics(body.Position);
            var rotation = ToNumerics(body.Rotation);

            if (!ValidatePosition(position) || !ValidateQuaternion(rotation))
            {
                Logger.Warning("Invalid pose for static, using default");
                position = System.Numerics.Vector3.Zero;
                rotation = System.Numerics.Quaternion.Identity;
            }

            var staticDescription = new StaticDescription(position, rotation, shapeIndex.Value);
            var handle = _simulation.Statics.Add(staticDescription);

            _staticHandles[body] = handle;
            _staticHandleToBody[handle] = body;
            _bodyShapeIndices[body] = shapeIndex.Value;
            body.AttachStaticHandle(this, handle);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add static to simulation");
        }
    }

    private TypedIndex? CreateShape(PhysicsBody body)
    {
        if (_simulation == null)
            return null;

        var collider = body.ColliderShape ?? new BoxColliderShape(EngineVector3.One);

        TypedIndex shapeIndex;
        if (collider is BoxColliderShape box)
        {
            var w = MathF.Max(0.001f, MathF.Abs(box.Size.X));
            var h = MathF.Max(0.001f, MathF.Abs(box.Size.Y));
            var d = MathF.Max(0.001f, MathF.Abs(box.Size.Z));
            var shape = new Box(w, h, d);
            shapeIndex = _simulation.Shapes.Add(shape);
        }
        else if (collider is SphereColliderShape sphere)
        {
            var r = MathF.Max(0.001f, MathF.Abs(sphere.Radius));
            var shape = new Sphere(r);
            shapeIndex = _simulation.Shapes.Add(shape);
        }
        else
        {
            var shape = new Box(1f, 1f, 1f);
            shapeIndex = _simulation.Shapes.Add(shape);
        }

        return shapeIndex;
    }

    private float GetMaxSize(ColliderShape? collider)
    {
        if (collider is BoxColliderShape box)
        {
            return MathF.Max(MathF.Max(MathF.Abs(box.Size.X), MathF.Abs(box.Size.Y)), MathF.Abs(box.Size.Z));
        }
        else if (collider is SphereColliderShape sphere)
        {
            return MathF.Abs(sphere.Radius) * 2f;
        }
        return 1f;
    }

    private BodyInertia ComputeInertia(PhysicsBody body, TypedIndex shapeIndex)
    {
        if (body.IsKinematic || body.Mass <= 0)
            return default;

        var collider = body.ColliderShape ?? new BoxColliderShape(EngineVector3.One);
        var mass = MathF.Max(0.0001f, body.Mass);

        if (collider is BoxColliderShape box)
        {
            var w = MathF.Max(0.001f, MathF.Abs(box.Size.X));
            var h = MathF.Max(0.001f, MathF.Abs(box.Size.Y));
            var d = MathF.Max(0.001f, MathF.Abs(box.Size.Z));
            var shape = new Box(w, h, d);
            return shape.ComputeInertia(mass);
        }
        else if (collider is SphereColliderShape sphere)
        {
            var r = MathF.Max(0.001f, MathF.Abs(sphere.Radius));
            var shape = new Sphere(r);
            return shape.ComputeInertia(mass);
        }
        else
        {
            var shape = new Box(1f, 1f, 1f);
            return shape.ComputeInertia(mass);
        }
    }

    public void Update(float deltaTime)
    {
        if (!IsSimulating || _simulation == null)
            return;

        var clampedDelta = MathF.Min(deltaTime, MaxDeltaTime);
        _timeAccumulator += clampedDelta;

        var maxSteps = 10;
        var steps = 0;
        while (_timeAccumulator >= FixedTimeStep && steps < maxSteps)
        {
            try
            {
                _simulation.Timestep(FixedTimeStep);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Physics timestep failed");
                break;
            }
            _timeAccumulator -= FixedTimeStep;
            steps++;
        }
        
        foreach (var kvp in _handleToBody)
        {
            var body = kvp.Value;
            if (body != null && _simulation.Bodies.BodyExists(kvp.Key))
            {
                var reference = _simulation.Bodies.GetBodyReference(kvp.Key);
                body.Position = new EngineVector3(reference.Pose.Position.X, reference.Pose.Position.Y, reference.Pose.Position.Z);
                body.Rotation = new EngineQuaternion(reference.Pose.Orientation.X, reference.Pose.Orientation.Y, reference.Pose.Orientation.Z, reference.Pose.Orientation.W);
            }
        }
    }

    internal void UpdateBodyPose(PhysicsBody body)
    {
        if (_simulation == null || !IsSimulating)
            return;

        try
        {
            var position = ToNumerics(body.Position);
            var rotation = ToNumerics(body.Rotation);

            if (!ValidatePosition(position) || !ValidateQuaternion(rotation))
            {
                Logger.Warning("Invalid pose detected, skipping update");
                return;
            }

            if (_bodyHandles.TryGetValue(body, out var handle))
            {
                var reference = _simulation.Bodies.GetBodyReference(handle);
                reference.Pose.Position = position;
                reference.Pose.Orientation = rotation;
                if (body.IsKinematic)
                {
                    reference.Velocity.Linear = System.Numerics.Vector3.Zero;
                    reference.Velocity.Angular = System.Numerics.Vector3.Zero;
                }
            }
            else if (_staticHandles.TryGetValue(body, out var staticHandle))
            {
                var reference = _simulation.Statics.GetStaticReference(staticHandle);
                reference.Pose.Position = position;
                reference.Pose.Orientation = rotation;
                _simulation.Statics.UpdateBounds(staticHandle);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update body pose");
        }
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

    public void UpdateBodyMass(PhysicsBody body)
    {
        if (_simulation == null || !IsSimulating)
            return;

        try
        {
            bool needsReregister = false;
            
            if (_bodyHandles.TryGetValue(body, out var handle))
            {
                var reference = _simulation.Bodies.GetBodyReference(handle);
                bool wasKinematic = reference.Kinematic;
                
                if (body.Mass <= 0 || body.IsKinematic)
                {
                    if (!wasKinematic)
                    {
                        needsReregister = true;
                    }
                    else
                    {
                        reference.LocalInertia = default;
                        if (body.IsKinematic && !wasKinematic)
                        {
                            reference.BecomeKinematic();
                        }
                    }
                }
                else
                {
                    if (wasKinematic || _bodyShapeIndices.TryGetValue(body, out var shapeIndex))
                    {
                        if (wasKinematic)
                        {
                            needsReregister = true;
                        }
                        else if (_bodyShapeIndices.TryGetValue(body, out shapeIndex))
                        {
                            var inertia = ComputeInertia(body, shapeIndex);
                            reference.LocalInertia = inertia;
                        }
                    }
                }
            }
            else if (_staticHandles.ContainsKey(body))
            {
                if (body.Mass > 0 && !body.IsKinematic)
                {
                    needsReregister = true;
                }
            }
            
            if (needsReregister)
            {
                UnregisterBody(body);
                RegisterBody(body);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update body mass");
        }
    }

    public void Dispose()
    {
        StopSimulation();
        _registeredBodies.Clear();
        _bodyHandles.Clear();
        _handleToBody.Clear();
        _staticHandles.Clear();
        _staticHandleToBody.Clear();
        _bodyShapeIndices.Clear();
    }

    private static System.Numerics.Vector3 ToNumerics(EngineVector3 v)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            return System.Numerics.Vector3.Zero;
        return new System.Numerics.Vector3(v.X, v.Y, v.Z);
    }

    private static System.Numerics.Quaternion ToNumerics(EngineQuaternion q)
    {
        if (!float.IsFinite(q.X) || !float.IsFinite(q.Y) || !float.IsFinite(q.Z) || !float.IsFinite(q.W))
            return System.Numerics.Quaternion.Identity;

        var quat = new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);
        var lenSq = quat.X * quat.X + quat.Y * quat.Y + quat.Z * quat.Z + quat.W * quat.W;
        if (lenSq < 1e-6f)
            return System.Numerics.Quaternion.Identity;

        return System.Numerics.Quaternion.Normalize(quat);
    }

    private static bool ValidatePosition(System.Numerics.Vector3 position)
    {
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);
    }

    private static bool ValidateQuaternion(System.Numerics.Quaternion quaternion)
    {
        var lengthSquared = quaternion.X * quaternion.X + quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z + quaternion.W * quaternion.W;
        return float.IsFinite(lengthSquared) && lengthSquared > 1e-6f;
    }
}

internal struct SimpleNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    public void Initialize(Simulation simulation) { }
    public void Dispose() { }

    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
    }

    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, ref NonconvexContactManifold manifold)
    {
        return true;
    }

    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref NonconvexContactManifold manifold)
    {
        return true;
    }

    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial.FrictionCoefficient = 0.5f;
        pairMaterial.MaximumRecoveryVelocity = 10f;
        pairMaterial.SpringSettings = new SpringSettings(30, 0.1f);
        return true;
    }
}

internal struct SimplePoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    public System.Numerics.Vector3 Gravity;
    private Vector3Wide _gravityWideDt;

    public SimplePoseIntegratorCallbacks(System.Numerics.Vector3 gravity)
    {
        Gravity = gravity;
        _gravityWideDt = default;
    }

    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
    public bool AllowSubstepsForUnconstrainedBodies => false;
    public bool IntegrateVelocityForKinematics => false;

    public void Initialize(Simulation simulation) { }

    public void PrepareForIntegration(float dt)
    {
        _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
    }

    public void IntegrateVelocity(System.Numerics.Vector<int> bodyIndices, Vector3Wide positions, QuaternionWide orientations, BodyInertiaWide localInertias, System.Numerics.Vector<int> integrationMask, int workerIndex, System.Numerics.Vector<float> dt, ref BodyVelocityWide velocity)
    {
        Vector3Wide.Add(velocity.Linear, _gravityWideDt, out velocity.Linear);
    }
}
