using Godot;
using System.Collections.Generic;

// Register as an Autoload: Project Settings > Globals > Autoload, name it "SceneManager".
public partial class SceneManager : Node
{
    public static SceneManager Instance { get; private set; }

    // A stack so buildings inside buildings also work.
    private readonly Stack<(string Scene, Vector2 Position)> _returnStack = new();

    public override void _Ready()
    {
        Instance = this;
    }

    // Go into a new scene, remembering where to come back to.
    public void Enter(string targetScene, Vector2 returnPosition)
    {
        _returnStack.Push((GetTree().CurrentScene.SceneFilePath, returnPosition));
        ChangeScene(targetScene, null);
    }

    // Go back to the previous scene and place the player at the saved position.
    public void Exit()
    {
        if (_returnStack.Count == 0)
        {
            GD.PushWarning("SceneManager: nothing to return to.");
            return;
        }

        var (scene, position) = _returnStack.Pop();
        ChangeScene(scene, position);
    }

    private async void ChangeScene(string path, Vector2? spawnPosition)
    {
        GetTree().ChangeSceneToFile(path);

        if (spawnPosition is null)
            return;

        // Wait for the new scene to be loaded and in the tree.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        foreach (Node node in GetTree().GetNodesInGroup("player"))
        {
            if (node is Node2D player)
                player.GlobalPosition = spawnPosition.Value;
        }
    }
}