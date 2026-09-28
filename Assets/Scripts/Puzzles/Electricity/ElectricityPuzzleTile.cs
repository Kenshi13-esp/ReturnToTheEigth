using System.Collections.Generic;
using UnityEngine;

namespace ReturnToTheEigth.Puzzles.Electricity
{
    /// <summary>Stores one rotatable wire layout and draws its editable frame and cardinal wire segments.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectricityPuzzleTile : MonoBehaviour
    {
        private const int PortMask = 15;
        private const int FullTurnSteps = 4;
        private const int NoRotation = 0;
        private const float HalfTurn = 180f;
        private const float QuarterTurn = 90f;
        private const float DefaultHalfCellSize = 0.46f;
        private const float DefaultWireReach = 0.4f;
        private const float DefaultWireWidth = 0.055f;
        private const float DefaultFrameWidth = 0.045f;
        private const int FramePointCount = 4;
        private const int WirePointCount = 2;
        private const int FirstIndex = 0;
        private const int SortingOrder = 2;
        private const string LineShaderName = "Universal Render Pipeline/Unlit";
        private const string FallbackLineShaderName = "Sprites/Default";
        private const string FrameObjectName = "TileFrame";
        private const string WireNorthName = "WireNorth";
        private const string WireEastName = "WireEast";
        private const string WireSouthName = "WireSouth";
        private const string WireWestName = "WireWest";
        private static readonly Vector2[] CardinalDirections =
        {
            Vector2.up,
            Vector2.right,
            Vector2.down,
            Vector2.left
        };
        private static readonly int[] PortBits = { 1, 2, 4, 8 };
        private static readonly Color FrameColor = new Color(0.08f, 0.08f, 0.055f, 1f);
        private static readonly Color PoweredFrameColor = PuzzleInteractionPalette.GetHighlightedColor(Color.white);
        private static readonly Color UnpoweredWireColor = new Color(0.08f, 0.085f, 0.075f, 1f);
        private static readonly Color PoweredWireColor = PuzzleInteractionPalette.GetHighlightedColor(Color.white);
        private static readonly Vector2 ZeroPoint = Vector2.zero;
        private static Material sharedLineMaterial;

        [SerializeField] private Vector2Int cell;
        [SerializeField] private bool isFixed;
        [SerializeField, Range(0, PortMask)] private int basePortMask;
        [SerializeField, Range(NoRotation, FullTurnSteps - 1)] private int rotationSteps;
        [SerializeField, Min(0.01f)] private float halfCellSize = DefaultHalfCellSize;
        [SerializeField, Min(0.01f)] private float wireReach = DefaultWireReach;
        [SerializeField, Min(0.001f)] private float wireWidth = DefaultWireWidth;
        [SerializeField, Min(0.001f)] private float frameWidth = DefaultFrameWidth;

        private readonly LineRenderer[] wireRenderers = new LineRenderer[FullTurnSteps];
        private readonly List<Material> runtimeMaterials = new List<Material>(FullTurnSteps + 1);
        private LineRenderer frameRenderer;
        private bool isPowered;

        /// <summary>Gets the grid coordinate of this tile.</summary>
        public Vector2Int Cell => cell;

        /// <summary>Gets whether this tile is fixed and cannot be rotated.</summary>
        public bool IsFixed => isFixed;

        /// <summary>Gets the cardinal ports after applying the current clockwise rotation.</summary>
        public ElectricityPorts CurrentPorts => RotateMask((ElectricityPorts)basePortMask, rotationSteps);

        /// <summary>Gets whether this tile is currently energized from input A.</summary>
        public bool IsPowered => isPowered;

        private void Awake()
        {
            CreateWireVisuals();
            ApplyRotation();
            SetPowered(false);
        }

        private void OnDestroy()
        {
            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            runtimeMaterials.Clear();
        }

        /// <summary>Rotates this wire tile 90 degrees clockwise and updates its scene transform.</summary>
        public bool RotateClockwise()
        {
            if (isFixed)
            {
                return false;
            }

            rotationSteps = (rotationSteps + 1) % FullTurnSteps;
            ApplyRotation();
            return true;
        }

        /// <summary>Changes wire tint to show whether current flows through this tile.</summary>
        public void SetPowered(bool powered)
        {
            isPowered = powered;
            Color wireColor = powered ? PoweredWireColor : UnpoweredWireColor;
            if (frameRenderer != null)
            {
                Color frameColor = powered ? PoweredFrameColor : FrameColor;
                frameRenderer.startColor = frameColor;
                frameRenderer.endColor = frameColor;
                frameRenderer.sharedMaterial.color = frameColor;
            }
            for (int index = FirstIndex; index < wireRenderers.Length; index++)
            {
                LineRenderer renderer = wireRenderers[index];
                if (renderer != null)
                {
                    renderer.startColor = wireColor;
                    renderer.endColor = wireColor;
                    renderer.sharedMaterial.color = wireColor;
                }
            }
        }

        private void CreateWireVisuals()
        {
            CreateFrameRenderer();
            CreateWireRenderer(FirstIndex, WireNorthName);
            CreateWireRenderer(FirstIndex + 1, WireEastName);
            CreateWireRenderer(FirstIndex + 2, WireSouthName);
            CreateWireRenderer(FirstIndex + 3, WireWestName);
        }

        private void CreateFrameRenderer()
        {
            frameRenderer = CreateLineRenderer(FrameObjectName, FramePointCount, frameWidth);
            Vector3[] points =
            {
                new Vector3(-halfCellSize, -halfCellSize, 0f),
                new Vector3(-halfCellSize, halfCellSize, 0f),
                new Vector3(halfCellSize, halfCellSize, 0f),
                new Vector3(halfCellSize, -halfCellSize, 0f)
            };
            for (int index = FirstIndex; index < points.Length; index++)
            {
                frameRenderer.SetPosition(index, points[index]);
            }
            frameRenderer.loop = true;
            frameRenderer.startColor = FrameColor;
            frameRenderer.endColor = FrameColor;
        }

        private void CreateWireRenderer(int index, string objectName)
        {
            LineRenderer renderer = CreateLineRenderer(objectName, WirePointCount, wireWidth);
            Vector2 direction = CardinalDirections[index];
            renderer.SetPosition(FirstIndex, Vector3.zero);
            renderer.SetPosition(FirstIndex + 1, direction * wireReach);
            renderer.startColor = UnpoweredWireColor;
            renderer.endColor = UnpoweredWireColor;
            renderer.enabled = ((basePortMask & PortBits[index]) != 0);
            wireRenderers[index] = renderer;
        }

        private LineRenderer CreateLineRenderer(string objectName, int pointCount, float width)
        {
            GameObject visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(transform, false);
            LineRenderer renderer = visualObject.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.positionCount = pointCount;
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.sortingOrder = SortingOrder;
            Material material = new Material(GetLineMaterial());
            runtimeMaterials.Add(material);
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, -rotationSteps * QuarterTurn);
        }

