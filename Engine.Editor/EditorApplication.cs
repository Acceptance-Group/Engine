using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Core;
using Engine.Content;
using Engine.Graphics;
using Engine.Math;
using Engine.Physics;
using Engine.Renderer;

namespace Engine.Editor;

public class EditorApplication : IDisposable
{
    private GameWindow? _window;
    private ImGuiController? _imguiController;
    private Scene? _currentScene;
    private Camera _camera;
    private FreeLookCameraController? _cameraController;
    private GameObject? _selectedObject;
    private ProjectManager _projectManager;
    private ConsoleWindow _console;
    private ProfilerWindow _profiler;
    private SceneHierarchyWindow _hierarchy;
    private InspectorWindow _inspector;
    private ViewportWindow _viewport;
    private SettingsWindow _settings;
    private MaterialEditorWindow _materialEditor;
    private FileBrowserWindow? _fileBrowser;
    private Shader? _defaultShader;
    private Skybox? _skybox;
    private ShadowRenderer? _shadowRenderer;
    private ShadowSettings _shadowSettings;
    private DirectionalLight _directionalLight;
    private PhysicsWorld? _physicsWorld;
    private AntiAliasingSettings _antiAliasingSettings;
    private FXAA? _fxaa;
    private SMAA? _smaa;
    private PostProcessingSettings _postProcessingSettings;
    private MotionBlur? _motionBlur;
    private Bloom? _bloom;
    private Vignette? _vignette;
    private bool _showPhysicsDebug = false;
    private bool _isPhysicsSimulating = false;
    private bool _isRunning = false;
    private bool _vSyncEnabled = true;
    private enum PlayMode { Stopped, Playing, Paused }
    private PlayMode _playMode = PlayMode.Stopped;
    private Dictionary<GameObject, (Engine.Math.Vector3 position, Engine.Math.Quaternion rotation)> _originalTransforms = new Dictionary<GameObject, (Engine.Math.Vector3, Engine.Math.Quaternion)>();

    public EditorApplication()
    {
        var nativeWindowSettings = NativeWindowSettings.Default;
        nativeWindowSettings.ClientSize = new OpenTK.Mathematics.Vector2i(1920, 1080);
        nativeWindowSettings.Title = "Engine";

        _window = new GameWindow(GameWindowSettings.Default, nativeWindowSettings);
        _window.Load += OnLoad;
        _window.UpdateFrame += OnUpdateFrame;
        _window.RenderFrame += OnRenderFrame;
        _window.Resize += OnResize;
        _window.Closing += (e) => OnClosing();
        _window.TextInput += OnTextInput;
        _window.MouseWheel += OnMouseWheel;

        _projectManager = new ProjectManager();
        _console = new ConsoleWindow();
        _profiler = new ProfilerWindow();
        _hierarchy = new SceneHierarchyWindow(this);
        _inspector = new InspectorWindow(this);
        _viewport = new ViewportWindow(this);
        _settings = new SettingsWindow(this);
        _materialEditor = new MaterialEditorWindow(this);
        
        _shadowSettings = new ShadowSettings();
        _directionalLight = new DirectionalLight();
        _antiAliasingSettings = new AntiAliasingSettings();
        _postProcessingSettings = new PostProcessingSettings();

        _camera = new Camera
        {
            Position = new Engine.Math.Vector3(0, 0, 5),
            FOV = MathF.PI / 4.0f,
            AspectRatio = 16.0f / 9.0f,
            NearPlane = 0.1f,
            FarPlane = 1000.0f
        };

        _cameraController = new FreeLookCameraController(_camera);

        Logger.OnLog += _console.AddLog;
    }

    private void OnLoad()
    {
        ApplyVSync();

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _imguiController = new ImGuiController(_window!.Size.X, _window.Size.Y);
        if (Engine.Core.System.IsMacOS())
        {
            _imguiController.FramebufferResized(_window.FramebufferSize.X, _window.FramebufferSize.Y);
        }
        _isRunning = true;

        Input.Initialize(_window);

        CreateDefaultShader();
        CreateDefaultScene();
        
        _skybox = new Skybox();
        _camera.LookAt(new Engine.Math.Vector3(0, 0, 0), Engine.Math.Vector3.Up);
        
        _shadowRenderer = new ShadowRenderer(_shadowSettings);
        
        if (_currentScene != null)
        {
            _physicsWorld = _currentScene.PhysicsWorld as PhysicsWorld;
            if (_physicsWorld == null)
            {
                _physicsWorld = new PhysicsWorld();
                _currentScene.PhysicsWorld = _physicsWorld;
            }
        }
        
        _fxaa = new FXAA(1920, 1080);
        _smaa = new SMAA(1920, 1080);
        _motionBlur = new MotionBlur(1920, 1080, _postProcessingSettings);
        _bloom = new Bloom(1920, 1080, _postProcessingSettings);
        _vignette = new Vignette(1920, 1080, _postProcessingSettings);
    }

