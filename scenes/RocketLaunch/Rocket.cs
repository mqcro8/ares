
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

	public void StartIgnition()
	{
		fire.Visible = true;
		fire.SpeedScale = 1;
		fire.Modulate = Colors.White;
		fire.Play("ignition");
	}

	public void LaunchFailed()
	{
		fire.Visible = true;
		fire.SpeedScale = 1;
		fire.Modulate = Colors.White;
		fire.Play("failed");
	}

	// Despegue exitoso
	public void LaunchSuccess()
	{
		fire.Visible = true;
		fire.SpeedScale = 1;
		fire.Modulate = Colors.White;
		fire.Play("success");
	}
	// Fuego continuo mientras se sostiene el empuje
	public void BeginThrust()
	{
		fire.Visible = true;
		fire.Play("thrust");
	}

	public void SetThrust(double thrust)
	{
		if (!fire.Visible || fire.Animation != "thrust") return;
		float strength = Mathf.Clamp((float)thrust / 100, 0, 1);
		fire.SpeedScale = .4f + strength * 1.6f;
		fire.Modulate = new Color(1, .85f + strength * .15f, .85f + strength * .15f);
	}

	// Fuego continuo durante el ascenso real
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
