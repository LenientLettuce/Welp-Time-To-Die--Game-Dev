from pathlib import Path

base = Path('Assets/RectanglePrototype/Scripts')
p = base / 'RectangleArena.cs'
s = p.read_text()
s = s.replace('private RectangleCombat world, rehearsalFrame, playbackOrigin;', '''private RectangleCombat world, rehearsalFrame, playbackOrigin;
        [SerializeField] private UnityEngine.UIElements.VisualTreeAsset hudAsset;
        [SerializeField] private PanelSettings savedPanel;
        [SerializeField] private WelpTimeToDie.Levels.ProceduralTerrainMap terrain;
        [SerializeField] private WelpTimeToDie.Cameras.MapCameraController mapCamera;
        private float strength = 1;
        private bool paused;
        private VisualElement root;
        public RectangleCombat World => world;
        public RectanglePlan CurrentPlan => plan;
        public bool IsPlaying => playing;
        public bool IsWatching => watching;
        public bool IsPaused => paused;
        public UIDocument Document => document;''')
start = s.index('            panel = ScriptableObject.CreateInstance<PanelSettings>();')
end = s.index('        private void Restart()')
s = s[:start] + '''            document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = savedPanel;
            document.visualTreeAsset = hudAsset;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            status = root.Q<Label>("arenaStatus"); help = root.Q<Label>("arenaHelp");
            foreach (CombatMove move in new[] { CombatMove.Move, CombatMove.Punch, CombatMove.Uppercut,
                CombatMove.Kick, CombatMove.DashKick, CombatMove.Block, CombatMove.Wait, CombatMove.Dash, CombatMove.Jump })
            {
                var choice = move;
                var button = root.Q<Button>("move" + move);
                button.clicked += () => Select(choice);
                moves.Add(move, button);
            }
            goButton = root.Q<Button>("goAction"); goButton.clicked += Commit;
            previewButton = root.Q<Button>("watchPreview"); previewButton.clicked += WatchPreview;
            root.Q<Button>("resetEncounter").clicked += Restart;
            root.Q<Button>("pauseAction").clicked += () => paused = !paused;
            root.Q<Button>("stepFrame").clicked += StepFrame;
            root.Q<Button>("aimLeft").clicked += () => Aim(-1);
            root.Q<Button>("aimRight").clicked += () => Aim(1);
            root.Q<Slider>("dashPower").RegisterValueChangedCallback(e => { strength = e.newValue; RefreshPrediction(); });
            if (terrain != null) terrain.Generated += Restart;
            Restart();
        }

        private bool IsOverHud(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var picked = root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y)));
            return picked != null && picked != root && picked.name != "arenaRoot";
        }

        public void Aim(float facing)
        {
            if (playing || watching) return;
            direction = facing < 0 ? -1 : 1;
            RefreshPrediction();
        }

        public void StepFrame()
        {
            if (watching || world.Finished) return;
            if (!playing) Commit();
            if (!playing) return;
            paused = true;
            world = activePlan.Frames[playbackIndex++].Copy();
            playbackTime = 0;
            if (playbackIndex == activePlan.Frames.Count) { playing = false; RefreshPrediction(); }
            else Render();
            stage.AdvanceRig(1f / 12);
        }

''' + s[end:]
s = s.replace('private void Restart()', 'public void Restart()').replace('private void Select(CombatMove move)', 'public void Select(CombatMove move)').replace('private void WatchPreview()', 'public void WatchPreview()').replace('private void Commit()', 'public void Commit()')
s = s.replace('            world = RectangleCombat.CreateBasic', '            stage.EndPreview();\n            paused = false;\n            world = RectangleCombat.CreateBasic')
s = s.replace('            stage.SetStartingPositions(world);', '''            if (terrain != null && terrain.HeightMap != null)
            {
                world.Terrain = new RectangleTerrain(terrain);
                float center = terrain.Layout.WorldBounds.center.x;
                world.Player.X = center - 1.5f;
                world.Player.Y = world.Terrain.Surface(world.Player.X);
                world.Player.TerrainGrounded = true;
                var golem = world.Fighters[1];
                golem.X = Mathf.Min(world.Terrain.MaxX - 0.6f, center + 1.5f);
                golem.Y = world.Terrain.Surface(golem.X); golem.TerrainGrounded = true;
                golem.Name = "GOLEM";
                golem.Move = CombatMove.None;
                System.Array.Clear(golem.Cooldowns, 0, golem.Cooldowns.Length);
                world.PlanEnemies();
            }
            else stage.SetStartingPositions(world);
            stage.ResetRig();
            mapCamera?.RecenterOnPlayer();''')
