using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Cameras;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.UI
{
    /// <summary>Binds the map controls to terrain generation and camera navigation.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapHudPresenter : MonoBehaviour
    {
        [SerializeField] private MapCameraController mapCamera;
        [SerializeField] private ProceduralTerrainMap terrain;
        private UIDocument document;
        private Button recenterButton;
        private Button generateButton;
        private Button randomizeButton;
        private TextField seedInput;
        private Label mapLabel;
        private Label seedLabel;
        private string displayedSeed;

        private void OnEnable() => document = GetComponent<UIDocument>();

        private void LateUpdate()
        {
            if (document.rootVisualElement == null) return;
            if (mapLabel == null || mapLabel.panel == null) BindControls();
            recenterButton?.EnableInClassList("hidden", mapCamera == null || !mapCamera.IsBrowsing);
            if (terrain == null) return;
            if (mapLabel != null)
                mapLabel.text = $"{terrain.Settings.WidthInBlocks} × {terrain.Settings.HeightInBlocks} blocks  |  16 × 16 px";
            if (displayedSeed == terrain.CurrentSeed) return;
            displayedSeed = terrain.CurrentSeed;
            if (seedLabel != null) seedLabel.text = $"Current seed: {displayedSeed}";
            seedInput?.SetValueWithoutNotify(displayedSeed);
        }

        private void BindControls()
        {
            UnbindControls();
            var root = document.rootVisualElement;
            recenterButton = root.Q<Button>("recenterPlayer");
            generateButton = root.Q<Button>("generateMap");
            randomizeButton = root.Q<Button>("randomizeMap");
            seedInput = root.Q<TextField>("seedInput");
            mapLabel = root.Q<Label>("mapDimensions");
            seedLabel = root.Q<Label>("currentSeed");
            if (recenterButton != null) recenterButton.clicked += Recenter;
            if (generateButton != null) generateButton.clicked += GenerateMap;
            if (randomizeButton != null) randomizeButton.clicked += RandomizeMap;
        }

        private void Recenter() => mapCamera?.RecenterOnPlayer();
        private void GenerateMap()
        {
            if (terrain == null || seedInput == null) return;
            terrain.Generate(seedInput.value);
            Recenter();
        }
        private void RandomizeMap()
        {
            terrain?.GenerateRandomMap();
            Recenter();
        }

        private void UnbindControls()
        {
            if (recenterButton != null) recenterButton.clicked -= Recenter;
            if (generateButton != null) generateButton.clicked -= GenerateMap;
            if (randomizeButton != null) randomizeButton.clicked -= RandomizeMap;
            recenterButton = generateButton = randomizeButton = null;
            mapLabel = seedLabel = null;
            seedInput = null;
            displayedSeed = null;
        }

        private void OnDisable() => UnbindControls();
    }
}
