
using Godot;

public partial class Rocket : Node2D
{
	private AnimatedSprite2D fire;

	public override void _Ready()
	{
		fire = GetNode<AnimatedSprite2D>("Fire");

		fire.Visible = false;
		fire.Stop();
	}

	// Animación inicial de ignición
	public void StartIgnition()
	{
		fire.Visible = true;
		fire.Play("ignition");
	}

	// Despegue exitoso
	public void LaunchSuccess()
	{
		fire.Visible = true;
		fire.Play("success");
	}

	// Despegue fallido
	public void LaunchFailed()
	{
		fire.Visible = true;
		fire.Play("failed");
	}

	// Fuego continuo durante el ascenso
	public void StartAscending()
	{
		fire.Visible = true;
		fire.Play("ascending");
	}

	public void StopFire()
	{
		fire.Stop();
		fire.Visible = false;
	}
}