    private void CreateDefaultShader()
    {
        string vertexShader = @"
#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 uMVP;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uLightSpaceMatrix;
uniform mat4 uLightSpaceMatrix0;
uniform mat4 uLightSpaceMatrix1;
uniform mat4 uLightSpaceMatrix2;
uniform mat4 uLightSpaceMatrix3;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;
out vec4 FragPosLightSpace;
out vec4 FragPosLightSpace0;
out vec4 FragPosLightSpace1;
out vec4 FragPosLightSpace2;
out vec4 FragPosLightSpace3;
out float FragDepth;

void main()
{
    FragPos = vec3(uModel * vec4(aPosition, 1.0));
    Normal = mat3(transpose(inverse(uModel))) * aNormal;
    TexCoord = aTexCoord;
    
    vec4 fragPosViewSpace = uView * vec4(FragPos, 1.0);
    FragDepth = -fragPosViewSpace.z;
    
    FragPosLightSpace = uLightSpaceMatrix * vec4(FragPos, 1.0);
    FragPosLightSpace0 = uLightSpaceMatrix0 * vec4(FragPos, 1.0);
    FragPosLightSpace1 = uLightSpaceMatrix1 * vec4(FragPos, 1.0);
    FragPosLightSpace2 = uLightSpaceMatrix2 * vec4(FragPos, 1.0);
    FragPosLightSpace3 = uLightSpaceMatrix3 * vec4(FragPos, 1.0);
    
    gl_Position = uMVP * vec4(aPosition, 1.0);
}";

        string fragmentShader = @"
#version 330 core
out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;
in vec4 FragPosLightSpace;
in vec4 FragPosLightSpace0;
in vec4 FragPosLightSpace1;
in vec4 FragPosLightSpace2;
in vec4 FragPosLightSpace3;
in float FragDepth;

uniform vec4 uColor;
uniform sampler2D uTexture;
uniform bool uUseTexture;
uniform vec2 uUVScale;
uniform vec2 uUVOffset;
uniform float uMetallic;
uniform float uSpecular;
uniform float uRoughness;
uniform sampler2D uShadowMap;
uniform sampler2D uShadowMap0;
uniform sampler2D uShadowMap1;
uniform sampler2D uShadowMap2;
uniform sampler2D uShadowMap3;
uniform mat4 uLightSpaceMatrix;
uniform mat4 uLightSpaceMatrix0;
uniform mat4 uLightSpaceMatrix1;
uniform mat4 uLightSpaceMatrix2;
uniform mat4 uLightSpaceMatrix3;
uniform float uShadowBias;
uniform float uNormalBias;
uniform float uShadowOpacity;
uniform bool uUseShadows;
uniform bool uUseCascadedShadows;
uniform int uCascadeCount;
uniform float uCascadeDepth0;
uniform float uCascadeDepth1;
uniform float uCascadeDepth2;
uniform float uCascadeDepth3;
uniform float uCascadeBlendArea;
uniform bool uSoftShadows;
uniform int uShadowQuality;

float SampleShadowMapHard(sampler2D shadowMap, vec2 coords, float currentDepth)
{
    if (coords.x < 0.0 || coords.x > 1.0 || coords.y < 0.0 || coords.y > 1.0)
        return 1.0;
    
    float pcfDepth = texture(shadowMap, coords).r;
    return currentDepth <= pcfDepth ? 1.0 : 0.0;
}

float SampleShadowMapPCF(sampler2D shadowMap, vec2 coords, float currentDepth, float texelSize)
{
    if (coords.x < 0.0 || coords.x > 1.0 || coords.y < 0.0 || coords.y > 1.0)
        return 1.0;
    
    float shadowSum = 0.0;
    float totalWeight = 0.0;
    
    float filterRadius;
    float penumbraSize;
    int sampleCount;
    
    if (uShadowQuality == 0)
    {
        filterRadius = texelSize * 2.0;
        penumbraSize = texelSize * 3.0;
        sampleCount = 4;
    }
    else if (uShadowQuality == 1)
    {
        filterRadius = texelSize * 3.0;
        penumbraSize = texelSize * 4.0;
        sampleCount = 9;
    }
    else if (uShadowQuality == 2)
    {
        filterRadius = texelSize * 4.0;
        penumbraSize = texelSize * 5.0;
        sampleCount = 16;
    }
    else if (uShadowQuality == 3)
        {
        filterRadius = texelSize * 5.0;
        penumbraSize = texelSize * 6.0;
        sampleCount = 25;
    }
    else
    {
        filterRadius = texelSize * 7.0;
        penumbraSize = texelSize * 8.0;
        sampleCount = 32;
    }
    
    float angle = fract(sin(dot(coords, vec2(12.9898, 78.233))) * 43758.5453) * 6.28318;
    float s = sin(angle);
    float c = cos(angle);
    mat2 rotationMatrix = mat2(c, -s, s, c);
    
    vec2 poissonDisk[32];
    poissonDisk[0] = vec2(-0.613392, 0.617481);
    poissonDisk[1] = vec2(0.170019, -0.040254);
    poissonDisk[2] = vec2(-0.299417, 0.791925);
    poissonDisk[3] = vec2(0.645680, 0.493210);
    poissonDisk[4] = vec2(-0.651784, 0.717887);
    poissonDisk[5] = vec2(0.421003, 0.027070);
    poissonDisk[6] = vec2(-0.817194, -0.271096);
    poissonDisk[7] = vec2(-0.705374, -0.668203);
    poissonDisk[8] = vec2(0.977050, -0.108615);
    poissonDisk[9] = vec2(0.063326, 0.142369);
    poissonDisk[10] = vec2(0.203528, 0.214331);
    poissonDisk[11] = vec2(-0.667531, 0.326090);
    poissonDisk[12] = vec2(-0.098422, -0.295755);
    poissonDisk[13] = vec2(-0.885922, 0.215369);
    poissonDisk[14] = vec2(0.566637, 0.605213);
    poissonDisk[15] = vec2(0.039766, -0.396100);
    poissonDisk[16] = vec2(0.308546, -0.891201);
    poissonDisk[17] = vec2(-0.093909, -0.890445);
    poissonDisk[18] = vec2(0.111921, -0.888637);
    poissonDisk[19] = vec2(0.508736, -0.578866);
    poissonDisk[20] = vec2(-0.428035, 0.494363);
    poissonDisk[21] = vec2(0.139783, 0.660326);
    poissonDisk[22] = vec2(-0.492372, -0.458685);
    poissonDisk[23] = vec2(0.827699, 0.117278);
    poissonDisk[24] = vec2(-0.139264, 0.320192);
    poissonDisk[25] = vec2(-0.324772, -0.698413);
    poissonDisk[26] = vec2(0.698413, 0.324772);
    poissonDisk[27] = vec2(-0.698413, 0.324772);
    poissonDisk[28] = vec2(0.324772, 0.698413);
    poissonDisk[29] = vec2(-0.324772, 0.698413);
    poissonDisk[30] = vec2(0.698413, -0.324772);
    poissonDisk[31] = vec2(0.324772, -0.698413);
    
    for (int i = 0; i < sampleCount; i++)
    {
        vec2 offset = rotationMatrix * poissonDisk[i];
        float dist = length(offset);
        
        if (dist > 1.0)
            continue;
        
        vec2 sampleOffset = offset * filterRadius;
        vec2 sampleCoord = coords + sampleOffset;
        
        float normalizedDist = dist;
        float weight = 1.0 - normalizedDist * normalizedDist;
        weight = max(weight, 0.0);
        
        if (sampleCoord.x >= 0.0 && sampleCoord.x <= 1.0 && sampleCoord.y >= 0.0 && sampleCoord.y <= 1.0)
        {
            float pcfDepth = texture(shadowMap, sampleCoord).r;
            float diff = currentDepth - pcfDepth;
            
            float shadowFactor = 1.0;
            if (diff > 0.0)
            {
                shadowFactor = 1.0 - smoothstep(0.0, penumbraSize, diff);
        }
            
            shadowSum += shadowFactor * weight;
            totalWeight += weight;
        }
        else
        {
            shadowSum += 1.0 * weight;
            totalWeight += weight;
        }
    }
    
    return totalWeight > 0.0 ? shadowSum / totalWeight : 1.0;
}

float CalculateShadowSingle(vec4 fragPosLightSpace, vec3 normal, vec3 lightDir)
{
    vec3 projCoords = fragPosLightSpace.xyz / fragPosLightSpace.w;
    
    vec3 normalOffset = normal * uNormalBias * 0.001;
    vec4 offsetFragPos = fragPosLightSpace + vec4(normalOffset, 0.0);
    vec3 offsetProjCoords = offsetFragPos.xyz / offsetFragPos.w;
    offsetProjCoords = offsetProjCoords * 0.5 + 0.5;
    
    if (offsetProjCoords.z > 1.0 || offsetProjCoords.x < 0.0 || offsetProjCoords.x > 1.0 || offsetProjCoords.y < 0.0 || offsetProjCoords.y > 1.0)
        return 1.0;
    
    float NdotL = dot(normal, lightDir);
    float minBias = 0.001;
    float slopeBias = 0.003 * tan(acos(clamp(NdotL, 0.0, 1.0)));
    slopeBias = clamp(slopeBias, 0.0, 0.015);
    float bias = max(max(slopeBias, uShadowBias), minBias);
    
    float texelSize = 1.0 / float(textureSize(uShadowMap, 0).x);
    bias += texelSize * 1.5;
    
    float currentDepth = offsetProjCoords.z - bias;
    
    if (!uSoftShadows)
    {
        return SampleShadowMapHard(uShadowMap, offsetProjCoords.xy, currentDepth);
    }
    
    return SampleShadowMapPCF(uShadowMap, offsetProjCoords.xy, currentDepth, texelSize);
}

float CalculateShadowCascaded(vec3 normal, vec3 lightDir)
{
    float NdotL = dot(normal, lightDir);
    float minBias = 0.001;
    float slopeBias = 0.003 * tan(acos(clamp(NdotL, 0.0, 1.0)));
    slopeBias = clamp(slopeBias, 0.0, 0.015);
    float bias = max(max(slopeBias, uShadowBias), minBias);
    
    float viewDepth = FragDepth;
    
    if (viewDepth < 0.0)
        return 1.0;
    
    vec3 normalOffset = normal * uNormalBias * 0.0015;
    vec4 fragPosLightSpace;
    float texelSize;
    float shadow = 1.0;
    
    if (viewDepth < uCascadeDepth0)
    {
        fragPosLightSpace = FragPosLightSpace0;
        vec4 offsetFragPos = fragPosLightSpace + vec4(normalOffset, 0.0);
        vec3 projCoords = offsetFragPos.xyz / offsetFragPos.w;
        projCoords = projCoords * 0.5 + 0.5;
        
        if (projCoords.z <= 1.0 && projCoords.x >= 0.0 && projCoords.x <= 1.0 && projCoords.y >= 0.0 && projCoords.y <= 1.0)
        {
            texelSize = 1.0 / float(textureSize(uShadowMap0, 0).x);
            float cascadeBias = bias + texelSize * 1.5;
            float currentDepth = projCoords.z - cascadeBias;
            if (!uSoftShadows)
            {
                shadow = SampleShadowMapHard(uShadowMap0, projCoords.xy, currentDepth);
            }
            else
            {
                shadow = SampleShadowMapPCF(uShadowMap0, projCoords.xy, currentDepth, texelSize);
            }
        }
    }
    else if (uCascadeCount > 1 && viewDepth < uCascadeDepth1)
    {
        fragPosLightSpace = FragPosLightSpace1;
        vec4 offsetFragPos = fragPosLightSpace + vec4(normalOffset, 0.0);
        vec3 projCoords = offsetFragPos.xyz / offsetFragPos.w;
        projCoords = projCoords * 0.5 + 0.5;
        
        if (projCoords.z <= 1.0 && projCoords.x >= 0.0 && projCoords.x <= 1.0 && projCoords.y >= 0.0 && projCoords.y <= 1.0)
        {
            texelSize = 1.0 / float(textureSize(uShadowMap1, 0).x);
            float cascadeBias = bias + texelSize * 1.0;
            float currentDepth = projCoords.z - cascadeBias;
            if (!uSoftShadows)
            {
                shadow = SampleShadowMapHard(uShadowMap1, projCoords.xy, currentDepth);
                }
                else
                {
                shadow = SampleShadowMapPCF(uShadowMap1, projCoords.xy, currentDepth, texelSize);
            }
        }
    }
    else if (uCascadeCount > 2 && viewDepth < uCascadeDepth2)
    {
        fragPosLightSpace = FragPosLightSpace2;
        vec4 offsetFragPos = fragPosLightSpace + vec4(normalOffset, 0.0);
        vec3 projCoords = offsetFragPos.xyz / offsetFragPos.w;
        projCoords = projCoords * 0.5 + 0.5;
        
        if (projCoords.z <= 1.0 && projCoords.x >= 0.0 && projCoords.x <= 1.0 && projCoords.y >= 0.0 && projCoords.y <= 1.0)
        {
            texelSize = 1.0 / float(textureSize(uShadowMap2, 0).x);
            float cascadeBias = bias + texelSize * 1.0;
            float currentDepth = projCoords.z - cascadeBias;
            if (!uSoftShadows)
            {
                shadow = SampleShadowMapHard(uShadowMap2, projCoords.xy, currentDepth);
                }
                else
                {
                shadow = SampleShadowMapPCF(uShadowMap2, projCoords.xy, currentDepth, texelSize);
            }
                }
            }
    else if (uCascadeCount > 3 && viewDepth < uCascadeDepth3)
                {
        fragPosLightSpace = FragPosLightSpace3;
        vec4 offsetFragPos = fragPosLightSpace + vec4(normalOffset, 0.0);
        vec3 projCoords = offsetFragPos.xyz / offsetFragPos.w;
        projCoords = projCoords * 0.5 + 0.5;
        
        if (projCoords.z <= 1.0 && projCoords.x >= 0.0 && projCoords.x <= 1.0 && projCoords.y >= 0.0 && projCoords.y <= 1.0)
        {
            texelSize = 1.0 / float(textureSize(uShadowMap3, 0).x);
            float cascadeBias = bias + texelSize * 1.0;
            float currentDepth = projCoords.z - cascadeBias;
            if (!uSoftShadows)
            {
                shadow = SampleShadowMapHard(uShadowMap3, projCoords.xy, currentDepth);
                }
                else
                {
                shadow = SampleShadowMapPCF(uShadowMap3, projCoords.xy, currentDepth, texelSize);
            }
        }
    }
    
    return shadow;
}

float ShadowCalculation(vec4 fragPosLightSpace, vec3 normal)
{
    if (!uUseShadows)
        return 1.0;
    
    vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));
    
