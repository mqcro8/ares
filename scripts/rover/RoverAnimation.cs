namespace ares.scripts.rover;

using Godot;

public partial class RoverAnimation : AnimatedSprite2D
{
	public override void _Ready()
	{
	}

	public void PlayAnimation(string animationName)
	{
		Play(animationName);
	}
}
