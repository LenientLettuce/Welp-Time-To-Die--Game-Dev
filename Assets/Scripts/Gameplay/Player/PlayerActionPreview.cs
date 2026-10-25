using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WelpTimeToDie.Player
{
    /// <summary>Rehearses the selected action in an isolated 2D physics scene.</summary>
    [RequireComponent(typeof(PlayerActionController), typeof(PlayerActionSelection))]
    public sealed class PlayerActionPreview : MonoBehaviour
    {
        private PlayerActionController player;
        private PlayerActionSelection selection;
        private Scene predictionScene;
        private PhysicsScene2D predictionPhysics;
        private PlayerMovementMotor predictionMotor;
        private WelpTimeToDie.Levels.TerrainHeightMap cachedTerrain;
        private SpriteRenderer ghost;
        private LineRenderer path;
        private int cachedRevision = -1;
        private long cachedFrame = -1;
        private Vector2 cachedPosition, cachedVelocity;
        private int worldRevision;
        public Vector2 PredictedPosition { get; private set; }
        public int PredictedFrames { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerActionController>();
            selection = GetComponent<PlayerActionSelection>();
            var source = GetComponent<SpriteRenderer>();
            ghost = new GameObject("Predicted player").AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.sharedMaterial = source.sharedMaterial;
            ghost.color = new Color(0.4f, 0.95f, 1f, 0.28f);
            ghost.sortingOrder = 4;
            ghost.transform.localScale = transform.lossyScale;
            path = new GameObject("Predicted movement path").AddComponent<LineRenderer>();
            path.sharedMaterial = source.sharedMaterial;
            path.widthMultiplier = 0.04f;
            path.startColor = path.endColor = new Color(0.4f, 0.95f, 1f, 0.65f);
            path.sortingOrder = 4;
        }

        private void LateUpdate()
        {
            bool available = player.State.CanStart(selection.Move, player.Motor.IsGrounded);
            ghost.enabled = path.enabled = available;
            if (!available) { cachedRevision = -1; return; }
            var map = player.Motor.Terrain;
            if (map == null || map.HeightMap == null) return;
            bool mapChanged = cachedTerrain != map.HeightMap;
            if (mapChanged) RebuildWorld();
            if (cachedRevision != selection.Revision || cachedFrame != player.Clock.CurrentFrame || mapChanged ||
                cachedPosition != player.Motor.Body.position || cachedVelocity != player.Motor.Body.linearVelocity)
                Recalculate();
        }

        private void RebuildWorld()
        {
            ReleaseWorld();
            predictionScene = SceneManager.CreateScene("Player action prediction " + GetInstanceID() + " " + ++worldRevision, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            predictionPhysics = predictionScene.GetPhysicsScene2D();
            var terrain = player.Motor.Terrain;
            cachedTerrain = terrain.HeightMap;
            // Reuse the exact Tilemap collider geometry, including its corner contacts.
            var sourceGrid = terrain.Layout.Grid;
            var gridObject = new GameObject("Prediction terrain grid", typeof(Grid));
            SceneManager.MoveGameObjectToScene(gridObject, predictionScene);
            gridObject.transform.SetPositionAndRotation(sourceGrid.transform.position, sourceGrid.transform.rotation);
            gridObject.transform.localScale = sourceGrid.transform.lossyScale;
            var grid = gridObject.GetComponent<Grid>();
            grid.cellSize = sourceGrid.cellSize;
            grid.cellGap = sourceGrid.cellGap;
            grid.cellLayout = sourceGrid.cellLayout;
            grid.cellSwizzle = sourceGrid.cellSwizzle;
            var sourceTiles = terrain.GetComponentInChildren<WelpTimeToDie.Levels.TerrainTilemapRenderer>();
            var ground = new GameObject("Prediction terrain", typeof(UnityEngine.Tilemaps.Tilemap));
            SceneManager.MoveGameObjectToScene(ground, predictionScene);
            ground.transform.SetParent(gridObject.transform, false);
            ground.transform.localPosition = sourceTiles.transform.localPosition;
            ground.transform.localRotation = sourceTiles.transform.localRotation;
            ground.transform.localScale = sourceTiles.transform.localScale;
            var predictedTiles = ground.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            var groundCollider = ground.AddComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
            predictedTiles.tileAnchor = sourceTiles.Tilemap.tileAnchor;
            predictedTiles.SetTilesBlock(terrain.Layout.PlayableCells, sourceTiles.Tilemap.GetTilesBlock(terrain.Layout.PlayableCells));
            groundCollider.ProcessTilemapChanges();
            foreach (var wall in terrain.GetComponentsInChildren<BoxCollider2D>())
                AddStaticBox("Prediction boundary", wall.bounds.center, wall.bounds.size);
            var bodyObject = new GameObject("Prediction physics body");
            SceneManager.MoveGameObjectToScene(bodyObject, predictionScene);
            bodyObject.transform.localScale = transform.lossyScale;
            var collider = bodyObject.AddComponent<BoxCollider2D>();
            bodyObject.AddComponent<Rigidbody2D>();
            collider.size = player.Motor.BodyCollider.size;
            collider.offset = player.Motor.BodyCollider.offset;
            collider.sharedMaterial = player.Motor.BodyCollider.sharedMaterial;
            predictionMotor = bodyObject.AddComponent<PlayerMovementMotor>();
            predictionMotor.CopySettingsFrom(player.Motor);
            Physics2D.SyncTransforms();
        }

        private void AddStaticBox(string name, Vector2 center, Vector2 size)
        {
            var item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, predictionScene);
            item.transform.position = center;
            item.AddComponent<BoxCollider2D>().size = size;
        }

        private void Recalculate()
        {
            cachedRevision = selection.Revision;
            cachedFrame = player.Clock.CurrentFrame;
            cachedPosition = player.Motor.Body.position;
            cachedVelocity = player.Motor.Body.linearVelocity;
            predictionMotor.Body.simulated = false;
            predictionMotor.Teleport(player.Motor.Body.position);
            predictionMotor.Body.simulated = true;
            predictionMotor.Body.linearVelocity = player.Motor.Body.linearVelocity;
            var state = player.State.Copy();
            if (!state.TryStart(selection.Move, selection.Direction, selection.Strength, player.Motor.IsGrounded)) return;
            var points = new List<Vector3> { player.Motor.Body.position };
            int steps = player.Clock.PhysicsStepsPerFrame;
            float seconds = WelpTimeToDie.Combat.CombatPhysicsController.FrameDuration / steps;
            PredictedFrames = 0;
            while (state.IsBusy && PredictedFrames < 24)
            {
                state.BeginFrame();
                predictionMotor.BeginFrame(state);
                for (int step = 0; step < steps; step++)
                {
                    predictionMotor.PrepareStep(state, seconds);
                    Physics2D.SyncTransforms();
                    predictionPhysics.Simulate(seconds);
                }
                var completed = state.Move;
                if (state.FinishFrame()) predictionMotor.FinishAction(completed);
                points.Add(predictionMotor.Body.position);
                PredictedFrames++;
            }
            PredictedPosition = predictionMotor.Body.position;
            ghost.transform.position = PredictedPosition;
            path.positionCount = points.Count;
            path.SetPositions(points.ToArray());
        }

        private void ReleaseWorld()
        {
            Scene releasedScene = predictionScene;
            predictionScene = default;
            if (releasedScene.IsValid() && releasedScene.isLoaded && Application.isPlaying)
                SceneManager.UnloadSceneAsync(releasedScene);
            predictionMotor = null;
            cachedTerrain = null;
        }
        private void OnDisable()
        {
            if (ghost != null) ghost.enabled = false;
            if (path != null) path.enabled = false;
            ReleaseWorld();
        }
        private void OnDestroy()
        {
            ReleaseWorld();
            if (ghost != null) Destroy(ghost.gameObject);
            if (path != null) Destroy(path.gameObject);
        }
    }
}
