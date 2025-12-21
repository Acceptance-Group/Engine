using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using Engine.Core;

namespace Engine.Renderer;

public class Shader : Disposable
{
    private readonly int _programID;
    private readonly Dictionary<string, int> _uniformLocations = new Dictionary<string, int>();

    public int ProgramID => _programID;

    public Shader(string vertexSource, string fragmentSource)
    {
        int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _programID = GL.CreateProgram();
        GL.AttachShader(_programID, vertexShader);
        GL.AttachShader(_programID, fragmentShader);
        GL.LinkProgram(_programID);

        GL.GetProgram(_programID, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(_programID);
            GL.DeleteProgram(_programID);
            throw new Exception($"Shader program linking failed: {infoLog}");
        }

        GL.DetachShader(_programID, vertexShader);
        GL.DetachShader(_programID, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        CacheUniformLocations();
    }

    public Shader(string computeSource)
    {
        int computeShader = CompileShader(ShaderType.ComputeShader, computeSource);

        _programID = GL.CreateProgram();
        GL.AttachShader(_programID, computeShader);
        GL.LinkProgram(_programID);

        GL.GetProgram(_programID, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(_programID);
            GL.DeleteProgram(_programID);
            throw new Exception($"Compute shader program linking failed: {infoLog}");
        }

        GL.DetachShader(_programID, computeShader);
        GL.DeleteShader(computeShader);

        CacheUniformLocations();
    }

    private int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new Exception($"Shader compilation failed ({type}): {infoLog}");
        }

        return shader;
    }

    private void CacheUniformLocations()
    {
        GL.GetProgram(_programID, GetProgramParameterName.ActiveUniforms, out int uniformCount);
        for (int i = 0; i < uniformCount; i++)
        {
            string name = GL.GetActiveUniform(_programID, i, out _, out _);
            int location = GL.GetUniformLocation(_programID, name);
            _uniformLocations[name] = location;
        }
    }

    public void Use()
    {
        GL.UseProgram(_programID);
    }

    public int GetUniformLocation(string name)
    {
        if (_uniformLocations.TryGetValue(name, out int location))
            return location;
        return GL.GetUniformLocation(_programID, name);
    }

    public void SetInt(string name, int value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
            GL.Uniform1(location, value);
    }

    public void SetFloat(string name, float value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
            GL.Uniform1(location, value);
    }

    public void SetVector2(string name, Engine.Math.Vector2 value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
            GL.Uniform2(location, value.X, value.Y);
    }

    public void SetVector3(string name, Engine.Math.Vector3 value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
            GL.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetVector4(string name, Engine.Math.Vector4 value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
            GL.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    public void SetMatrix4(string name, Engine.Math.Matrix4 value)
    {
        int location = GetUniformLocation(name);
        if (location >= 0)
        {
            unsafe
            {
                float* matrixPtr = stackalloc float[16];
                matrixPtr[0] = value.M11; matrixPtr[1] = value.M21; matrixPtr[2] = value.M31; matrixPtr[3] = value.M41;
                matrixPtr[4] = value.M12; matrixPtr[5] = value.M22; matrixPtr[6] = value.M32; matrixPtr[7] = value.M42;
                matrixPtr[8] = value.M13; matrixPtr[9] = value.M23; matrixPtr[10] = value.M33; matrixPtr[11] = value.M43;
                matrixPtr[12] = value.M14; matrixPtr[13] = value.M24; matrixPtr[14] = value.M34; matrixPtr[15] = value.M44;
                GL.UniformMatrix4(location, 1, false, matrixPtr);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GL.DeleteProgram(_programID);
        }
    }
}