s = s.replace('if (playing || watching || !world.CanStart(0, move)) return;', 'if (playing || watching || world.Finished) return;')
s = s.replace('                watching = false;\n                rehearsalFrame = null;', '                watching = false;\n                stage.EndPreview();\n                rehearsalFrame = null;')
s = s.replace('            watching = true;', '            stage.BeginPreview();\n            watching = true;\n            paused = false;')
s = s.replace('RectanglePlan.Create(world, selected, direction)', 'RectanglePlan.Create(world, selected, direction, strength)')
s = s.replace('            playing = true;', '            playing = true;\n            paused = false;')
s = s.replace('            if (keyboard != null)', '            if (keyboard != null && !(root?.focusController?.focusedElement is TextField) && !(root?.focusController?.focusedElement is TextElement))')
s = s.replace('                    watching = false;\n                    rehearsalFrame = null;', '                    watching = false;\n                    stage.EndPreview();\n                    rehearsalFrame = null;')
s = s.replace('if (stage.ArenaCamera.pixelRect.Contains(screen))', 'if (stage.ArenaCamera.pixelRect.Contains(screen) && !IsOverHud(screen))')
s = s.replace('            playbackTime += Time.unscaledDeltaTime;', '''            if (paused) { Render(); return; }
            stage.AdvanceRig(Time.unscaledDeltaTime);
            playbackTime += Time.unscaledDeltaTime;''')
s = s.replace('                pair.Value.style.opacity = available ? 1 : 0.4f;\n                pair.Value.style.backgroundColor = selected == pair.Key ? new Color(0.12f, 0.36f, 0.48f) : new Color(0.14f, 0.18f, 0.24f);', '                pair.Value.EnableInClassList("selected", selected == pair.Key);')
s = s.replace('pair.Value.SetEnabled(available);', 'pair.Value.SetEnabled(!playing && !watching && !world.Finished);\n                pair.Value.tooltip = available ? RectangleCombat.Spec(world.Player, pair.Key).Name : "Recharging or requires ground contact";')
s = s.replace('            stage.Draw(shown,', '''            root.Q<Label>("frameCount").text = $"Frame {shown.Frame} · 24 Hz combat / 12 Hz playback";
            root.Q<Button>("generateMap").SetEnabled(!playing && !watching);
            root.Q<Button>("randomizeMap").SetEnabled(!playing && !watching);
            root.Q<Slider>("dashPower").SetEnabled(!playing && !watching);
            if (terrain != null && shown.Terrain != null)
            {
                if (watching) terrain.Features.Visuals.RenderPreview(shown.Terrain.State);
                else if (renderedTerrain != shown.Terrain.State)
                { terrain.Features.Visuals.RenderCommitted(shown.Terrain.State); renderedTerrain = shown.Terrain.State; }
            }
            stage.Draw(shown,''')
s = s.replace('        private void Render()', '        private WelpTimeToDie.Levels.TerrainFeatureState renderedTerrain;\n        private void Render()')
s = s.replace('            if (document != null) { document.enabled = false; Destroy(document); }', '            if (terrain != null) { terrain.Generated -= Restart; terrain.Features?.ClearPreview(); }\n            stage?.EndPreview();')
p.write_text(s)

p = base / 'RectanglePlan.cs'; s = p.read_text().replace('CombatMove move, float direction)', 'CombatMove move, float direction, float strength = 1)').replace('simulation.Start(0, move, direction, 0);', 'simulation.Start(0, move, direction, 0, strength);'); p.write_text(s)

