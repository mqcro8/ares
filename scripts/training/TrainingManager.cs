namespace ares.scripts.training;

using Godot;
using System.Collections.Generic;
using ares.scripts.player;

public partial class TrainingManager : Node2D
{
	[Export] public float FallSpeedThreshold { get; set; } = 350f;

	private Label _speakerLabel;
	private Label _dialogueLabel;
	private Button _nextButton;
	private Label _progressLabel;
	private Player _player;
	private Area2D _exitTrigger;

	private readonly Queue<(string Speaker, string Text)> _dialogueQueue = new();
	private bool _dialogueActive;
	private bool _fallTriggered;
	private bool _sceneChangePending;

	private bool _wasOnFloor;
	private float _maxFallSpeed;

	public override void _Ready()
	{
		_player = GetNode<Player>("player");
		_speakerLabel = GetNode<Label>("UI/DialoguePanel/SpeakerLabel");
		_dialogueLabel = GetNode<Label>("UI/DialoguePanel/DialogueLabel");
		_nextButton = GetNode<Button>("UI/NextButton");
		_progressLabel = GetNode<Label>("UI/ProgressLabel");
		_exitTrigger = GetNode<Area2D>("ExitTrigger");

		// Posicionar al jugador en el punto de aparición
		var spawnPoint = GetNode<Node2D>("SpawnPoint");
		if (spawnPoint != null)
			_player.GlobalPosition = spawnPoint.GlobalPosition;

		// Señales
		_exitTrigger.BodyEntered += OnExitTriggerEntered;
		_nextButton.Pressed += OnNextPressed;
		_nextButton.Visible = false;

		// Iniciar el flujo de diálogos (módulo: movimiento)
		Enqueue("CONTROL", "Bienvenido, Comandante. Nuestra Gateway lleva dos semanas en silencio y tú vas a ser quien la reactive. Antes de salir, necesitas dominar tu cuerpo, tu equipo y tu nave.");
		Enqueue("CONTROL", "Aquí aprenderás a moverte. Los ejercicios son solo práctica, nada es permanente.");
		NextLine();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null || _fallTriggered)
			return;

		bool onFloor = _player.IsOnFloor();

		if (!onFloor)
		{
			// Registrar la velocidad máxima de caída mientras está en el aire
			if (_player.Velocity.Y > _maxFallSpeed)
				_maxFallSpeed = _player.Velocity.Y;
		}
		else if (!_wasOnFloor)
		{
			// El jugador acaba de tocar el suelo tras estar en el aire.
			// Si la caída fue lo suficientemente brusca, activar el diálogo de daño por caída.
			if (_maxFallSpeed > FallSpeedThreshold && !_fallTriggered)
			{
				_fallTriggered = true;
				EnqueueFallDialogue();
				if (!_dialogueActive)
					NextLine();
			}
			_maxFallSpeed = 0f;
		}

		_wasOnFloor = onFloor;
	}

	private void EnqueueFallDialogue()
	{
		Enqueue("CONTROL", "¡Uf! Eso me hizo algo de daño, tengo que tener cuidado con las grandes caídas.");
		Enqueue("CONTROL", "Desde las alturas, el cuerpo paga. Y allá arriba la gravedad será distinta, así que tu cuerpo va a cansarse de otra forma.");
		Enqueue("CONTROL", "Bien. Ahora dirígete al punto de salida para continuar.");
	}

	private void Enqueue(string speaker, string text)
	{
		_dialogueQueue.Enqueue((speaker, text));
	}

	/// <summary>Avanza en la cola de diálogos y muestra la siguiente línea.</summary>
	public void NextLine()
	{
		if (_dialogueQueue.Count == 0)
		{
			_nextButton.Visible = false;
			_dialogueActive = false;

			if (_sceneChangePending)
				LoadNextScene();

			return;
		}

		var line = _dialogueQueue.Dequeue();
		ShowDialogue(line.Speaker, line.Text);
		_dialogueActive = true;

		_nextButton.Visible = _dialogueQueue.Count > 0;
	}

	public void ShowDialogue(string speaker, string text)
	{
		_speakerLabel.Text = speaker;
		_dialogueLabel.Text = text;
	}

	private void OnNextPressed()
	{
		if (!_dialogueActive)
			return;

		NextLine();
	}

	private void OnExitTriggerEntered(Node2D body)
	{
		if (body != _player)
			return;

		if (_sceneChangePending)
			return;

		if (!_fallTriggered)
			return; // El jugador debe completar el entrenamiento de movimiento primero.

		_sceneChangePending = true;
		Enqueue("CONTROL", "Excelente. Has completado el entrenamiento. Prepárate para el despegue.");

		if (!_dialogueActive)
			NextLine();
	}

	private void LoadNextScene()
	{
		const string nextScene = "res://scenes/main_menu/main_menu_take3.tscn";

		if (ResourceLoader.Exists(nextScene))
			GetTree().ChangeSceneToFile(nextScene);
		else
		{
			GD.PushWarning("Training: la escena de destino no existe. TODO: asignar la escena de despegue/corte.");
			// TODO: reemplazar por la escena de despegue/cinemática una vez exista.
		}
	}
}
