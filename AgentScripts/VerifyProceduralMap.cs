using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Cameras;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Player;

public static class VerifyProceduralMap
{
    public static async Task<object> VerifyMouseInput()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<MapCameraController>();
        var originalMouse = UnityEngine.InputSystem.Mouse.current;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Map camera verification mouse");
        try
        {
            mouse.MakeCurrent();
            controller.RecenterOnPlayer();
            Vector3 start = controller.transform.position;
            Vector2 pointer = new Vector2(Screen.width * 0.55f, Screen.height * 0.55f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = pointer }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));
            await Task.Delay(120);
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position, pointer - new Vector2(160f, 0f));
            await Task.Delay(120);
            Require(controller.IsBrowsing, "Right-button dragging did not enter browse mode.");
            Require(Vector3.Distance(start, controller.transform.position) > 0.1f, "Mouse dragging did not move the camera.");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState { position = pointer - new Vector2(160f, 0f) });
            await Task.Delay(80);
            float beforeZoom = controller.Zoom;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0f, 120f));
            await Task.Delay(120);
            Require(controller.Zoom < beforeZoom, "Scroll-wheel input did not zoom the camera.");
            controller.ZoomBy(-1f, pointer);
            controller.RecenterOnPlayer();
            return new { rightDragVerified = true, scrollWheelVerified = true };
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            if (originalMouse != null) originalMouse.MakeCurrent();
        }
    }

    public static async Task<object> Verify()
    {
        Require(Application.isPlaying, "Run this scene verification in Play Mode.");
        var terrain = UnityEngine.Object.FindFirstObjectByType<ProceduralTerrainMap>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerDashController>();
        if (player == null) return await VerifyCurrentMap();
        var clock = UnityEngine.Object.FindFirstObjectByType<CombatPhysicsController>();
        var camera = UnityEngine.Object.FindFirstObjectByType<MapCameraController>();
        var body = player.GetComponent<Rigidbody2D>();
        var collider = player.GetComponent<BoxCollider2D>();
        int firstFrame = Time.frameCount;
        await Task.Delay(120);
        Require(Time.frameCount > firstFrame, "The Unity player loop is not advancing.");
        Require(terrain.HeightMap.Width == 128 && terrain.HeightMap.Height == 32, "Unexpected scene map dimensions.");
        Require(Mathf.Abs(camera.Zoom - 6.75f) < 0.001f, "The default camera scale changed.");
        string initialSeed = terrain.CurrentSeed;
        terrain.Generate("scene-smoke-test");
        Bounds limits = terrain.Layout.WorldBounds;
        int occupiedTiles = 0;
        var tilemap = terrain.GetComponentInChildren<TerrainTilemapRenderer>().Tilemap;
        for (int x = 0; x < terrain.HeightMap.Width; x++)
            for (int y = 0; y < terrain.HeightMap.Height; y++)
            {
                bool filled = terrain.HeightMap.IsSolid(x, y);
                Require(tilemap.HasTile(new Vector3Int(x + terrain.Layout.PlayableCells.xMin, y + terrain.Layout.PlayableCells.yMin, 0)) == filled, "Rendered tiles differ from generated data.");
                if (filled) occupiedTiles++;
            }
        Require(terrain.GetComponentsInChildren<BoxCollider2D>().Length == 4, "Missing boundary colliders.");
        Physics2D.SyncTransforms();
        var leftWall = Physics2D.Raycast(new Vector2(limits.min.x + 2f, limits.max.y - 2f), Vector2.left, 4f);
        var rightWall = Physics2D.Raycast(new Vector2(limits.max.x - 2f, limits.max.y - 2f), Vector2.right, 4f);
        Require(leftWall.collider != null && rightWall.collider != null, "Side boundaries do not collide.");

        player.DashStrength = 1f;
        float maximumGroundError = 0f;
        for (int direction = 1; direction >= -1; direction -= 2)
        {
            for (int dash = 0; dash < 15; dash++)
            {
                Require(direction > 0 ? player.DashRight() : player.DashLeft(), "Dash failed to start.");
                for (int frame = 0; frame < PlayerDashController.DashFrames; frame++)
                {
                    clock.AdvanceFrames(1);
                    Physics2D.SyncTransforms();
                    Require(collider.bounds.min.x >= limits.min.x - 0.001f && collider.bounds.max.x <= limits.max.x + 0.001f, "Player escaped map bounds.");
                    float groundError = Mathf.Abs(collider.bounds.min.y - terrain.GetSurfaceHeight(body.position.x, collider.bounds.extents.x) - 0.02f);
                    maximumGroundError = Mathf.Max(maximumGroundError, groundError);
                    Require(groundError < 0.03f, "Player is not following the terrain surface.");
                }
            }
            Require(Mathf.Abs(body.position.x - (direction > 0 ? limits.max.x - collider.bounds.extents.x : limits.min.x + collider.bounds.extents.x)) < 0.1f,
                "Player did not traverse to the expected map edge.");
        }

        // A body without the player adapter must also be physically blocked by the wall.
        var probe = new GameObject("Temporary map boundary probe", typeof(BoxCollider2D), typeof(Rigidbody2D));
        try
        {
            probe.transform.position = new Vector3(limits.max.x - 1f, limits.max.y - 2f);
            var probeBody = probe.GetComponent<Rigidbody2D>();
            probeBody.bodyType = RigidbodyType2D.Dynamic;
            probeBody.gravityScale = 0f;
            probeBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            probeBody.linearVelocity = Vector2.right * 30f;
            Physics2D.SyncTransforms();
            clock.AdvanceFrames(24);
            Require(probe.GetComponent<BoxCollider2D>().bounds.max.x <= limits.max.x + 0.01f, "The side wall allowed a physics body through.");
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); }

        camera.Pan(new Vector2(20f, 3f));
        await Task.Delay(80);
        Require(camera.IsBrowsing, "Panning did not enter browse mode.");
        var document = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
        var recenter = document.rootVisualElement.Q<Button>("recenterPlayer");
        Require(recenter != null && !recenter.ClassListContains("hidden"), "Recenter button is not visible while browsing.");
        recenter.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) recenter.SendEvent(submit);
        await Task.Delay(80);
        Require(!camera.IsBrowsing && recenter.ClassListContains("hidden"), "Recenter button did not restore player follow.");
        camera.ZoomBy(2f, new Vector2(Screen.width / 2f, Screen.height / 2f));
        Require(camera.Zoom < 6.75f, "Zoom input did not change the view.");
        camera.ZoomBy(-2f, new Vector2(Screen.width / 2f, Screen.height / 2f));

        var seedInput = document.rootVisualElement.Q<TextField>("seedInput");
        seedInput.value = "seed-button-check";
        var generate = document.rootVisualElement.Q<Button>("generateMap");
        generate.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) generate.SendEvent(submit);
        Require(terrain.CurrentSeed == "seed-button-check", "Seed input/button did not generate the requested map.");
        int sample = terrain.HeightMap.GetColumnHeight(10);
        terrain.Generate("seed-button-check");
        Require(terrain.HeightMap.GetColumnHeight(10) == sample, "Seed regeneration is not repeatable.");
        terrain.GenerateRandomMap();
        string randomSeed = terrain.CurrentSeed;
        terrain.GenerateRandomMap();
        Require(terrain.CurrentSeed != randomSeed && terrain.CurrentSeed != initialSeed, "Random seeds were reused.");
        player.DashStrength = 0.5f;
        player.ResetToSpawn();
        Require(Vector2.Distance(body.position, player.transform.position) < 0.001f, "Reset did not update the rendered player pose.");
        camera.RecenterOnPlayer();
        return new { success = true, occupiedTiles, maximumGroundError, traversedBothEdges = true,
            physicalWallVerified = true, recenterButtonVerified = true, seedButtonVerified = true, playerLoopAdvancing = true };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task<object> VerifyCurrentMap()
    {
        var terrain = UnityEngine.Object.FindFirstObjectByType<ProceduralTerrainMap>();
        var camera = UnityEngine.Object.FindFirstObjectByType<MapCameraController>();
        var root = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        terrain.Generate("map-verification");
        var cells = terrain.Layout.PlayableCells;
        var tiles = terrain.GetComponentInChildren<TerrainTilemapRenderer>().Tilemap;
        int occupied = 0;
        for (int x = 0; x < terrain.HeightMap.Width; x++)
            for (int y = 0; y < terrain.HeightMap.Height; y++)
            {
                bool solid = terrain.HeightMap.IsSolid(x, y);
                Require(tiles.HasTile(new Vector3Int(x + cells.xMin, y + cells.yMin, 0)) == solid, "Tile occupancy differs from map data.");
                if (solid) occupied++;
            }
        Physics2D.SyncTransforms();
        Bounds bounds = terrain.Layout.WorldBounds;
        Require(Physics2D.Raycast(new Vector2(bounds.min.x + 1f, bounds.max.y - 1f), Vector2.left, 2f).collider != null, "Missing left wall.");
        Require(Physics2D.Raycast(new Vector2(bounds.max.x - 1f, bounds.max.y - 1f), Vector2.right, 2f).collider != null, "Missing right wall.");
        camera.Pan(new Vector2(3f, 0f));
        await Task.Delay(80);
        var recenter = root.Q<Button>("recenterPlayer");
        Require(!recenter.ClassListContains("hidden"), "Recenter did not appear.");
        recenter.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) recenter.SendEvent(submit);
        Require(!camera.IsBrowsing, "Recenter did not restore follow.");
        root.Q<TextField>("seedInput").value = "map-ui-verification";
        var generate = root.Q<Button>("generateMap");
        generate.Focus();
        using (var submit = NavigationSubmitEvent.GetPooled()) generate.SendEvent(submit);
        Require(terrain.CurrentSeed == "map-ui-verification", "Seed input failed.");
        return new { occupancyVerified = true, occupied, wallsVerified = true, seedUiVerified = true, recenterVerified = true };
    }
}
