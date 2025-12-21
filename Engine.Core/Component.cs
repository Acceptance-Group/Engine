using System;

namespace Engine.Core;

public abstract class Component
{
    public GameObject? GameObject { get; internal set; }
    public Transform? Transform => GameObject?.Transform;
    public bool Enabled { get; set; } = true;

    public T? GetComponent<T>() where T : Component
    {
        return GameObject?.GetComponent<T>();
    }

    public T[] GetComponents<T>() where T : Component
    {
        return GameObject?.GetComponents<T>() ?? Array.Empty<T>();
    }

    public T AddComponent<T>() where T : Component, new()
    {
        return GameObject?.AddComponent<T>() ?? throw new InvalidOperationException("Component is not attached to a GameObject");
    }

    public void RemoveComponent<T>() where T : Component
    {
        GameObject?.RemoveComponent<T>();
    }

    public virtual void RenderInspector()
    {
    }

    protected internal virtual void OnAttached() { }
    protected internal virtual void OnDetached() { }
    protected internal virtual void OnUpdate(float deltaTime) { }
    protected internal virtual void OnStart() { }
}

