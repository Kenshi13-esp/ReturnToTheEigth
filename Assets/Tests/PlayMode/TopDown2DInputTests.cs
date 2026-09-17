using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ReturnToTheEigth.Events;
using ReturnToTheEigth.Interaction;
using ReturnToTheEigth.Player;
using ReturnToTheEigth.Puzzles;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ReturnToTheEigth.Tests
{
    /// <summary>Exercises the real keyboard bindings, 2D movement, wall contact, and immediate interaction.</summary>
    public sealed class TopDown2DInputTests
    {
        private const string FixtureName = "TopDown2DInputFixture";
        private const string PlayerName = "Player";
        private const string WallName = "Wall";
        private const string LeverName = "Lever";
        private const string PlayerMap = "Player";
        private const string MoveAction = "Move";
        private const string ShiftAction = "TimeShift";
        private const string InteractAction = "Interact";
        private const string MoveComposite = "2DVector";
        private const string UpPart = "up";
        private const string DownPart = "down";
        private const string LeftPart = "left";
        private const string RightPart = "right";
        private const string UpKey = "<Keyboard>/w";
        private const string DownKey = "<Keyboard>/s";
        private const string LeftKey = "<Keyboard>/a";
        private const string RightKey = "<Keyboard>/d";
        private const string ShiftKey = "<Keyboard>/leftShift";
        private const string InteractKey = "<Keyboard>/space";
        private const string InputField = "inputActions";
        private const string ShiftField = "timeShiftRequestedChannel";
        private const string StateField = "gameStateChannel";
        private const string DoorChannelField = "doorStateChannel";
        private const string DoorIdField = "doorIdentifier";
        private const string DoorId = "InputTestDoor";
        private const float Zero = 0f;
        private const float HoldDuration = 0.8f;
        private const float ShortDuration = 0.1f;
        private const float MinimumTravel = 0.2f;
        private const float WallDistance = 0.6f;
        private const float MaximumWallTravel = 0.49f;
        private const float LeverDistance = -0.25f;
        private const float PositionTolerance = 0.02f;
        private const int NoEvents = 0;
        private const int OneEvent = 1;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 TestPosition = new Vector3(2000f, 2000f, 0f);
        private static readonly Vector2 PlayerSize = new Vector2(0.16f, 0.14f);
        private static readonly Vector2 WallSize = new Vector2(2f, 0.1f);
        private static readonly Vector2 LeverSize = new Vector2(0.1f, 0.1f);
        private GameObject fixture;
        private GameObject player;
        private Keyboard keyboard;
        private InputActionAsset input;
        private VoidEventChannelSO shiftChannel;
        private DoorStateEventChannelSO doorChannel;
        private GameStateEventChannelSO stateChannel;

        /// <summary>Creates owned input actions and a gravity-free 2D player.</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            input = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = input.AddActionMap(PlayerMap);
            map.AddAction(MoveAction, InputActionType.Value).AddCompositeBinding(MoveComposite)
                .With(UpPart, UpKey).With(DownPart, DownKey).With(LeftPart, LeftKey).With(RightPart, RightKey);
            map.AddAction(ShiftAction, InputActionType.Button, ShiftKey);
            map.AddAction(InteractAction, InputActionType.Button, InteractKey);
            shiftChannel = ScriptableObject.CreateInstance<VoidEventChannelSO>();
            doorChannel = ScriptableObject.CreateInstance<DoorStateEventChannelSO>();
            stateChannel = ScriptableObject.CreateInstance<GameStateEventChannelSO>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            fixture = new GameObject(FixtureName);
            fixture.SetActive(false);
            player = new GameObject(PlayerName);
            player.transform.SetParent(fixture.transform, false);
            player.transform.position = TestPosition;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = Zero;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<BoxCollider2D>().size = PlayerSize;
            TopDownCharacterController controller = player.AddComponent<TopDownCharacterController>();
            SetField(controller, InputField, input);
            SetField(controller, ShiftField, shiftChannel);
            SetField(controller, StateField, stateChannel);
            PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
            SetField(interaction, InputField, input);
            SetField(interaction, StateField, stateChannel);
            fixture.SetActive(true);
            yield return null;
        }

        /// <summary>Releases test-owned objects and the synthetic keyboard.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(fixture);
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            Object.Destroy(input);
            Object.Destroy(shiftChannel);
            Object.Destroy(doorChannel);
            Object.Destroy(stateChannel);
            yield return null;
        }

        /// <summary>W moves up on Y and never introduces Z movement or gravity.</summary>
        [UnityTest]
        public IEnumerator WMovesOnXYNotXZ()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(HoldDuration);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(player.transform.position.y, Is.GreaterThan(TestPosition.y + MinimumTravel));
            Assert.That(player.transform.position.x, Is.EqualTo(TestPosition.x).Within(PositionTolerance));
            Assert.That(player.transform.position.z, Is.EqualTo(TestPosition.z));
        }

        /// <summary>A solid wall stops keyboard-driven 2D movement.</summary>
        [UnityTest]
        public IEnumerator SolidWallStopsPlayer()
        {
            GameObject wall = new GameObject(WallName);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.position = TestPosition + Vector3.up * WallDistance;
            wall.AddComponent<BoxCollider2D>().size = WallSize;
            Physics2D.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(HoldDuration);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(player.transform.position.y, Is.GreaterThan(TestPosition.y + MinimumTravel));
            Assert.That(player.transform.position.y, Is.LessThan(TestPosition.y + MaximumWallTravel));
        }

        /// <summary>Left Shift publishes exactly one immediate request per press.</summary>
        [UnityTest]
        public IEnumerator LeftShiftPublishesRequest()
        {
            int count = NoEvents;
            shiftChannel.OnEventRaised += () => count++;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            yield return new WaitForSeconds(ShortDuration);
            Assert.That(count, Is.EqualTo(OneEvent));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        /// <summary>Space immediately activates a nearby forward-facing interactable.</summary>
        [UnityTest]
        public IEnumerator SpaceActivatesNearbyLeverWithoutHold()
        {
            GameObject lever = new GameObject(LeverName);
            lever.SetActive(false);
            lever.transform.SetParent(fixture.transform, false);
            lever.transform.position = TestPosition + Vector3.up * LeverDistance;
            BoxCollider2D collider = lever.AddComponent<BoxCollider2D>();
            collider.size = LeverSize;
            collider.isTrigger = true;
            DoorSwitchInteractable interactable = lever.AddComponent<DoorSwitchInteractable>();
            SetField(interactable, DoorChannelField, doorChannel);
            SetField(interactable, DoorIdField, DoorId);
            lever.SetActive(true);
            Physics2D.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(ShortDuration);
            Assert.That(doorChannel.TryGetDoorState(DoorId, out bool open), Is.True);
            Assert.That(open, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
