using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Visuals;

// Each FS post shader under an id of its own, so it replaces or removes only itself. The old single-shader setter
// cleared every post shader on the sprite, the hover outline, stealth and floor occlusion included.
public static class FSPostShaders
{
    public const string ZombieGlow = "fs-zombie-glow";
    public const string RevenantGlow = "fs-revenant-glow";
    public const string RevenantMark = "fs-revenant-mark";
    public const string PlayerEffect = "fs-player-effect";
    public const string ShopGlow = "fs-shop-glow";

    // Glows tint the sprite, so they go under any outline drawn round it.
    public static void SetGlow(SpriteSystem sprites, Entity<SpriteComponent> sprite, string id, ShaderInstance shader)
    {
        Set(sprites, sprite, new SpriteComponent.PostShaderArgs(id, shader) { Before = ContentPostShaderIds.BeforeOutlines });
    }

    public static void SetOutline(SpriteSystem sprites, Entity<SpriteComponent> sprite, string id, ShaderInstance shader)
    {
        Set(sprites, sprite, new SpriteComponent.PostShaderArgs(id, shader) { After = ContentPostShaderIds.AfterBaseEffects });
    }

    // Left alone when already set, as some callers run every frame.
    private static void Set(SpriteSystem sprites, Entity<SpriteComponent> sprite, SpriteComponent.PostShaderArgs args)
    {
        if (sprites.TryGetPostShader(sprite.Comp, args.Id, out var entry) && entry.Shader == args.Shader)
            return;

        sprites.SetPostShader(sprite.Comp, args);
    }
}
