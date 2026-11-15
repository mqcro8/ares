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
        var currentScene = GetTree().CurrentScene;
        if (currentScene == null)
        {
            GD.PushWarning("SceneManager: cannot enter a scene when there is no current scene.");
            return;
        }

        if (ChangeScene(targetScene, null) == Error.Ok)
            _returnStack.Push((currentScene.SceneFilePath, returnPosition));
    }

    // Go back to the previous scene and place the player at the saved position.
    public void Exit()
    {
        if (_returnStack.Count == 0)
        {
            GD.PushWarning("SceneManager: nothing to return to.");
            return;
        }

        var (scene, position) = _returnStack.Peek();
        if (ChangeScene(scene, position) == Error.Ok)
            _returnStack.Pop();
    }

    private Error ChangeScene(string path, Vector2? spawnPosition)
    {
        Error error = GetTree().ChangeSceneToFile(path);
        if (error != Error.Ok)
        {
            GD.PushWarning($"SceneManager: unable to load {path}: {error}");
            return error;
        }

        if (spawnPosition.HasValue)
            _ = PlacePlayer(path, spawnPosition.Value);
        return Error.Ok;
    }

    private async System.Threading.Tasks.Task PlacePlayer(string scenePath, Vector2 spawnPosition)
    {
        // Wait for the new scene to be loaded and in the tree.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (GetTree().CurrentScene?.SceneFilePath != scenePath)
            return;

        foreach (Node node in GetTree().GetNodesInGroup("player"))
        {
            if (node is Node2D player)
                player.GlobalPosition = spawnPosition;
        }
    }
}
