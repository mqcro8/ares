
using Godot;

public partial class Background : Sprite2D
{
	[Export] public float Speed = 150f;

	private bool ascending = false;
	private float maxTravel;
	private float initialY;

	public override void _Ready()
	{
		Vector2 viewport = GetViewportRect().Size;

		// Ajustar imagen al ancho de pantalla
		float scaleFactor = viewport.X / Texture.GetWidth();
		Scale = Vector2.One * scaleFactor;

		float imageHeight = Texture.GetHeight() * scaleFactor;

		maxTravel = imageHeight - viewport.Y;

		// Comenzar mostrando la parte inferior de la imagen
		initialY = viewport.Y - imageHeight / 2f;
		Position = new Vector2(viewport.X / 2f, initialY);
	}

	public void BeginAscend()
	{
		ascending = true;
	}

	public override void _Process(double delta)
	{
		if (!ascending)
			return;

		float distance = Position.Y - initialY;

		if (distance < maxTravel)
		{
			Position += Vector2.Down * Speed * (float)delta;
			Position = new Vector2(
				Position.X,
				Mathf.Min(Position.Y, initialY + maxTravel)
			);
		}
		else
		{
			ascending = false;
		}
	}
}
