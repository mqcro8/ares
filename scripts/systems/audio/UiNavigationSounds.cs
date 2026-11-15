namespace Ares.Systems.Audio;

using Godot;
using System;
using System.Collections.Generic;

/// <summary>Navigation only: action owners emit success/rejection after checking the result.</summary>
public partial class UiNavigationSounds : Node
{
	private readonly FeedbackSounds _sounds = new();
	private readonly List<(BaseButton Button, Action Handler, Action Clear)> _bindings = new();
	private BaseButton _lastButton;

	public static void Attach(Node root)
	{
		if (root.GetNodeOrNull("UiNavigationSounds") != null) return;
		var helper = new UiNavigationSounds { Name = "UiNavigationSounds" };
		root.AddChild(helper);
		helper.Bind(root);
	}

	private void Bind(Node node)
	{
		if (node is BaseButton button)
		{
			Action handler = () =>
			{
				if (button.Disabled || !button.IsVisibleInTree() || !button.CanProcess() || _lastButton == button) return;
				_lastButton = button;
				_sounds.Play(SoundIds.UiNavigate, 100, true);
			};
			Action clear = () =>
			{
				if (_lastButton == button) _lastButton = null;
			};
			button.FocusEntered += handler;
			button.MouseEntered += handler;
			button.FocusExited += clear;
			button.MouseExited += clear;
			_bindings.Add((button, handler, clear));
		}
		foreach (Node child in node.GetChildren()) if (child != this) Bind(child);
	}

	public override void _ExitTree()
	{
		foreach (var (button, handler, clear) in _bindings)
		{
			if (!IsInstanceValid(button)) continue;
			button.FocusEntered -= handler;
			button.MouseEntered -= handler;
			button.FocusExited -= clear;
			button.MouseExited -= clear;
		}
		_bindings.Clear();
	}
}