    float shadow = 1.0;
    if (!uUseCascadedShadows)
    {
        shadow = CalculateShadowSingle(fragPosLightSpace, normal, lightDir);
    }
    else
    {
        shadow = CalculateShadowCascaded(normal, lightDir);
    }
    
    return mix(1.0, shadow, uShadowOpacity);
}

void main()
{
    vec4 color = uColor;
    if (uUseTexture)
    {
        vec2 transformedUV = (TexCoord / uUVScale) + uUVOffset;
        color = texture(uTexture, transformedUV) * uColor;
    }
    
    vec3 lightDir = normalize(vec3(1.0, 1.0, 1.0));
    vec3 viewDir = normalize(-FragPos);
    vec3 normal = normalize(Normal);
    
    float NdotL = max(dot(normal, lightDir), 0.0);
    float diff = mix(0.2, 1.0, NdotL);
    
    vec3 reflectDir = reflect(-lightDir, normal);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), (1.0 - uRoughness) * 128.0 + 8.0);
    spec *= uSpecular;
    
    vec3 albedo = color.rgb * (1.0 - uMetallic);
    vec3 metallic = color.rgb * uMetallic;
    
    color.rgb = albedo * diff + metallic * spec * uSpecular;
    
    if (uUseShadows)
    {
        vec4 fragPosLightSpace = uLightSpaceMatrix * vec4(FragPos, 1.0);
        float shadow = ShadowCalculation(fragPosLightSpace, normal);
        color.rgb *= shadow;
    }
    
    FragColor = color;
}";

        _defaultShader = new Shader(vertexShader, fragmentShader);
        _defaultShader.SetVector2("uUVScale", Engine.Math.Vector2.One);
        _defaultShader.SetVector2("uUVOffset", Engine.Math.Vector2.Zero);
    }

    private void CreateDefaultScene()
    {
        _currentScene = new Scene("Default Scene");
        if (_currentScene.PhysicsWorld == null)
        {
            _physicsWorld = new PhysicsWorld();
            _currentScene.PhysicsWorld = _physicsWorld;
        }
        else
        {
            _physicsWorld = _currentScene.PhysicsWorld as PhysicsWorld;
        }

        CreateGround();

        for (int i = 0; i < 4; i++)
        {
            var cube = _currentScene.CreateGameObject($"Cube {i + 1}");
            var meshRenderer = cube.AddComponent<MeshRenderer>();
            meshRenderer.Mesh = Primitives.CreateCube();
            meshRenderer.SetDefaultShader(_defaultShader!);
            cube.Transform.Position = new Engine.Math.Vector3(-3 + i * 2, 0.5f + i * 1.1f, 0);
            
            var cubePhysics = cube.AddComponent<Rigidbody>();
            cubePhysics.Mass = 1.0f;
            cubePhysics.IsKinematic = false;
            
            var cubeCollider = cube.AddComponent<BoxCollider>();
            cubeCollider.Size = new Engine.Math.Vector3(1.0f, 1.0f, 1.0f);
        }

        for (int i = 0; i < 6; i++)
        {
            var sphere = _currentScene.CreateGameObject($"Sphere {i + 1}");
            var sphereRenderer = sphere.AddComponent<MeshRenderer>();
            sphereRenderer.Mesh = Primitives.CreateSphere();
            sphereRenderer.SetDefaultShader(_defaultShader!);
            sphere.Transform.Position = new Engine.Math.Vector3(-4 + i * 1.5f, 5 + i * 0.5f, -2 + i * 0.3f);
            
            var spherePhysics = sphere.AddComponent<Rigidbody>();
            spherePhysics.Mass = 0.5f + i * 0.2f;
            spherePhysics.IsKinematic = false;
            
            var sphereCollider = sphere.AddComponent<SphereCollider>();
            sphereCollider.Radius = 0.5f;
        }

        for (int i = 0; i < 3; i++)
        {
            var heavyCube = _currentScene.CreateGameObject($"Heavy Cube {i + 1}");
            var meshRenderer = heavyCube.AddComponent<MeshRenderer>();
            meshRenderer.Mesh = Primitives.CreateCube();
            meshRenderer.SetDefaultShader(_defaultShader!);
            heavyCube.Transform.Position = new Engine.Math.Vector3(4, 2 + i * 1.2f, -1);
            heavyCube.Transform.Scale = new Engine.Math.Vector3(1.2f, 1.2f, 1.2f);
            
            var heavyPhysics = heavyCube.AddComponent<Rigidbody>();
            heavyPhysics.Mass = 5.0f;
            heavyPhysics.IsKinematic = false;
            
            var heavyCollider = heavyCube.AddComponent<BoxCollider>();
            heavyCollider.Size = new Engine.Math.Vector3(1.2f, 1.2f, 1.2f);
        }
    }

    private void CreateGround()
    {
        if (_currentScene == null)
            return;

        var ground = _currentScene.CreateGameObject("Ground");
        ground.Transform.Position = new Engine.Math.Vector3(0, -0.5f, 0);
        ground.Transform.Scale = new Engine.Math.Vector3(20, 1, 20);

        var groundRenderer = ground.AddComponent<MeshRenderer>();
        groundRenderer.Mesh = Primitives.CreateCube();
        groundRenderer.SetDefaultShader(_defaultShader!);

        var groundPhysics = ground.AddComponent<Rigidbody>();
        groundPhysics.IsKinematic = true;
        groundPhysics.Mass = 0;
        
        var groundCollider = ground.AddComponent<BoxCollider>();
        groundCollider.Size = new Engine.Math.Vector3(20, 1, 20);
    }

    private void OnUpdateFrame(FrameEventArgs e)
    {
        if (!_isRunning || _window == null)
            return;

        _profiler.BeginFrame();
        Input.Update();

        if (_window.KeyboardState.IsKeyPressed(Keys.Escape))
            _window.Close();

        bool viewportHovered = _viewport != null && _viewport.IsHovered;
        bool isCapturing = _cameraController != null && _cameraController.IsCapturing;
        bool canControlCamera = viewportHovered || isCapturing;
        _cameraController?.Update(_window, (float)e.Time, canControlCamera);

        if (_playMode == PlayMode.Playing)
        {
            if (_physicsWorld != null)
            {
                _physicsWorld.Update((float)e.Time);
            }

        if (_currentScene != null)
        {
            foreach (var gameObject in _currentScene.GameObjects)
            {
                gameObject.Update((float)e.Time);
                }
            }
        }

        _profiler.EndFrame((float)e.Time);
    }

    private void OnRenderFrame(FrameEventArgs e)
    {
        if (!_isRunning || _window == null || _imguiController == null)
            return;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (Engine.Core.System.IsMacOS())
        {
            GL.Viewport(0, 0, _window.FramebufferSize.X, _window.FramebufferSize.Y);
        }
        else
        {
            GL.Viewport(0, 0, _window.Size.X, _window.Size.Y);
        }
        
        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _imguiController.Update(_window, (float)e.Time);

        RenderMainMenuBar();
        RenderDockSpace();
        _hierarchy.Render();
        _inspector.Render();
        _viewport.Render();
        _console.Render();
        _profiler.Render();
        _settings.Render();
        _materialEditor.Render();
        
        if (_fileBrowser != null && _fileBrowser.Show())
        {
            OnModelFileSelected();
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (Engine.Core.System.IsMacOS())
        {
            GL.Viewport(0, 0, _window.FramebufferSize.X, _window.FramebufferSize.Y);
        }
        else
        {
            GL.Viewport(0, 0, _window.Size.X, _window.Size.Y);
        }
        _imguiController.Render();

        _window.SwapBuffers();
    }

    private void RenderDockSpace()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.SetNextWindowViewport(viewport.ID);

        ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDocking;
        windowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, System.Numerics.Vector2.Zero);

        if (ImGui.Begin("DockSpace", windowFlags))
        {
            ImGui.PopStyleVar(3);
            
            var dockSpaceId = ImGui.GetID("DockSpace");
            ImGui.DockSpace(dockSpaceId, System.Numerics.Vector2.Zero);
        }
        ImGui.End();
    }

    private void RenderMainMenuBar()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Project"))
                {
                    _projectManager.NewProject();
                }
                if (ImGui.MenuItem("Open Project"))
                {
                    _projectManager.OpenProject();
                }
                if (ImGui.MenuItem("Save Project"))
                {
                    _projectManager.SaveProject();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("New Scene"))
                {
                    _currentScene = new Scene("New Scene");
                    _physicsWorld = _currentScene.PhysicsWorld as PhysicsWorld;
                    if (_physicsWorld == null)
                    {
                        _physicsWorld = new PhysicsWorld();
                        _currentScene.PhysicsWorld = _physicsWorld;
                    }
                    _selectedObject = null;
                }
                if (ImGui.MenuItem("Save Scene"))
                {
                    SaveScene();
                }
                if (ImGui.MenuItem("Load Scene"))
                {
                    LoadScene();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Exit"))
                {
                    _window?.Close();
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("GameObject"))
            {
                if (ImGui.MenuItem("Create Empty"))
                {
                    _currentScene?.CreateGameObject("GameObject");
                }
                if (ImGui.MenuItem("Cube"))
                {
                    CreatePrimitive("Cube", () => Primitives.CreateCube());
                }
                if (ImGui.MenuItem("Sphere"))
                {
                    CreatePrimitive("Sphere", () => Primitives.CreateSphere());
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Window"))
            {
                if (ImGui.MenuItem("Console", "", _console.Visible))
                {
                    _console.Visible = !_console.Visible;
                }
                if (ImGui.MenuItem("Profiler", "", _profiler.Visible))
                {
                    _profiler.Visible = !_profiler.Visible;
                }
                if (ImGui.MenuItem("Settings", "", _settings.IsVisible))
                {
                    _settings.IsVisible = !_settings.IsVisible;
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Tools"))
            {
                if (ImGui.MenuItem("Material Editor", "", _materialEditor.Visible))
                {
                    _materialEditor.Visible = !_materialEditor.Visible;
                }
                if (ImGui.MenuItem("Model Loader"))
                {
                    LoadModel();
                }
                ImGui.EndMenu();
            }

            ImGui.Separator();

            bool isPlaying = _playMode == PlayMode.Playing;
            bool isPaused = _playMode == PlayMode.Paused;
            bool isStopped = _playMode == PlayMode.Stopped;

            if (isPlaying)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.7f, 0.2f, 1.0f));
            }
            if (ImGui.Button("▶ Play", new System.Numerics.Vector2(80, 0)))
            {
                OnPlay();
            }
            if (isPlaying)
            {
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();

            if (isPaused)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.7f, 0.7f, 0.2f, 1.0f));
            }
            if (ImGui.Button("⏸ Pause", new System.Numerics.Vector2(80, 0)))
            {
                OnPause();
            }
            if (isPaused)
            {
                ImGui.PopStyleColor();
            }

            ImGui.SameLine();

            if (ImGui.Button("⏹ Stop", new System.Numerics.Vector2(80, 0)))
            {
                OnStop();
            }

            ImGui.EndMainMenuBar();
        }
    }

    private void CreatePrimitive(string name, Func<Engine.Graphics.Mesh> meshFactory)
    {
        if (_currentScene == null)
            return;

        var go = _currentScene.CreateGameObject(name);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.Mesh = meshFactory();
        renderer.SetDefaultShader(_defaultShader!);
    }

    private void SaveScene()
    {
        if (_currentScene == null)
            return;

        var serializer = new SceneSerializer();
        serializer.Save(_currentScene, Path.Combine(_projectManager.CurrentProjectPath ?? ".", "Scenes", $"{_currentScene.Name}.scene"));
    }

    private void LoadScene()
    {
        var serializer = new SceneSerializer();
        var scenePath = Path.Combine(_projectManager.CurrentProjectPath ?? ".", "Scenes");
        if (Directory.Exists(scenePath))
        {
            var files = Directory.GetFiles(scenePath, "*.scene");
            if (files.Length > 0)
            {
                _currentScene = serializer.Load(files[0]);
                _physicsWorld = _currentScene.PhysicsWorld as PhysicsWorld;
                if (_physicsWorld == null)
                {
                    _physicsWorld = new PhysicsWorld();
                    _currentScene.PhysicsWorld = _physicsWorld;
                }
                _selectedObject = null;
            }
        }
    }

    private void LoadModel()
    {
        if (_currentScene == null)
        {
            Logger.Warning("No scene loaded. Please create or load a scene first.");
            return;
        }

        var extensions = new[] { "obj", "fbx", "dae", "3ds", "blend", "x", "md2", "md3", "ply", "stl", "gltf", "glb" };
        _fileBrowser = new FileBrowserWindow("Load 3D Model", extensions);
        _fileBrowser.Visible = true;
        _fileBrowser.Reset();
    }

    private void OnModelFileSelected()
    {
        if (_fileBrowser == null || _fileBrowser.SelectedFilePath == null || _currentScene == null)
            return;

        try
        {
            var mesh = ModelLoader.LoadMesh(_fileBrowser.SelectedFilePath);
            if (mesh != null)
            {
                var fileName = Path.GetFileNameWithoutExtension(_fileBrowser.SelectedFilePath);
                var gameObject = _currentScene.CreateGameObject(fileName);
                var renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.Mesh = mesh;
                renderer.SetDefaultShader(_defaultShader!);
                
                Logger.Info($"Successfully loaded model: {fileName}");
            }
            else
            {
                Logger.Error($"Failed to load model from: {_fileBrowser.SelectedFilePath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading model: {ex.Message}");
        }
        finally
        {
            _fileBrowser = null;
        }
    }

    private void OnResize(ResizeEventArgs e)
    {
        if (_window != null)
        {
            if (Engine.Core.System.IsMacOS())
            {
                GL.Viewport(0, 0, _window.FramebufferSize.X, _window.FramebufferSize.Y);
                _imguiController?.WindowResized(e.Width, e.Height);
                _imguiController?.FramebufferResized(_window.FramebufferSize.X, _window.FramebufferSize.Y);
            }
            else
            {
                GL.Viewport(0, 0, e.Width, e.Height);
                _imguiController?.WindowResized(e.Width, e.Height);
            }
        }
        _camera.AspectRatio = (float)e.Width / e.Height;
    }

    private void OnClosing()
    {
        _isRunning = false;
    }

    private void OnTextInput(TextInputEventArgs e)
    {
        if (e.Unicode > 0 && e.Unicode < 0x10000)
        {
            _imguiController?.PressChar((char)e.Unicode);
        }
    }

    private void OnMouseWheel(MouseWheelEventArgs e)
    {
        _imguiController?.MouseScroll(new OpenTK.Mathematics.Vector2((float)e.OffsetX, (float)e.OffsetY));
    }

    public void Run()
    {
        _window?.Run();
    }

    public Scene? CurrentScene => _currentScene;
    public GameObject? SelectedObject
    {
        get => _selectedObject;
        set => _selectedObject = value;
    }

    public Camera Camera => _camera;
    public Shader? DefaultShader => _defaultShader;
    public ProjectManager ProjectManager => _projectManager;
    public Skybox? Skybox => _skybox;
    public ShadowRenderer? ShadowRenderer => _shadowRenderer;
    public ShadowSettings ShadowSettings => _shadowSettings;
    public DirectionalLight DirectionalLight => _directionalLight;
    public PhysicsWorld? PhysicsWorld => _physicsWorld;
    public AntiAliasingSettings AntiAliasingSettings => _antiAliasingSettings;
    public PostProcessingSettings PostProcessingSettings => _postProcessingSettings;
    public FXAA? FXAA => _fxaa;
    public SMAA? SMAA => _smaa;
    public MotionBlur? MotionBlur => _motionBlur;
    public Bloom? Bloom => _bloom;
    public Vignette? Vignette => _vignette;
    public bool ShowPhysicsDebug
    {
        get => _showPhysicsDebug;
        set => _showPhysicsDebug = value;
    }
    
    public bool IsPhysicsSimulating
    {
        get => _isPhysicsSimulating;
        set => _isPhysicsSimulating = value;
    }
    
    private void OnPlay()
    {
        if (_playMode == PlayMode.Playing)
            return;

        if (_playMode == PlayMode.Stopped)
        {
            StoreOriginalTransforms();
            SyncPhysicsTransforms();
        }

        _playMode = PlayMode.Playing;
        _isPhysicsSimulating = true;

        if (_physicsWorld != null)
        {
            _physicsWorld.StartSimulation();
        }

        if (_currentScene != null)
        {
            _currentScene.Start();
        }
    }

    private void OnPause()
    {
        if (_playMode != PlayMode.Playing)
            return;

        _playMode = PlayMode.Paused;
        _isPhysicsSimulating = false;

        if (_physicsWorld != null)
        {
            _physicsWorld.StopSimulation();
        }
    }

    private void OnStop()
    {
        if (_playMode == PlayMode.Stopped)
            return;

        _playMode = PlayMode.Stopped;
        _isPhysicsSimulating = false;

        if (_physicsWorld != null)
        {
            _physicsWorld.StopSimulation();
        }

        RestoreOriginalTransforms();
    }

    private void StoreOriginalTransforms()
    {
        _originalTransforms.Clear();
        
        if (_currentScene == null)
            return;

        foreach (var gameObject in _currentScene.GameObjects)
        {
            var physics = gameObject.GetComponent<Rigidbody>();
            if (physics != null)
            {
                _originalTransforms[gameObject] = (gameObject.Transform.Position, gameObject.Transform.Rotation);
            }
        }
    }

    private void RestoreOriginalTransforms()
    {
        if (_currentScene == null)
            return;

        foreach (var kvp in _originalTransforms)
        {
            var gameObject = kvp.Key;
            var (originalPosition, originalRotation) = kvp.Value;

            gameObject.Transform.Position = originalPosition;
            gameObject.Transform.Rotation = originalRotation;

            var physics = gameObject.GetComponent<Rigidbody>();
            if (physics?.Body != null)
            {
                physics.Body.ResetToOriginal();
            }
        }

        _originalTransforms.Clear();
        SyncPhysicsTransforms();
    }

    private void SyncPhysicsTransforms()
    {
        if (_currentScene == null)
            return;

        var physicsComponents = _currentScene.FindObjectsOfType<Rigidbody>();
        foreach (var component in physicsComponents)
        {
            component.GameObject?.GetComponent<MeshCollider>()?.RefreshSize(force: true);
            component.SyncWithTransform();
        }
    }
    
    public void ResetPhysics()
    {
        if (_currentScene == null)
            return;
            
        foreach (var gameObject in _currentScene.GameObjects)
        {
            var physics = gameObject.GetComponent<Rigidbody>();
            if (physics?.Body != null)
            {
                if (gameObject.Name == "Cube")
                {
                    gameObject.Transform.Position = new Engine.Math.Vector3(-2, 2, 0);
                    physics.Body.Position = gameObject.Transform.Position;
                    physics.Body.Velocity = Engine.Math.Vector3.Zero;
                }
                else if (gameObject.Name == "Sphere")
                {
                    gameObject.Transform.Position = new Engine.Math.Vector3(2, 2, 0);
                    physics.Body.Position = gameObject.Transform.Position;
                    physics.Body.Velocity = Engine.Math.Vector3.Zero;
                }
            }
        }
    }
    
    internal GameWindow? Window => _window;

    public bool VSyncEnabled
    {
        get => _vSyncEnabled;
        set
        {
            if (_vSyncEnabled != value)
            {
                _vSyncEnabled = value;
                ApplyVSync();
            }
        }
    }

    private void ApplyVSync()
    {
        if (_window != null)
            _window.VSync = _vSyncEnabled ? VSyncMode.On : VSyncMode.Off;
    }

    public void Dispose()
    {
        _defaultShader?.Dispose();
        _skybox?.Dispose();
        _shadowRenderer?.Dispose();
        _fxaa?.Dispose();
        _smaa?.Dispose();
        _motionBlur?.Dispose();
        _bloom?.Dispose();
        _vignette?.Dispose();
        _imguiController?.Dispose();
        _window?.Dispose();
    }
}

