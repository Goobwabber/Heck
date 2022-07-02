﻿using System;
using System.Collections.Generic;
using CustomJSONData.CustomBeatmap;
using Heck;
using Heck.Animation;
using Heck.Animation.Transform;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;
using static Heck.HeckController;
using static NoodleExtensions.NoodleController;

namespace NoodleExtensions.Animation
{
    internal class ParentObject : MonoBehaviour
    {
        private Track _track = null!;
        private bool _worldPositionStays;
        private bool _leftHanded;
        private Vector3 _startPos = Vector3.zero;
        private Quaternion _startRot = Quaternion.identity;
        private Quaternion _startLocalRot = Quaternion.identity;
        private Vector3 _startScale = Vector3.one;

        private BeatmapObjectSpawnMovementData _movementData = null!;

        internal HashSet<Track> ChildrenTracks { get; } = new();

        internal void Init(
            NoodleParentTrackEventData noodleData,
            bool leftHanded,
            BeatmapObjectSpawnMovementData movementData,
            HashSet<ParentObject> parentObjects)
        {
            List<Track> tracks = noodleData.ChildrenTracks;
            Track parentTrack = noodleData.ParentTrack;
            if (tracks.Contains(parentTrack))
            {
                throw new InvalidOperationException("How could a track contain itself?");
            }

            _track = parentTrack;
            _worldPositionStays = noodleData.WorldPositionStays;
            _leftHanded = leftHanded;
            _movementData = movementData;

            parentTrack.AddGameObject(gameObject);

            foreach (Track track in tracks)
            {
                foreach (ParentObject parentObject in parentObjects)
                {
                    track.GameObjectAdded -= parentObject.OnTrackGameObjectAdded;
                    track.GameObjectRemoved -= OnTrackGameObjectRemoved;
                    parentObject.ChildrenTracks.Remove(track);
                }

                foreach (GameObject go in track.GameObjects)
                {
                    ParentToObject(go.transform);
                }

                ChildrenTracks.Add(track);

                track.GameObjectAdded += OnTrackGameObjectAdded;
                track.GameObjectRemoved += OnTrackGameObjectRemoved;
            }

            parentObjects.Add(this);
        }

        internal void ApplyV2Transform(NoodleParentTrackEventData noodleData)
        {
            Vector3? startPos = noodleData.OffsetPosition;
            Quaternion? startRot = noodleData.WorldRotation;
            Quaternion? startLocalRot = noodleData.TransformData.LocalRotation;
            Vector3? startScale = noodleData.TransformData.Scale;

            Transform transform1 = transform;
            if (startPos.HasValue)
            {
                _startPos = startPos.Value;
                transform1.localPosition = _startPos * _movementData.noteLinesDistance;
            }

            if (startRot.HasValue)
            {
                _startRot = startRot.Value;
                _startLocalRot = _startRot;
                transform1.localPosition = _startRot * transform1.localPosition;
                transform1.localRotation = _startRot;
            }

            if (startLocalRot.HasValue)
            {
                _startLocalRot = _startRot * startLocalRot.Value;
                transform1.localRotation *= _startLocalRot;
            }

            // ReSharper disable once InvertIf
            if (startScale.HasValue)
            {
                _startScale = startScale.Value;
                transform1.localScale = _startScale;
            }
        }

        private static void OnTrackGameObjectRemoved(GameObject trackGameObject)
        {
            trackGameObject.transform.SetParent(null, false);
        }

        private void OnTrackGameObjectAdded(GameObject trackGameObject)
        {
            ParentToObject(trackGameObject.transform);
        }

        private void ParentToObject(Transform childTransform)
        {
            childTransform.SetParent(transform, _worldPositionStays);
        }

        private void OnDestroy()
        {
            foreach (Track track in ChildrenTracks)
            {
                track.GameObjectAdded -= OnTrackGameObjectAdded;
                track.GameObjectRemoved -= OnTrackGameObjectRemoved;
            }
        }

        private void Update()
        {
            Quaternion? rotation = _track.GetQuaternionProperty(OFFSET_ROTATION)?.Mirror(_leftHanded);
            Vector3? position = _track.GetVector3Property(OFFSET_POSITION)?.Mirror(_leftHanded);

            Quaternion worldRotationQuatnerion = _startRot;
            Vector3 positionVector = worldRotationQuatnerion * (_startPos * _movementData.noteLinesDistance);
            if (rotation.HasValue || position.HasValue)
            {
                Quaternion rotationOffset = rotation ?? Quaternion.identity;
                worldRotationQuatnerion *= rotationOffset;
                Vector3 positionOffset = position ?? Vector3.zero;
                positionVector = worldRotationQuatnerion * ((positionOffset + _startPos) * _movementData.noteLinesDistance);
            }

            worldRotationQuatnerion *= _startLocalRot;
            Quaternion? localRotation = _track.GetQuaternionProperty(LOCAL_ROTATION)?.Mirror(_leftHanded);
            if (localRotation.HasValue)
            {
                worldRotationQuatnerion *= localRotation.Value;
            }

            Vector3 scaleVector = _startScale;
            Vector3? scale = _track.GetVector3Property(SCALE);
            if (scale.HasValue)
            {
                scaleVector = Vector3.Scale(_startScale, scale.Value);
            }

            Transform transform1 = transform;
            transform1.localRotation = worldRotationQuatnerion;
            transform1.localPosition = positionVector;
            transform1.localScale = scaleVector;
        }
    }

    [UsedImplicitly]
    internal class ParentController
    {
        private readonly bool _v2;
        private readonly DeserializedData _deserializedData;
        private readonly bool _leftHanded;
        private readonly TransformControllerFactory _transformControllerFactory;
        private readonly BeatmapObjectSpawnMovementData _movementData;
        private readonly HashSet<ParentObject> _parentObjects = new();

        internal ParentController(
            IReadonlyBeatmapData beatmapData,
            [Inject(Id = ID)] DeserializedData deserializedData,
            [Inject(Id = LEFT_HANDED_ID)] bool leftHanded,
            IBeatmapObjectSpawnController spawnController,
            TransformControllerFactory transformControllerFactory)
        {
            _v2 = ((CustomBeatmapData)beatmapData).version2_6_0AndEarlier;
            _deserializedData = deserializedData;
            _leftHanded = leftHanded;
            _transformControllerFactory = transformControllerFactory;
            _movementData = spawnController.beatmapObjectSpawnMovementData;
        }

        internal void Create(
            CustomEventData customEventData)
        {
            if (!_deserializedData.Resolve(customEventData, out NoodleParentTrackEventData? noodleData))
            {
                return;
            }

            GameObject parentGameObject = new("ParentObject");
            ParentObject instance = parentGameObject.AddComponent<ParentObject>();
            instance.Init(
                noodleData,
                _leftHanded,
                _movementData,
                _parentObjects);

            if (_v2)
            {
                instance.ApplyV2Transform(noodleData);
            }
            else
            {
                instance.enabled = false;
                noodleData.TransformData.Apply(instance.transform, _leftHanded);
                _transformControllerFactory.Create(parentGameObject, noodleData.ParentTrack);
            }
        }
    }
}
