using Godot;

public partial class Background : Sprite2D
{
	private float initialY;
	private float _scrollOffset;
	[Export] public Texture2D PracticeTexture { get; set; }
	[Export] public Vector2 LaunchSocket { get; set; } = new(254, 1814);
	public float ScrollOffset => _scrollOffset;
	public float MaximumScrollOffset => Texture == null ? 0 :
		Mathf.Max(0, Texture.GetHeight() * Scale.Y - GetViewportRect().Size.Y);
	public void Configure(bool practice)
	{
		if (practice && PracticeTexture != null)
			Texture = PracticeTexture;
		ResetScroll();
		Resize();
	}

	/// <summary>Moves the scenery downward to simulate the rocket gaining altitude.</summary>
	public void SetScrollOffset(float offset)
	{
		if (Texture == null) return;

		_scrollOffset = Mathf.Clamp(offset, 0, MaximumScrollOffset);
		Position = new Vector2(Position.X, initialY + _scrollOffset);
	}

	/// <summary>Restores the background to the launch-pad view.</summary>
	public void ResetScroll()
	{
		_scrollOffset = 0;
		Position = new Vector2(Position.X, initialY);
	}

	public Vector2 LaunchGround => new Vector2(Position.X, initialY) +
		new Vector2(LaunchSocket.X - Texture.GetWidth() / 2f,
			LaunchSocket.Y - Texture.GetHeight() / 2f) * Scale;

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
		float maxScroll = Mathf.Max(0, imageHeight - viewport.Y);
		_scrollOffset = Mathf.Clamp(_scrollOffset, 0, maxScroll);
		Position = new Vector2(viewport.X / 2f, initialY + _scrollOffset);
	}
}
