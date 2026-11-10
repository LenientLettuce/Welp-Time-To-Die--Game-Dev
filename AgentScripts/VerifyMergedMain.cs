using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.RectanglePrototype;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Cameras;

public static class VerifyMergedMain
{
    public static async Task<object> Pointer()
    {
        var arena = UnityEngine.Object.FindFirstObjectByType<RectangleArena>();
        var root = arena.Document.rootVisualElement;
        arena.Restart(); arena.Aim(-1);
        var original = UnityEngine.InputSystem.Mouse.current;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Merge pointer verification");
        try
        {
            mouse.MakeCurrent();
            var point = new Vector2(Screen.width * 0.75f, Screen.height * 0.7f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState {position=point}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            await Task.Delay(150);
            Require(root.Q<Label>("arenaHelp").text.Contains("right"),"World pointer did not aim right");
            Require(arena.World.Frame==0 && !arena.IsPlaying,"World aiming committed an action");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=point});
            await Task.Delay(100);
            arena.Aim(-1);
            Vector2 panel = root.Q<Button>("moveWait").worldBound.center;
            point = new Vector2(panel.x * Screen.width / root.resolvedStyle.width,
                Screen.height - panel.y * Screen.height / root.resolvedStyle.height);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,
                new UnityEngine.InputSystem.LowLevel.MouseState {position=point}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            await Task.Delay(150);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=point});
            await Task.Delay(150);
            Require(arena.CurrentPlan.MoveName=="Hold","Pointer did not click HUD move");
            Require(root.Q<Label>("arenaHelp").text.Contains("left"),"HUD click changed world aim");
            Require(arena.World.Frame==0,"HUD move selection advanced combat");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=point});
            await Task.Delay(80);
            arena.Restart();
            return new {worldMouseAim=true,hudMouseClick=true,hudDoesNotAimWorld=true};
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
            await Task.Delay(80);
            UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            if(original!=null) original.MakeCurrent();
        }
    }
    private static void Require(bool pass, string reason) { if (!pass) throw new Exception(reason); }
    private static void Click(VisualElement root, string name)
    {
        var button = root.Q<Button>(name); Require(button != null && button.enabledInHierarchy, "Button unavailable: " + name);
        button.Focus(); using(var e = NavigationSubmitEvent.GetPooled()) button.SendEvent(e);
    }
    private static string Signature(RectangleCombat world) => world.Frame + ":" + world.Terrain.State.Revision + ":" +
        string.Join(";", world.Fighters.Select(f => $"{f.X:R},{f.Y:R},{f.Health},{f.Move},{f.Elapsed},{f.Stun}," + string.Join(",",f.Cooldowns)));
    public static async Task<object> Main()
    {
        var arena = UnityEngine.Object.FindFirstObjectByType<RectangleArena>();
        var rig = UnityEngine.Object.FindFirstObjectByType<GolemPhysicsAnimator>();
        var map = UnityEngine.Object.FindFirstObjectByType<ProceduralTerrainMap>();
        var camera = UnityEngine.Object.FindFirstObjectByType<MapCameraController>();
        Require(arena != null && rig != null, "Merged objects missing");
        var root = arena.Document.rootVisualElement;
        arena.Restart();
        int firstFrame = Time.frameCount; await Task.Delay(200);
        Require(Time.frameCount > firstFrame, "Player loop is frozen");
        var bones = rig.GetComponentsInChildren<Transform>();
        var rotations = bones.Select(t => t.localRotation).ToArray();
        string idle = Signature(arena.World); await Task.Delay(300);
        Require(Signature(arena.World) == idle, "Idle combat advanced");
        Require(bones.Select((t,i) => t.localRotation == rotations[i]).All(v=>v), "Idle rig did not freeze immediately");
        Require(arena.World.Fighters.Count == 2, "Expected one enemy");
        var renderers = rig.GetComponentsInChildren<SpriteRenderer>();
        var bounds = renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        Require(Mathf.Abs(bounds.min.y-arena.World.Fighters[1].Y)<0.12f,"Golem art floats or sinks into terrain");
        Require(bounds.size.y>1.6f && bounds.size.y<2.1f,"Golem art scale differs from combat body");
        root.Q<Foldout>("mapOptions").value = true;
        await Task.Delay(100);
        Require(root.Q<TextField>("seedInput").resolvedStyle.display != DisplayStyle.None, "Map panel missing");
        root.Q<TextField>("seedInput").value = "merge-runtime-check";
        Click(root,"generateMap"); await Task.Delay(150);
        Require(map.CurrentSeed == "merge-runtime-check", "Seed controls failed");
        var heights = Enumerable.Range(0,map.HeightMap.Width).Select(map.HeightMap.GetColumnHeight).ToArray();
        Click(root,"generateMap"); await Task.Delay(100);
        Require(heights.SequenceEqual(Enumerable.Range(0,map.HeightMap.Width).Select(map.HeightMap.GetColumnHeight)), "Seed did not repeat");
        camera.Pan(Vector2.right * 4); Require(camera.IsBrowsing,"Camera browse failed"); await Task.Delay(100);
        Click(root,"recenterPlayer"); Require(!camera.IsBrowsing,"Camera recenter failed");
        float zoom = camera.Zoom; camera.ZoomBy(1, new Vector2(Screen.width/2,Screen.height/2)); Require(camera.Zoom < zoom,"Zoom failed");
        await VerifyPreview(arena, rig);
        Click(root,"resetEncounter"); Click(root,"moveJump");
        var jump = arena.CurrentPlan; Require(jump != null,"Jump unavailable at spawn");
        Click(root,"stepFrame"); Require(arena.World.Frame==1 && arena.IsPaused,"Single step failed");
        string before = Signature(arena.World); await Task.Delay(200); Require(Signature(arena.World)==before,"Paused playback advanced");
        for(int i=1;i<jump.Frames.Count;i++) Click(root,"stepFrame");
        Require(Signature(arena.World)==Signature(jump.Frames.Last()),"Stepped jump differs from rehearsal");
        Click(root,"resetEncounter");
        root.Q<Foldout>("mapOptions").value = false;
        return new { ticking=true, oneGolem=true, rigFreezes=true, previewLoopsWithKeyPoses=true, previewPreservesCombatAndBones=true,
            goMatchesPreview=true, jumpAndFrameStep=true, seedRepeatable=true, cameraBrowseZoomRecenter=true };
    }

    public static async Task<object> Preview()
    {
        var arena = UnityEngine.Object.FindFirstObjectByType<RectangleArena>();
        var rig = UnityEngine.Object.FindFirstObjectByType<GolemPhysicsAnimator>();
        var options = arena.Document.rootVisualElement.Q<Foldout>("mapOptions");
        bool expanded = options.value;
        try
        {
            arena.Restart();
            options.value = true;
            await Task.Delay(150);
            await VerifyPreview(arena, rig);
            return new { previewLoopsWithKeyPoses=true, previewPreservesCombatAndBones=true, goMatchesPreview=true };
        }
        finally
        {
            arena.Restart();
            options.value = expanded;
        }
    }

    private static async Task VerifyPreview(RectangleArena arena, GolemPhysicsAnimator rig)
    {
        var root = arena.Document.rootVisualElement;
        var bones = rig.GetComponentsInChildren<Transform>();
        var preview = root.Q<Toggle>("showPreview");
        Require(preview.value && arena.IsWatching, "Preview should start checked and loop automatically");
        Click(root,"moveDash"); Click(root,"aimLeft");
        var leftDash = arena.CurrentPlan;
        Click(root,"aimRight");
        Require(arena.CurrentPlan != leftDash, "Aiming did not refresh the active preview");
        var fullDash = arena.CurrentPlan;
        root.Q<Slider>("dashPower").value = 0.4f;
        Require(arena.CurrentPlan != fullDash, "Changing dash strength did not refresh the active preview");
        root.Q<Slider>("dashPower").value = 1f;
        Click(root,"moveJump");
        var plan = arena.CurrentPlan; Require(plan != null,"No rehearsal");
        string before = Signature(arena.World);
        var rotations = bones.Select(t => t.localRotation).ToArray();
        await Task.Delay(100);
        var ghost = GameObject.Find("Player preview");
        var key = GameObject.Find("Player preview key pose 1");
        Require(ghost != null && key != null, "Animated ghost and key pose should be visible");
        var initialGhostPosition = ghost.transform.position;
        var keyPosition = key.transform.position;
        await Task.Delay(200);
        Require(ghost.transform.position != initialGhostPosition, "Ghost pose did not move");
        await Task.Delay(2200);
        Require(arena.IsWatching && arena.CurrentPlan == plan, "Preview should keep looping the current plan");
        Require(key.activeInHierarchy && key.transform.position == keyPosition, "Key pose changed during the loop");
        Require(Signature(arena.World) == before,"Preview mutated live combat");
        Require(bones.Select((t,i) => t.localRotation == rotations[i]).All(v=>v), "Preview moved live bones");
        preview.value = false;
        Require(!arena.IsWatching && !ghost.activeInHierarchy && !key.activeInHierarchy, "Checkbox did not hide ghosts");
        preview.value = true;
        await Task.Delay(120);
        Require(arena.IsWatching && ghost.activeInHierarchy, "Checkbox did not resume the loop");
        Click(root,"goAction"); Require(arena.IsPlaying,"Go button failed");
        Require(!arena.IsWatching && !ghost.activeInHierarchy, "Ghosts stayed visible during committed playback");
        await Task.Delay(2600);
        Require(!arena.IsPlaying,"Go did not finish");
        Require(Signature(arena.World)==Signature(plan.Frames.Last()),"Go differs from preview");
        rotations = bones.Select(t=>t.localRotation).ToArray();
        await Task.Delay(250);
        Require(bones.Select((t,i)=>t.localRotation==rotations[i]).All(v=>v),"Rig continued settling after Go");
    }
}