p = base / 'GolemPhysicsAnimator.cs'; s = p.read_text()
# The package accidentally nests its namespace twice. Preserve its actual solver.
s = s.replace('namespace WelpTimeToDie.RectanglePrototype\n{\n    using System;\n    using UnityEngine;\n\n    namespace WelpTimeToDie.RectanglePrototype\n    {', 'using System;\nnamespace WelpTimeToDie.RectanglePrototype\n{')
s = s.rstrip(); s = s[:-1].rstrip() # remove the extra outer closing brace
s = s.replace('            private void Update() => Simulate(Mathf.Min(Time.unscaledDeltaTime, 0.1f));', '''            // Deliberately no Update: the arena advances the rig only during playback.
            public void ResetRig() { lastFrame = -1; lastHitFrame = -1; hasRootSample = false; }
            private float[] savedState;
            private Vector3 savedRootPosition, savedLastRoot;
            private Vector3 savedScale;
            private Vector2 savedVelocity, savedAcceleration;
            private bool savedHasSample, savedAlive;
            private int savedFrame, savedHit, savedDeathSide;
            private Color[] savedColors;
            public void BeginPreview()
            {
                if (!EnsureRig() || savedState != null) return;
                savedState = new float[4 * 4 + 5];
                for (int i = 0; i < 4; i++) { savedState[i*4] = bodies[i].Angle; savedState[i*4+1] = bodies[i].Velocity; savedState[i*4+2] = bodies[i].Target; savedState[i*4+3] = bodies[i].Acceleration; }
                savedState[16] = hip; savedState[17] = hipVelocity; savedState[18] = hipTarget; savedState[19] = stiffness; savedState[20] = accumulator;
                savedRootPosition = transform.position; savedScale = transform.localScale; savedLastRoot = lastRootPosition;
                savedVelocity = rootVelocity; savedAcceleration = rootAcceleration; savedHasSample = hasRootSample; savedAlive = wasAlive;
                savedFrame = lastFrame; savedHit = lastHitFrame; savedDeathSide = deathSide;
                savedColors = new Color[parts.Length]; for (int i = 0; i < parts.Length; i++) savedColors[i] = parts[i].color;
            }
            public void EndPreview()
            {
                if (savedState == null) return;
                for (int i = 0; i < 4; i++) { bodies[i].Angle = savedState[i*4]; bodies[i].Velocity = savedState[i*4+1]; bodies[i].Target = savedState[i*4+2]; bodies[i].Acceleration = savedState[i*4+3]; }
                hip = savedState[16]; hipVelocity = savedState[17]; hipTarget = savedState[18]; stiffness = savedState[19]; accumulator = savedState[20];
                transform.position = savedRootPosition; transform.localScale = savedScale; lastRootPosition = savedLastRoot;
                rootVelocity = savedVelocity; rootAcceleration = savedAcceleration; hasRootSample = savedHasSample; wasAlive = savedAlive;
                lastFrame = savedFrame; lastHitFrame = savedHit; deathSide = savedDeathSide;
                for (int i = 0; i < parts.Length; i++) parts[i].color = savedColors[i];
                savedState = null; Apply();
            }''')
p.write_text(s)

p = base / 'RectangleStage.cs'; s = p.read_text()
s = s.replace('        private readonly List<SpriteRenderer>', '''        [SerializeField] private GolemPhysicsAnimator enemyGolem;
        [SerializeField] private bool useMapCamera;
        public void AdvanceRig(float seconds) => enemyGolem?.Simulate(Mathf.Min(seconds, 0.1f));
        public void BeginPreview() => enemyGolem?.BeginPreview();
        public void EndPreview() => enemyGolem?.EndPreview();
        public void ResetRig() => enemyGolem?.ResetRig();
        private readonly List<SpriteRenderer>''')
s = s.replace('            DrawBody(enemyBody, world.Fighters[1], next?.Fighters[1], blend, Red);', '''            if (enemyGolem != null)
            {
                var fighter = world.Fighters[1].Copy();
                fighter.X = Mathf.Lerp(fighter.X, next?.Fighters[1].X ?? fighter.X, blend);
                fighter.Y = Mathf.Lerp(fighter.Y, next?.Fighters[1].Y ?? fighter.Y, blend);
                enemyGolem.Show(fighter, world.Frame, preview);
                Box(fighter.X, fighter.Y + 2.1f, 1.1f, 0.07f, new Color(0.2f, 0.25f, 0.3f));
                float width = 1.1f * fighter.Health / fighter.MaxHealth;
                if (width > 0) Box(fighter.X - 0.55f + width / 2, fighter.Y + 2.1f, width, 0.07f, Red);
                enemyBody.enabled = false;
            }
            else DrawBody(enemyBody, world.Fighters[1], next?.Fighters[1], blend, Red);''')
s = s.replace('            FitCamera(framing ?? world);', '            if (!useMapCamera) FitCamera(framing ?? world);')
p.write_text(s)
