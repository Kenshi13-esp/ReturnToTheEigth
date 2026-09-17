using System.Collections;
using NUnit.Framework;
using ReturnToTheEigth.CameraSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace ReturnToTheEigth.Tests
{
    /// <summary>Checks exact room framing, boundary cuts, and aspect-ratio preservation.</summary>
    public sealed class RoomCameraTests
    {
        private const string RootName = "RoomCameraTestFixture";
        private const string CameraName = "RoomCamera";
        private const string TargetName = "PlayerTarget";
        private const int FirstIndex = 0;
        private const int Columns = 2;
        private const int Rows = 4;
        private const int LastRow = Rows - 1;
        private const int SecondColumn = 1;
        private const int PortraitWidth = 300;
        private const int PortraitHeight = 600;
        private const int WideWidth = 900;
        private const int WideHeight = 300;
        private const int NoDepthBuffer = 0;
        private const float RoomWidth = 4.26f;
        private const float RoomHeight = 2.4f;
        private const float OriginX = -4.26f;
        private const float OriginY = -4.8f;
        private const float Zero = 0f;
        private const float Half = 0.5f;
        private const float One = 1f;
        private const float Twice = 2f;
        private const float CameraDepth = -10f;
        private const float WorldTolerance = 0.0001f;
        private const float BoundaryInset = 0.01f;
        private const float OutsideCoordinate = 100f;
        private static readonly Vector3 SpawnPosition = new Vector3(-1.2f, -3.7f, 0f);
        private GameObject root;
        private Camera camera;
        private TopDownCameraFollow roomCamera;
        private Transform target;
        private RenderTexture output;

        /// <summary>Creates the camera with Hall's room dimensions and assigns a player target.</summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject(RootName);
            GameObject cameraObject = new GameObject(CameraName);
            cameraObject.transform.SetParent(root.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            roomCamera = cameraObject.AddComponent<TopDownCameraFollow>();
            target = new GameObject(TargetName).transform;
            target.SetParent(root.transform, false);
            target.position = SpawnPosition;
            roomCamera.SetTarget(target);
        }

        /// <summary>Releases the camera, target, and any owned render texture.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            camera.targetTexture = null;
            if (output != null) Object.Destroy(output);
            Object.Destroy(root);
            yield return null;
        }

        /// <summary>All eight room centers produce exactly the matching room projection and location.</summary>
        [Test]
        public void EveryRoomHasExactBounds()
        {
            for (int column = FirstIndex; column < Columns; column++)
                for (int row = FirstIndex; row < Rows; row++)
                {
                    Vector3 center = new Vector3(OriginX + (column + Half) * RoomWidth,
                        OriginY + (row + Half) * RoomHeight, Zero);
                    target.position = center;
                    roomCamera.RefreshRoomFraming();
                    Assert.That(roomCamera.CurrentRoom, Is.EqualTo(new Vector2Int(column, row)));
                    Assert.That(camera.transform.position.x, Is.EqualTo(center.x).Within(WorldTolerance));
                    Assert.That(camera.transform.position.y, Is.EqualTo(center.y).Within(WorldTolerance));
                    Assert.That(camera.transform.position.z, Is.EqualTo(CameraDepth));
                    AssertExactDimensions();
                }
        }

        /// <summary>Moving within one room never causes camera drift.</summary>
        [Test]
        public void MovementInsideRoomKeepsCameraFixed()
        {
            Vector3 previousPosition = camera.transform.position;
            target.position = new Vector3(OriginX + BoundaryInset, OriginY + BoundaryInset, Zero);
            roomCamera.RefreshRoomFraming();
            Assert.That(camera.transform.position, Is.EqualTo(previousPosition));
        }

        /// <summary>Crossing the central doorway cuts immediately to the right room without intermediate framing.</summary>
        [Test]
        public void CrossingBoundaryCutsToNextRoom()
        {
            target.position = new Vector3(BoundaryInset, SpawnPosition.y, Zero);
            roomCamera.RefreshRoomFraming();
            Assert.That(roomCamera.CurrentRoom, Is.EqualTo(new Vector2Int(SecondColumn, FirstIndex)));
            Assert.That(camera.transform.position.x, Is.EqualTo(RoomWidth * Half).Within(WorldTolerance));
            AssertExactDimensions();
        }

        /// <summary>Targets beyond the map remain framed by the closest valid room.</summary>
        [Test]
        public void OutsideMapClampsToEdgeRoom()
        {
            target.position = new Vector3(-OutsideCoordinate, OutsideCoordinate, Zero);
            roomCamera.RefreshRoomFraming();
            Assert.That(roomCamera.CurrentRoom, Is.EqualTo(new Vector2Int(FirstIndex, LastRow)));
        }

        /// <summary>Portrait outputs add letterboxing instead of cropping or showing additional rooms.</summary>
        [Test]
        public void PortraitViewportPreservesRoomDimensions()
        {
            output = new RenderTexture(PortraitWidth, PortraitHeight, NoDepthBuffer);
            camera.targetTexture = output;
            roomCamera.RefreshRoomFraming();
            Assert.That(camera.rect.width, Is.EqualTo(One).Within(WorldTolerance));
            Assert.That(camera.rect.height, Is.LessThan(One));
            AssertExactDimensions();
        }

        /// <summary>Wide outputs add side bars instead of widening the visible room.</summary>
        [Test]
        public void WideViewportPreservesRoomDimensions()
        {
            output = new RenderTexture(WideWidth, WideHeight, NoDepthBuffer);
            camera.targetTexture = output;
            roomCamera.RefreshRoomFraming();
            Assert.That(camera.rect.width, Is.LessThan(One));
            Assert.That(camera.rect.height, Is.EqualTo(One).Within(WorldTolerance));
            AssertExactDimensions();
        }

        private void AssertExactDimensions()
        {
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize * Twice, Is.EqualTo(RoomHeight).Within(WorldTolerance));
            Assert.That(camera.orthographicSize * Twice * camera.aspect, Is.EqualTo(RoomWidth).Within(WorldTolerance));
            Assert.That(camera.transform.rotation, Is.EqualTo(Quaternion.identity));
        }
    }
}