        private static ElectricityPorts RotateMask(ElectricityPorts ports, int clockwiseSteps)
        {
            int mask = (int)ports & PortMask;
            for (int index = FirstIndex; index < clockwiseSteps; index++)
            {
                mask = ((mask << 1) & PortMask) | (mask >> 3);
            }
            return (ElectricityPorts)mask;
        }

        private static Material GetLineMaterial()
        {
            if (sharedLineMaterial != null)
            {
                return sharedLineMaterial;
            }

            Shader shader = Shader.Find(LineShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackLineShaderName);
            }
            if (shader != null)
            {
                sharedLineMaterial = new Material(shader);
                sharedLineMaterial.color = Color.white;
            }
            return sharedLineMaterial;
        }

        private void OnValidate()
        {
            basePortMask &= PortMask;
            rotationSteps = (rotationSteps % FullTurnSteps + FullTurnSteps) % FullTurnSteps;
            halfCellSize = Mathf.Max(0.01f, halfCellSize);
            wireReach = Mathf.Max(0.01f, wireReach);
            wireWidth = Mathf.Max(0.001f, wireWidth);
            frameWidth = Mathf.Max(0.001f, frameWidth);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = FrameColor;
            Vector3 bottomLeft = transform.TransformPoint(new Vector3(-halfCellSize, -halfCellSize, 0f));
            Vector3 topLeft = transform.TransformPoint(new Vector3(-halfCellSize, halfCellSize, 0f));
            Vector3 topRight = transform.TransformPoint(new Vector3(halfCellSize, halfCellSize, 0f));
            Vector3 bottomRight = transform.TransformPoint(new Vector3(halfCellSize, -halfCellSize, 0f));
            Gizmos.DrawLine(bottomLeft, topLeft);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);

            Gizmos.color = isPowered ? PoweredWireColor : UnpoweredWireColor;
            ElectricityPorts ports = RotateMask((ElectricityPorts)basePortMask, rotationSteps);
            DrawGizmoPort(ports, ElectricityPorts.North, Vector2.up);
            DrawGizmoPort(ports, ElectricityPorts.East, Vector2.right);
            DrawGizmoPort(ports, ElectricityPorts.South, Vector2.down);
            DrawGizmoPort(ports, ElectricityPorts.West, Vector2.left);
        }

        private void DrawGizmoPort(ElectricityPorts ports, ElectricityPorts port, Vector2 direction)
        {
            if ((ports & port) == 0)
            {
                return;
            }

            Vector3 start = transform.TransformPoint(ZeroPoint);
            Vector3 end = transform.TransformPoint(direction * wireReach);
            Gizmos.DrawLine(start, end);
        }
    }
}
