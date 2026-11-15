using Godot;

namespace ares.scripts.mars;

public partial class Animator : AnimatedSprite2D
{


    public override void _Ready()
    {
        if (SpriteFrames != null && SpriteFrames.HasAnimation("blink"))
        {
            Play("blink");
        }
    }
}
