using Engine.Math;

namespace Engine.Graphics;

public class Camera
{
    private float _fov = MathF.PI / 4.0f;
    private float _aspectRatio = 16.0f / 9.0f;
    private float _nearPlane = 0.1f;
    private float _farPlane = 1000.0f;
    private Vector3 _position = Vector3.Zero;
    private Quaternion _rotation = Quaternion.Identity;
    private Matrix4? _viewMatrix;
    private Matrix4? _projectionMatrix;
    private bool _dirty = true;

    public float FOV
    {
        get => _fov;
        set
        {
            if (_fov == value) return;
            _fov = value;
            _projectionMatrix = null;
        }
    }

    public float AspectRatio
    {
        get => _aspectRatio;
        set
        {
            if (_aspectRatio == value) return;
            _aspectRatio = value;
            _projectionMatrix = null;
        }
    }

    public float NearPlane
    {
        get => _nearPlane;
        set
        {
            if (_nearPlane == value) return;
            _nearPlane = value;
            _projectionMatrix = null;
        }
    }

    public float FarPlane
    {
        get => _farPlane;
        set
        {
            if (_farPlane == value) return;
            _farPlane = value;
            _projectionMatrix = null;
        }
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            _dirty = true;
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value) return;
            _rotation = value.Normalized();
            _dirty = true;
        }
    }

    public Vector3 Forward => (_rotation * Vector3.Forward).Normalized();
    public Vector3 Right => (_rotation * Vector3.Right).Normalized();
    public Vector3 Up => (_rotation * Vector3.Up).Normalized();

    public Matrix4 ViewMatrix
    {
        get
        {
            if (_viewMatrix == null || _dirty)
            {
                Vector3 target = _position + Forward;
                _viewMatrix = Matrix4.CreateLookAt(_position, target, Up);
                _dirty = false;
            }
            return _viewMatrix.Value;
        }
    }

    public Matrix4 ProjectionMatrix
    {
        get
        {
            if (_projectionMatrix == null)
            {
                _projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(_fov, _aspectRatio, _nearPlane, _farPlane);
            }
            return _projectionMatrix.Value;
        }
    }

    public Matrix4 ViewProjectionMatrix => ProjectionMatrix * ViewMatrix;

    public void LookAt(Vector3 target, Vector3 up)
    {
        Vector3 forward = (target - _position).Normalized();
        if (forward.LengthSquared < 0.0001f)
            return;

        Vector3 right = Vector3.Cross(up, forward).Normalized();
        Vector3 newUp = Vector3.Cross(forward, right).Normalized();

        Matrix4 lookAtMatrix = Matrix4.CreateLookAt(_position, target, up);
        Matrix4 rotationOnly = new Matrix4(
            lookAtMatrix.M11, lookAtMatrix.M12, lookAtMatrix.M13, 0,
            lookAtMatrix.M21, lookAtMatrix.M22, lookAtMatrix.M23, 0,
            lookAtMatrix.M31, lookAtMatrix.M32, lookAtMatrix.M33, 0,
            0, 0, 0, 1
        );
        
        _rotation = Quaternion.FromEulerAngles(rotationOnly.ToEulerAngles());
        _viewMatrix = null;
        _dirty = true;
    }
}

