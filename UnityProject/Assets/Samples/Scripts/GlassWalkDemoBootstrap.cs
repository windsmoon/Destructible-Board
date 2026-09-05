using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Windsmoon.DesctructibleBoard.Samples
{
    public sealed class GlassWalkDemoBootstrap : MonoBehaviour
    {
        #region fields
        [Header("Destructible Board")]
        [SerializeField, Min(0.01f)]
        private float _glassRadius = 6f;
        [SerializeField, Min(0.01f)]
        private float _glassThickness = 0.16f;
        [SerializeField, Min(0.01f)]
        private float _fragmentSize = 0.66f;
        [SerializeField]
        private int _randomSeed = 240519;
        [SerializeField, Min(1)]
        private int _maxFragmentCount = 260;
        [SerializeField, Range(8, 64)]
        private int _circleSegments = 64;
        [SerializeField, Tooltip("Optional material override. Leave empty to use the demo's runtime glass material.")]
        private Material _glassMaterialOverride;

        [Header("Capsule Movement")]
        [SerializeField, Min(0.1f)]
        private float _moveSpeed = 4.2f;
        [SerializeField, Min(0.1f)]
        private float _moveAcceleration = 28f;
        [SerializeField, Min(0.1f)]
        private float _turnSpeed = 12f;

        [Header("Footstep Break")]
        [SerializeField, Min(0f)]
        private float _breakDelay = 0.65f;
        [SerializeField, Range(0, 2)]
        private int _neighborDepth = 1;
        [SerializeField, Min(0f)]
        private float _neighborDelay = 0.16f;
        [SerializeField, Min(0f)]
        private float _delayJitter = 0.12f;
        [SerializeField, Min(0.02f)]
        private float _stepInterval = 0.12f;
        [SerializeField, Min(0f)]
        private float _minimumPlanarSpeed = 0.2f;
        [SerializeField]
        private Color _warningColor = new Color(1f, 0.58f, 0.06f, 0.92f);
        [SerializeField]
        private bool _dropUnsupportedIslands = true;

        [Header("Falling Fragments")]
        [SerializeField, Min(0.001f)]
        private float _fragmentMass = 0.35f;
        [SerializeField, Min(0.1f)]
        private float _fragmentLifetime = 7f;

        private DestructibleBoard _board;
        private CapsuleWalker _walker;
        private DelayedGlassFootsteps _footsteps;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private float _nextAutomaticResetTime;
        #endregion

        #region unity methods
        private void Awake()
        {
            BuildDemo();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool manualReset = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            bool fellOutOfWorld = Time.unscaledTime >= _nextAutomaticResetTime &&
                                  _walker != null &&
                                  _walker.WorldPosition.y < -14f;
            if (manualReset || fellOutOfWorld)
            {
                ResetDemo();
            }
        }

        private void OnGUI()
        {
            EnsureGuiStyles();

            Rect panelRect = new Rect(24f, 24f, 355f, 116f);
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.09f, 0.86f);
            GUI.Box(panelRect, GUIContent.none);
            GUI.color = previousColor;

            GUI.Label(new Rect(42f, 36f, 320f, 30f), "DELAYED GLASS WALK", _titleStyle);
            GUI.Label(new Rect(42f, 68f, 320f, 48f), "WASD / Arrow Keys   Move\nR   Reset the glass", _bodyStyle);

            int total = _board != null ? _board.SamplePointCount : 0;
            int broken = _footsteps != null ? _footsteps.BrokenCellCount : 0;
            int pending = _footsteps != null ? _footsteps.PendingCellCount : 0;
            GUI.Label(
                new Rect(42f, 112f, 320f, 22f),
                $"Glass: {Mathf.Max(0, total - broken)}/{total}    Primed: {pending}",
                _bodyStyle);
        }
        #endregion

        #region methods
        private void BuildDemo()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.055f, 0.15f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.018f, 0.012f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.055f, 0.025f, 0.16f);
            RenderSettings.fogDensity = 0.012f;

            Material glassMaterial = _glassMaterialOverride != null
                ? _glassMaterialOverride
                : CreateGlassMaterial();
            Material capsuleMaterial = CreateLitMaterial(
                "Capsule Material",
                new Color(1f, 0.72f, 0.12f),
                0.52f,
                0.86f);
            Material rimMaterial = CreateUnlitMaterial(
                "Neon Rim Material",
                new Color(1f, 0.025f, 0.34f));

            CreateLighting();
            CreateCamera();
            CreateGlassBoard(glassMaterial);
            CreateNeonRim(rimMaterial, _glassRadius + 0.12f);
            CreateCapsule(capsuleMaterial);
            CreateVoidMarkers(rimMaterial);
        }

        private void CreateGlassBoard(Material material)
        {
            GameObject boardObject = new GameObject("Runtime Circular Glass");
            boardObject.SetActive(false);
            boardObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            _board = boardObject.AddComponent<DestructibleBoard>();
            _board.ConfigureCircle(
                _glassRadius,
                _glassThickness,
                _fragmentSize,
                _randomSeed,
                _maxFragmentCount,
                _circleSegments,
                material);

            // The reusable board does not choose its own initialization time.
            // This demo generates explicitly after all runtime settings are applied.
            boardObject.SetActive(true);
            _board.Generate();
        }

        private void CreateCapsule(Material material)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Player Capsule";
            capsule.transform.position = new Vector3(0f, 1.16f, -2.4f);
            capsule.GetComponent<MeshRenderer>().sharedMaterial = material;

            Rigidbody body = capsule.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            _walker = capsule.AddComponent<CapsuleWalker>();
            _footsteps = capsule.AddComponent<DelayedGlassFootsteps>();
            _walker.Configure(_moveSpeed, _moveAcceleration, _turnSpeed);
            _footsteps.Configure(
                _breakDelay,
                _neighborDepth,
                _neighborDelay,
                _delayJitter,
                _stepInterval,
                _minimumPlanarSpeed,
                _fragmentMass,
                _fragmentLifetime,
                _warningColor,
                _dropUnsupportedIslands);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 11.5f, -13.5f);
            cameraObject.transform.LookAt(new Vector3(0f, -0.15f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.012f, 0.12f);
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateLighting()
        {
            GameObject keyObject = new GameObject("Key Light");
            keyObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.56f, 0.82f, 1f);
            key.intensity = 2.1f;
            key.shadows = LightShadows.Soft;

            CreatePointLight("Cyan Fill", new Vector3(-4.5f, 3f, -2f), new Color(0.08f, 0.9f, 1f), 18f, 7f);
            CreatePointLight("Magenta Fill", new Vector3(4f, 2.5f, 3f), new Color(1f, 0.04f, 0.48f), 16f, 7f);
        }

        private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void CreateNeonRim(Material material, float radius)
        {
            GameObject rimObject = new GameObject("Neon Safety Rim");
            LineRenderer rim = rimObject.AddComponent<LineRenderer>();
            const int segmentCount = 128;
            rim.loop = true;
            rim.useWorldSpace = false;
            rim.positionCount = segmentCount;
            rim.widthMultiplier = 0.11f;
            rim.numCornerVertices = 3;
            rim.numCapVertices = 3;
            rim.sharedMaterial = material;
            rim.shadowCastingMode = ShadowCastingMode.Off;
            rim.receiveShadows = false;

            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index * Mathf.PI * 2f / segmentCount;
                rim.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, 0.12f, Mathf.Sin(angle) * radius));
            }
        }

        private static void CreateVoidMarkers(Material material)
        {
            for (int index = 0; index < 14; index++)
            {
                float angle = index * Mathf.PI * 2f / 14f + 0.18f;
                float radius = 8.4f + (index % 3) * 0.7f;
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Background Marker";
                marker.transform.position = new Vector3(
                    Mathf.Cos(angle) * radius,
                    -2.4f - index % 4,
                    Mathf.Sin(angle) * radius);
                marker.transform.localScale = Vector3.one * (0.08f + index % 3 * 0.035f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = material;
                Destroy(marker.GetComponent<Collider>());
            }
        }

        private void ResetDemo()
        {
            if (_board == null || _walker == null || _footsteps == null)
            {
                return;
            }

            _footsteps.ResetState();
            _board.Generate();
            _walker.ResetToSpawn();
            _nextAutomaticResetTime = Time.unscaledTime + 1f;
        }

        private void EnsureGuiStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.95f, 1f) },
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.94f, 1f) },
            };
        }

        private static Material CreateGlassMaterial()
        {
            Material material = CreateLitMaterial(
                "Runtime Glass Material",
                new Color(0.16f, 0.64f, 0.88f, 0.62f),
                0.18f,
                0.94f);

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.015f, 0.15f, 0.22f));
            return material;
        }

        private static Material CreateLitMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader)
            {
                name = name,
                color = color,
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material CreateUnlitMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader)
            {
                name = name,
                color = color,
            };
            material.SetColor("_BaseColor", color);
            return material;
        }
        #endregion
    }
}
