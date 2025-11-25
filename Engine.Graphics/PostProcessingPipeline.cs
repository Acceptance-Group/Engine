using System.Collections.Generic;
using Engine.Core;

namespace Engine.Graphics;

public class PostProcessingPipeline : Disposable
{
    private readonly List<PostProcessingEffect> _effects = new List<PostProcessingEffect>();

    public void AddEffect(PostProcessingEffect effect)
    {
        _effects.Add(effect);
    }

    public void RemoveEffect(PostProcessingEffect effect)
    {
        _effects.Remove(effect);
    }

    public void Apply(uint sourceTexture, uint targetFramebuffer, int width, int height)
    {
        uint currentTexture = sourceTexture;
        uint currentFBO = targetFramebuffer;

        foreach (var effect in _effects)
        {
            if (effect.Enabled)
            {
                effect.Apply(currentTexture, currentFBO, width, height);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var effect in _effects)
            {
                effect.Dispose();
            }
            _effects.Clear();
        }
    }
}

