using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;

public static class VerifyPlayerActions
{
    public static async Task<object> VerifyPredictionSeeds()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerActionController>();
        var selection = player.GetComponent<PlayerActionSelection>();
        var preview = player.GetComponent<PlayerActionPreview>();
        var playback = UnityEngine.Object.FindFirstObjectByType<CombatPlaybackController>();
        playback.SetPaused(true);
        float maximumError = 0f;
        string[] seeds = { "ridge", "valley", "map-verification", "map-ui-verification", "a saved seed", Guid.NewGuid().ToString("N") };
        foreach (string seed in seeds)
        {
            player.Motor.Terrain.Generate(seed);
            selection.SelectMove(PlayerMove.Jump);
            selection.SetDirection(new Vector2(1f, 1f));
            selection.SetStrength(1f);
            await Task.Delay(150);
            Vector2 predicted = preview.PredictedPosition;
            Require(player.TryStart(PlayerMove.Jump, selection.Direction, selection.Strength), "Jump failed on seed " + seed);
            player.Clock.AdvanceFrames(12);
            float error = Vector2.Distance(predicted, player.Motor.Body.position);
            maximumError = Mathf.Max(maximumError, error);
            Require(error < 0.12f, "Prediction mismatch on seed " + seed + ": " + error);
        }
        playback.ResetPlayer();
        return new { terrainSeedsChecked = seeds.Length, maximumPredictionError = maximumError };
    }

    public static async Task<object> VerifyWorldAim()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerActionController>();
        var selection = player.GetComponent<PlayerActionSelection>();
        var original = UnityEngine.InputSystem.Mouse.current;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Click aim verification mouse");
        try
        {
            mouse.MakeCurrent();
            selection.SetDirection(Vector2.left);
            var pointer = new Vector2(Screen.width * 0.55f, Screen.height * 0.8f);
            Vector2 expected = ((Vector2)Camera.main.ScreenToWorldPoint(pointer) - (Vector2)player.transform.position).normalized;
            long frame = player.Clock.CurrentFrame;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = pointer }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            await Task.Delay(150);
            Require(Vector2.Distance(selection.Direction, expected) < 0.01f, "World click did not set the expected aim.");
            Require(player.Clock.CurrentFrame == frame && !player.IsBusy, "Aiming committed movement or advanced time.");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = pointer });
            await Task.Delay(60);
            return new { worldClickSetsDirection = true, aimDoesNotCommitOrAdvanceTime = true };
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            if (original != null) original.MakeCurrent();
        }
    }

    public static async Task<object> Verify()
    {
        Require(Application.isPlaying, "Enter Play Mode first.");
        var actor = UnityEngine.Object.FindFirstObjectByType<PlayerActionController>();
        var selection = actor.GetComponent<PlayerActionSelection>();
        var preview = actor.GetComponent<PlayerActionPreview>();
        var playback = UnityEngine.Object.FindFirstObjectByType<CombatPlaybackController>();
        var clock = actor.Clock;
        var root = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        Require(UnityEngine.Object.FindFirstObjectByType<WelpTimeToDie.Input.PlayerKeyboardInput>() == null, "Legacy keyboard movement is still active.");
        Require(Mathf.Abs(actor.Motor.BodyCollider.bounds.size.x - 1f) < 0.001f && Mathf.Abs(actor.Motor.BodyCollider.bounds.size.y - 2f) < 0.001f,
            "Player collision size does not equal 16 × 32 pixels.");
        long beforePlanning = clock.CurrentFrame;
        Vector2 beforePosition = actor.Motor.Body.position;
        Click(root, "actionJump");
        Click(root, "aimUpRight");
        root.Q<Slider>("actionStrength").value = 1f;
        await Task.Delay(200);
        Require(clock.CurrentFrame == beforePlanning && actor.Motor.Body.position == beforePosition, "Planning advanced the live world.");
        Vector2 predicted = preview.PredictedPosition;
        Require(preview.PredictedFrames == 12, "Jump preview has the wrong frame count.");
        Click(root, "stepFrame");
        Require(clock.CurrentFrame == beforePlanning + 1 && actor.State.ElapsedFrames == 1 && playback.IsPaused, "Next frame did not advance exactly one paused frame.");
        Require(actor.Motor.Body.position.y > beforePosition.y, "Jump did not lift the player.");
        await Task.Delay(100);
        Require(clock.CurrentFrame == beforePlanning + 1, "Paused playback continued advancing.");
        for (int i = 0; i < 11; i++) Click(root, "stepFrame");
        Require(!actor.IsBusy && !actor.Motor.IsGrounded, "Jump should reach an airborne decision point.");
        float predictionError = Vector2.Distance(predicted, actor.Motor.Body.position);
        Require(predictionError < 0.12f, "Jump prediction differs from live physics: " + predictionError);
        Require(!actor.State.CanStart(PlayerMove.Jump, actor.Motor.IsGrounded), "Air jump was incorrectly enabled.");
        Vector2 airbornePosition = actor.Motor.Body.position;
        await Task.Delay(100);
        Require(actor.Motor.Body.position == airbornePosition, "Airborne position moved while time was stopped.");

        // Wait actions advance gravity and cooldowns without an enemy or automatic clock.
        Click(root, "actionWait");
        for (int i = 0; i < 8; i++)
        {
            Require(playback.TryAction(PlayerMove.Wait, Vector2.right, 1f), "Wait did not start.");
            clock.AdvanceFrames(4);
        }
        Require(actor.Motor.IsGrounded, "Player did not land on the procedural terrain.");
        var collider = actor.Motor.BodyCollider;
        float expectedFloor = actor.Motor.Terrain.GetSurfaceHeight(actor.Motor.Body.position.x, collider.bounds.extents.x - 0.01f);
        Require(Mathf.Abs(collider.bounds.min.y - expectedFloor) < 0.08f, "Player landed inside the terrain.");

        Click(root, "actionAttack");
        Click(root, "aimRight");
        Click(root, "stepFrame");
        for (int i = 0; i < 3; i++) Click(root, "stepFrame");
        Require(actor.State.IsAttackActive, "Attack was not active on frame four.");
        clock.AdvanceFrames(5);
        Require(!actor.IsBusy, "Attack did not finish after nine frames.");
        Click(root, "actionBlock");
        Click(root, "stepFrame");
        Require(actor.State.IsBlocking, "Block did not start.");
        var combat = actor.GetComponent<PlayerCombatController>();
        Require(combat.ResolveIncomingDamage(2, (Vector2)actor.transform.position + Vector2.right) == 0, "Frontal guard did not reduce damage.");
        Require(combat.ResolveIncomingDamage(2, (Vector2)actor.transform.position + Vector2.left) == 2, "Guard incorrectly protected the rear.");
        clock.AdvanceFrames(7);

        Click(root, "resetPlayer");
        Click(root, "actionDash");
        Click(root, "aimRight");
        selection.SetStrength(0.5f);
        await Task.Delay(120);
        predicted = preview.PredictedPosition;
        Require(playback.TryAction(PlayerMove.Dash, Vector2.right, 0.5f), "Dash did not start.");
        playback.SetPaused(true);
        clock.AdvanceFrames(6);
        Require(!actor.IsBusy, "Dash did not finish after six frames.");
        float dashPredictionError = Vector2.Distance(predicted, actor.Motor.Body.position);
        Require(dashPredictionError < 0.15f, "Dash prediction differs from live terrain collisions: " + dashPredictionError);
        Click(root, "resetPlayer");
        return new { sizePixels = "16 × 32", controls = "click/buttons only", jumpFrames = 12, attackFrame = 4,
            attackDuration = 9, blockFrames = 8, dashFrames = 6, predictionError, dashPredictionError,
            airborneDecisionVerified = true, landingVerified = true, liveWorldUnchangedByPreview = true };
    }

    private static void Click(VisualElement root, string name)
    {
        var button = root.Q<Button>(name);
        Require(button != null, "Missing button: " + name);
        button.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) button.SendEvent(submit);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
