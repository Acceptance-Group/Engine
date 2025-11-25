using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Graphics;
using Engine.Math;

namespace Engine.Editor;

public class FreeLookCameraController
{
    private Camera _camera;
    private float _yaw = -90.0f;
    private float _pitch = 0.0f;
    private float _movementSpeed = 5.0f;
    private float _rotationSensitivity = 0.1f;
    private bool _firstMouse = true;
    private float _lastMouseX = 0.0f;
    private float _lastMouseY = 0.0f;
    private bool _mouseCaptured = false;

    public float MovementSpeed
    {
        get => _movementSpeed;
        set => _movementSpeed = MathF.Max(0.1f, value);
    }

    public float RotationSensitivity
    {
        get => _rotationSensitivity;
        set => _rotationSensitivity = MathF.Max(0.01f, value);
    }

    public FreeLookCameraController(Camera camera)
    {
        _camera = camera;
    }

    public void Update(GameWindow window, float deltaTime)
    {
        if (!_mouseCaptured && window.MouseState.IsButtonPressed(MouseButton.Right))
        {
            _mouseCaptured = true;
            window.CursorState = CursorState.Grabbed;
            _firstMouse = true;
        }

        if (_mouseCaptured && window.MouseState.IsButtonReleased(MouseButton.Right))
        {
            _mouseCaptured = false;
            window.CursorState = CursorState.Normal;
        }

        if (_mouseCaptured)
        {
            ProcessMouseMovement(window);
            ProcessKeyboardInput(window, deltaTime);
        }
    }

    private void ProcessMouseMovement(GameWindow window)
    {
        var mouseState = window.MouseState;

        if (_firstMouse)
        {
            _lastMouseX = mouseState.X;
            _lastMouseY = mouseState.Y;
            _firstMouse = false;
        }

        float xOffset = _lastMouseX - mouseState.X;
        float yOffset = _lastMouseY - mouseState.Y;

        _lastMouseX = mouseState.X;
        _lastMouseY = mouseState.Y;

        xOffset *= _rotationSensitivity;
        yOffset *= _rotationSensitivity;

        _yaw += xOffset;
        _pitch += yOffset;

        if (_pitch > 89.0f)
            _pitch = 89.0f;
        if (_pitch < -89.0f)
            _pitch = -89.0f;

        float yawRad = _yaw * MathF.PI / 180.0f;
        float pitchRad = _pitch * MathF.PI / 180.0f;
        
        Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.Up, yawRad);
        Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.Right, pitchRad);
        _camera.Rotation = yawRotation * pitchRotation;
    }

    private void ProcessKeyboardInput(GameWindow window, float deltaTime)
    {
        var keyboardState = window.KeyboardState;
        float velocity = _movementSpeed * deltaTime;

        Vector3 position = _camera.Position;
        Vector3 forward = _camera.Forward;
        Vector3 right = _camera.Right;
        Vector3 up = _camera.Up;

        if (keyboardState.IsKeyDown(Keys.W))
            position += forward * velocity;
        if (keyboardState.IsKeyDown(Keys.S))
            position -= forward * velocity;
        if (keyboardState.IsKeyDown(Keys.A))
            position -= right * velocity;
        if (keyboardState.IsKeyDown(Keys.D))
            position += right * velocity;
        if (keyboardState.IsKeyDown(Keys.Q))
            position -= up * velocity;
        if (keyboardState.IsKeyDown(Keys.E))
            position += up * velocity;

        _camera.Position = position;
    }
}

