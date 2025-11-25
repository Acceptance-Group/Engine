using Engine.Core;
using Engine.Renderer;

namespace Engine.Graphics;

public abstract class PostProcessingEffect : Disposable
{
    public bool Enabled { get; set; } = true;

    public abstract void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeEffect(disposing);
        }
    }

    protected virtual void DisposeEffect(bool disposing) { }
}

