namespace ares.scripts.training;
using Godot;
using System.Collections.Generic;
using ares.scripts.player;
using ares.scripts.mission;

/// <summary>Memory-only room state with single-use, session-scoped travel tickets.</summary>
public static class PracticeSession
{
    public const string TrainingScene = "res://scenes/training/Training.tscn";
    public const string LabScene = "res://scenes/training/TrainingLab.tscn";
    public enum Exercise { Wires, Fuses, Frequency, HullRepair }
    public enum LaunchMode { Practice, Real }
    public sealed record LaunchRequest(long Session, long MissionSequence, LaunchMode Mode, Vector2 ReturnPosition);
    public sealed record RoomPose(Vector2 Position, string Facing, bool Flip);
    public sealed record Snapshot(PhysicalTraining.Progress Course, TrainingMovement.Progress Movement, bool CompletionShown);
    public sealed record Travel(long Session, long Id, string Source, string Target, string Spawn, Vector2 ReturnPosition);
    public static readonly Dictionary<string, RoomPose> Rooms = new();
    private static readonly Dictionary<string, HashSet<Exercise>> CompletedExercises = new();
    public static bool CompletedIn(string scene, Exercise exercise)
        => CompletedExercises.TryGetValue(scene, out var exercises) && exercises.Contains(exercise);
    public static Snapshot Saved { get; set; }
    public static Travel Pending { get; private set; }
    public static long SessionId { get; private set; }
    private static long _serial, _exerciseTicket;
    public static Exercise Selected { get; private set; }
    public static bool Wires => MissionState.Instance?.TrainingWiresComplete ?? false;
    public static bool Fuses => MissionState.Instance?.TrainingFusesComplete ?? false;
    public static bool LaunchPracticeComplete => MissionState.Instance?.TrainingLaunchPracticeComplete ?? false;
    public static LaunchRequest PendingLaunch { get; private set; }
    public static string Feedback { get; private set; } = "";
    public static string ExerciseOriginScene { get; private set; }
    public static string ExerciseReturnSpawn { get; private set; }
    public static Vector2 ExerciseReturnPosition { get; private set; }
    public static string SceneFor(Exercise exercise) => exercise switch
    {
        Exercise.Wires => "res://scenes/minigames/wireminigame.tscn",
        Exercise.Fuses => "res://scenes/minigames/FuseBoxMinigame.tscn",
        Exercise.Frequency => "res://scenes/minigames/FrequencyBoxMinigame.tscn",
        Exercise.HullRepair => "res://scenes/minigames/HullRepairMinigame.tscn",
        _ => throw new System.ArgumentOutOfRangeException(nameof(exercise), exercise, null)
    };
    private static string ReturnSpawn(Exercise exercise) => exercise switch
    {
        Exercise.Wires => "lab.wires.return",
        Exercise.Fuses => "lab.fuses.return",
        Exercise.Frequency => "lab.frequency.return",
        Exercise.HullRepair => "lab.hull.return",
        _ => throw new System.ArgumentOutOfRangeException(nameof(exercise), exercise, null)
    };
    public static void Clear()
    {
        SessionId++; Pending = null; Saved = null; Rooms.Clear();
        CompletedExercises.Clear();
        _exerciseTicket = 0; PendingLaunch = null; Feedback = "";
        ExerciseOriginScene = null; ExerciseReturnSpawn = null; ExerciseReturnPosition = Vector2.Zero;
    }
    public static Travel Prepare(string source, string target, string spawn, Vector2 returnPosition)
        => Pending = new(SessionId, ++_serial, source, target, spawn, returnPosition);
    public static Travel Arrive(string target)
    {
        if (Pending == null || Pending.Session != SessionId || Pending.Target != target) return null;
        var ticket = Pending; Pending = null; return ticket;
    }
    public static void Abort() { Pending = null; PendingLaunch = null; _exerciseTicket = 0; }
    public static void Capture(string room, Player player)
    {
        var visual = player.GetNode<PlayerAnimation>("AnimatedSprite2D");
        Rooms[room] = new(player.GlobalPosition, visual.Facing, visual.FlipH);
    }
    public static void RestoreFacing(string room, Player player)
    {
        if (Rooms.TryGetValue(room, out var pose)) player.GetNode<PlayerAnimation>("AnimatedSprite2D").RestoreFacing(pose.Facing, pose.Flip);
    }
    public static Travel Begin(Exercise exercise, Vector2 position)
        => Begin(exercise, LabScene, ReturnSpawn(exercise), position);

    public static Travel Begin(Exercise exercise, string source, string returnSpawn, Vector2 position)
    {
        Selected = exercise;
        ExerciseOriginScene = source;
        ExerciseReturnSpawn = returnSpawn;
        ExerciseReturnPosition = position;
        var ticket = Prepare(source, SceneFor(exercise), returnSpawn, position);
        _exerciseTicket = ticket.Id; return ticket;
    }
    public static long EnterExercise(Node root)
    {
        var ticket = Arrive(root.SceneFilePath);
        return ticket != null && ticket.Id == _exerciseTicket ? ticket.Id : 0;
    }
    public static bool Finish(long session, long ticket, bool success)
    {
        if (session != SessionId || ticket == 0 || ticket != _exerciseTicket) return false;
        _exerciseTicket = 0;
        if (success)
        {
            string origin = ExerciseOriginScene ?? LabScene;
            if (!CompletedExercises.TryGetValue(origin, out var exercises))
                CompletedExercises[origin] = exercises = new();
            exercises.Add(Selected);
            switch (Selected)
            {
                case Exercise.Wires: MissionState.Instance.TrainingWiresComplete = true; break;
                case Exercise.Fuses: MissionState.Instance.TrainingFusesComplete = true; break;
                case Exercise.Frequency: MissionState.Instance.TrainingFrequencyComplete = true; break;
                case Exercise.HullRepair: MissionState.Instance.TrainingHullRepairComplete = true; break;
            }
            MissionState.Instance.EvaluateTrainingCompletion();
        }
        Feedback = success ? "Practice complete. You can repeat it anytime." : "Practice cancelled. Previous progress kept.";
        Prepare(SceneFor(Selected), ExerciseOriginScene ?? LabScene,
            ExerciseReturnSpawn ?? ReturnSpawn(Selected), ExerciseReturnPosition);
        return true;
    }
    public static void ClearFeedback() => Feedback = "";
    public static void RequestLaunch(LaunchMode mode, Vector2 returnPosition)
        => PendingLaunch = new(SessionId, MissionState.Instance.LaunchSequence, mode, returnPosition);
    public static LaunchRequest ConsumeLaunch()
    {
        var request = PendingLaunch; PendingLaunch = null;
        return request != null && request.Session == SessionId &&
            request.MissionSequence == MissionState.Instance.LaunchSequence ? request : null;
    }
}
