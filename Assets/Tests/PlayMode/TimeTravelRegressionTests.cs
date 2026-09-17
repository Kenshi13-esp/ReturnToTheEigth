using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ReturnToTheEigth.Core;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Puzzles;
using ReturnToTheEigth.TimeTravel;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReturnToTheEigth.Tests
{
    /// <summary>XY physics regression coverage for inactive geometry and pending 2D door state.</summary>
    public sealed class TimeTravelRegressionTests
    {
        private const string FixtureName = "TimeTravel2DTestFixture";
        private const string PresentName = "Present";
        private const string PastName = "Past";
        private const string PlayerName = "Player";
        private const string ManagerName = "Manager";
        private const string ObstacleName = "DestinationObstacle2D";
        private const string TestDoorIdentifier = "RegressionDoor";
        private const string PresentField = "presentMansionRoot";
        private const string PastField = "pastMansionRoot";
        private const string PlayerField = "playerCollider";
        private const string MaskField = "solidObstacleLayers";
        private const string CooldownField = "transitionCooldownSeconds";
        private const string StateField = "gameStateChannel";
        private const string BlockedField = "transitionBlockedChannel";
        private const string DoorIdentifierField = "doorIdentifier";
        private const string DoorChannelField = "doorStateChannel";
        private const string DoorColliderField = "blockingCollider";
        private const int TestLayer = 31;
        private const int LayerBit = 1;
        private const int NoEvents = 0;
        private const int OneEvent = 1;
        private const float Zero = 0f;
        private const float CooldownSeconds = 0.5f;
        private const float DistantOffset = 4f;
        private const float RotatedWallAngle = 45f;
        private static readonly Vector3 TestPosition = new Vector3(1000f, 1000f, 0f);
        private static readonly Vector2 PlayerSize = new Vector2(0.16f, 0.14f);
        private static readonly Vector2 ObstacleSize = new Vector2(0.5f, 0.5f);
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject fixture;
        private GameObject present;
        private GameObject past;
        private BoxCollider2D player;
        private TimeTravelManager manager;
        private GameStateEventChannelSO stateChannel;
        private DoorStateEventChannelSO doorChannel;
        private VoidEventChannelSO blockedChannel;

        /// <summary>Creates an isolated XY fixture with the same footprint type as Hall's player.</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            stateChannel = ScriptableObject.CreateInstance<GameStateEventChannelSO>();
            doorChannel = ScriptableObject.CreateInstance<DoorStateEventChannelSO>();
            blockedChannel = ScriptableObject.CreateInstance<VoidEventChannelSO>();
            fixture = new GameObject(FixtureName);
            fixture.SetActive(false);
            present = CreateChild(PresentName);
            past = CreateChild(PastName);
            past.SetActive(false);
            GameObject playerObject = CreateChild(PlayerName);
            playerObject.transform.position = TestPosition;
            player = playerObject.AddComponent<BoxCollider2D>();
            player.size = PlayerSize;
            manager = CreateChild(ManagerName).AddComponent<TimeTravelManager>();
            SetField(manager, PresentField, present);
            SetField(manager, PastField, past);
            SetField(manager, PlayerField, player);
            SetField(manager, MaskField, (LayerMask)(LayerBit << TestLayer));
            SetField(manager, CooldownField, Zero);
            SetField(manager, StateField, stateChannel);
            SetField(manager, BlockedField, blockedChannel);
            fixture.SetActive(true);
            yield return null;
        }

        /// <summary>Destroys the fixture and lets its query-only physics world unload.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(fixture);
            Object.Destroy(stateChannel);
            Object.Destroy(doorChannel);
            Object.Destroy(blockedChannel);
            yield return null;
            yield return null;
        }

        /// <summary>A clear shift changes roots without moving XY or depth.</summary>
        [Test]
        public void ClearShiftPreservesPositionAndTogglesRoots()
        {
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(manager.CurrentEra, Is.EqualTo(TimelineEra.Past));
            Assert.That(present.activeSelf, Is.False);
            Assert.That(past.activeSelf, Is.True);
            Assert.That(player.transform.position, Is.EqualTo(TestPosition));
        }

        /// <summary>An inactive destination BoxCollider2D blocks and publishes feedback.</summary>
        [Test]
        public void InactiveDestinationBoxRejectsShiftAndRaisesFeedback()
        {
            CreateDestinationObstacle();
            int blockedEvents = NoEvents;
            blockedChannel.OnEventRaised += () => blockedEvents++;
            Assert.That(manager.TryShiftTime(), Is.False);
            Assert.That(blockedEvents, Is.EqualTo(OneEvent));
            Assert.That(manager.CurrentEra, Is.EqualTo(TimelineEra.Present));
            Assert.That(past.activeSelf, Is.False);
            Assert.That(player.transform.position, Is.EqualTo(TestPosition));
        }

        /// <summary>Rotated 2D wall shapes are copied at their world rotation.</summary>
        [Test]
        public void RotatedWallRejectsShift()
        {
            BoxCollider2D wall = CreateDestinationObstacle();
            wall.transform.rotation = Quaternion.Euler(Zero, Zero, RotatedWallAngle);
            Assert.That(manager.TryShiftTime(), Is.False);
        }

        /// <summary>A wall beside the player does not block the destination.</summary>
        [Test]
        public void DistantWallAllowsShift()
        {
            CreateDestinationObstacle().transform.position += Vector3.right * DistantOffset;
            Assert.That(manager.TryShiftTime(), Is.True);
        }

        /// <summary>Disabled colliders do not obstruct time travel.</summary>
        [Test]
        public void DisabledColliderDoesNotBlockShift()
        {
            CreateDestinationObstacle().enabled = false;
            Assert.That(manager.TryShiftTime(), Is.True);
        }

        /// <summary>Individually inactive children remain non-solid in the destination.</summary>
        [Test]
        public void InactiveChildDoesNotBlockShift()
        {
            CreateDestinationObstacle().gameObject.SetActive(false);
            Assert.That(manager.TryShiftTime(), Is.True);
        }

        /// <summary>Interaction triggers are ignored by destination clearance.</summary>
        [Test]
        public void TriggerColliderDoesNotBlockShift()
        {
            CreateDestinationObstacle().isTrigger = true;
            Assert.That(manager.TryShiftTime(), Is.True);
        }

        /// <summary>Geometry outside the solid mask does not participate in clearance.</summary>
        [Test]
        public void LayerMaskExcludesUnselectedGeometry()
        {
            CreateDestinationObstacle().gameObject.layer = NoEvents;
            Assert.That(manager.TryShiftTime(), Is.True);
        }

        /// <summary>Closed destination doors block before their first Awake.</summary>
        [Test]
        public void ClosedDoorBlocksBeforeFirstAwake()
        {
            CreateDestinationDoor();
            Assert.That(manager.TryShiftTime(), Is.False);
        }

        /// <summary>Pending open commands affect both clearance and the activated door collider.</summary>
        [Test]
        public void PendingOpenCommandAllowsShiftAndRestoresDoorState()
        {
            DoorController door = CreateDestinationDoor();
            doorChannel.RequestDoorState(TestDoorIdentifier, true);
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.GetComponent<BoxCollider2D>().enabled, Is.False);
        }

        /// <summary>Pending close commands block even if the previous collider state was disabled.</summary>
        [Test]
        public void PendingCloseCommandBlocksDisabledDoorCollider()
        {
            DoorController door = CreateDestinationDoor();
            door.GetComponent<BoxCollider2D>().enabled = false;
            doorChannel.RequestDoorState(TestDoorIdentifier, false);
            Assert.That(manager.TryShiftTime(), Is.False);
        }

        /// <summary>Success starts the configured cooldown.</summary>
        [Test]
        public void SuccessfulShiftStartsCooldown()
        {
            SetField(manager, CooldownField, CooldownSeconds);
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(manager.TryShiftTime(), Is.False);
        }

        /// <summary>Session states prevent travel independently of timescale.</summary>
        [TestCase(GameState.Puzzle)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.GameOver)]
        public void NonExplorationStateRejectsShift(GameState state)
        {
            stateChannel.RaiseGameStateChanged(state);
            Assert.That(manager.TryShiftTime(), Is.False);
        }

        /// <summary>Door commands survive disabling and re-enabling their era.</summary>
        [Test]
        public void DoorStateSurvivesEraRoundTrip()
        {
            DoorController door = CreateDestinationDoor();
            doorChannel.RequestDoorState(TestDoorIdentifier, true);
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(manager.TryShiftTime(), Is.True);
            Assert.That(door.IsOpen, Is.True);
        }

        private GameObject CreateChild(string childName)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(fixture.transform, false);
            return child;
        }

        private BoxCollider2D CreateDestinationObstacle()
        {
            GameObject obstacle = new GameObject(ObstacleName);
            obstacle.transform.SetParent(past.transform, false);
            obstacle.transform.position = TestPosition;
            obstacle.layer = TestLayer;
            BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
            collider.size = ObstacleSize;
            return collider;
        }

        private DoorController CreateDestinationDoor()
        {
            BoxCollider2D collider = CreateDestinationObstacle();
            DoorController door = collider.gameObject.AddComponent<DoorController>();
            SetField(door, DoorIdentifierField, TestDoorIdentifier);
            SetField(door, DoorChannelField, doorChannel);
            SetField(door, DoorColliderField, collider);
            return door;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
