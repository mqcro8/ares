
using Godot;

public partial class Background : Sprite2D
{
	private float initialY;
	[Export] public Texture2D PracticeTexture { get; set; }
	private Vector2 _launchSocket = new(510, 614);
	public void Configure(bool practice)
	{
		if (practice)
		{
			Texture = PracticeTexture;
			_launchSocket = new Vector2(300, 1340);
		}
		else
		{
			_launchSocket = new Vector2(510, 614);
		}
		Resize();
	}

	public Vector2 LaunchGround => new Vector2(Position.X, initialY) +
		new Vector2(_launchSocket.X - Texture.GetWidth() / 2f,
			_launchSocket.Y - Texture.GetHeight() / 2f) * Scale;

	public override void _Ready()
	{
		GetViewport().SizeChanged += Resize;
		Resize();
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= Resize;
	private void Resize()
	{
		Vector2 viewport = GetViewportRect().Size;

		// Fill the full viewport; controls overlay this static backdrop.
		float scaleFactor = Mathf.Max(viewport.X / Texture.GetWidth(), viewport.Y / Texture.GetHeight());
		Scale = Vector2.One * scaleFactor;

		float imageHeight = Texture.GetHeight() * scaleFactor;

		// Start with the lower launch platform in view.
		initialY = viewport.Y - imageHeight / 2f;
		Position = new Vector2(viewport.X / 2f, initialY);
	}
}
