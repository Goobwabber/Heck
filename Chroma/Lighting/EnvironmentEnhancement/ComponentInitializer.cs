﻿using System;
using System.Collections.Generic;
using System.Linq;
using Chroma.HarmonyPatches.Colorizer.Initialize;
using Chroma.Settings;
using HarmonyLib;
using IPA.Utilities;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Chroma.Lighting.EnvironmentEnhancement
{
    [UsedImplicitly]
    internal class ComponentInitializer
    {
        private static readonly FieldAccessor<LightWithIdMonoBehaviour, LightWithIdManager>.Accessor _lightWithIdMonoBehaviourManagerAccessor = FieldAccessor<LightWithIdMonoBehaviour, LightWithIdManager>.GetAccessor("_lightManager");
        private static readonly FieldAccessor<LightWithIds, LightWithIdManager>.Accessor _lightWithIdsManagerAccessor = FieldAccessor<LightWithIds, LightWithIdManager>.GetAccessor("_lightManager");
        private static readonly FieldAccessor<TrackLaneRingsManager, TrackLaneRing[]>.Accessor _ringsAccessor = FieldAccessor<TrackLaneRingsManager, TrackLaneRing[]>.GetAccessor("_rings");
        private static readonly FieldAccessor<Spectrogram, BasicSpectrogramData>.Accessor _spectrogramDataAccessor = FieldAccessor<Spectrogram, BasicSpectrogramData>.GetAccessor("_spectrogramData");
        private static readonly FieldAccessor<LightRotationEventEffect, BeatmapCallbacksController>.Accessor _lightCallbackControllerAccessor = FieldAccessor<LightRotationEventEffect, BeatmapCallbacksController>.GetAccessor("_beatmapCallbacksController");
        private static readonly FieldAccessor<LightPairRotationEventEffect, BeatmapCallbacksController>.Accessor _lightPairCallbackControllerAccessor = FieldAccessor<LightPairRotationEventEffect, BeatmapCallbacksController>.GetAccessor("_beatmapCallbacksController");
        private static readonly FieldAccessor<LightPairRotationEventEffect, IAudioTimeSource>.Accessor _audioTimeSourceAccessor = FieldAccessor<LightPairRotationEventEffect, IAudioTimeSource>.GetAccessor("_audioTimeSource");
        private static readonly FieldAccessor<LightPairRotationEventEffect, Transform>.Accessor _transformLAccessor = FieldAccessor<LightPairRotationEventEffect, Transform>.GetAccessor("_transformL");
        private static readonly FieldAccessor<LightPairRotationEventEffect, Transform>.Accessor _transformRAccessor = FieldAccessor<LightPairRotationEventEffect, Transform>.GetAccessor("_transformR");
        private static readonly FieldAccessor<ParticleSystemEventEffect, BeatmapCallbacksController>.Accessor _particleCallbackControllerAccessor = FieldAccessor<ParticleSystemEventEffect, BeatmapCallbacksController>.GetAccessor("_beatmapCallbacksController");
        private static readonly FieldAccessor<ParticleSystemEventEffect, ParticleSystem>.Accessor _particleSystemAccessor = FieldAccessor<ParticleSystemEventEffect, ParticleSystem>.GetAccessor("_particleSystem");
        private static readonly FieldAccessor<Mirror, MirrorRendererSO>.Accessor _mirrorRendererAccessor = FieldAccessor<Mirror, MirrorRendererSO>.GetAccessor("_mirrorRenderer");
        private static readonly FieldAccessor<Mirror, Material>.Accessor _mirrorMaterialAccessor = FieldAccessor<Mirror, Material>.GetAccessor("_mirrorMaterial");
        private static readonly FieldAccessor<TrackLaneRingsPositionStepEffectSpawner, TrackLaneRingsManager>.Accessor _stepSpawnerRingsManagerAccessor = FieldAccessor<TrackLaneRingsPositionStepEffectSpawner, TrackLaneRingsManager>.GetAccessor("_trackLaneRingsManager");
        private static readonly FieldAccessor<TrackLaneRingsRotationEffectSpawner, TrackLaneRingsRotationEffect>.Accessor _trackLaneRingsRotationEffectAccessor = FieldAccessor<TrackLaneRingsRotationEffectSpawner, TrackLaneRingsRotationEffect>.GetAccessor("_trackLaneRingsRotationEffect");
        private static readonly FieldAccessor<TrackLaneRingsRotationEffectSpawner, BeatmapCallbacksController>.Accessor _rotationEffectSpawnerCallbackControllerAccessor = FieldAccessor<TrackLaneRingsRotationEffectSpawner, BeatmapCallbacksController>.GetAccessor("_beatmapCallbacksController");
        private static readonly FieldAccessor<LightWithIds, IEnumerable<LightWithIds.LightWithId>>.Accessor _lightWithIdsAccessor = FieldAccessor<LightWithIds, IEnumerable<LightWithIds.LightWithId>>.GetAccessor("_lightWithIds");

        private static readonly FieldAccessor<TrackLaneRing, Transform>.Accessor _ringTransformAccessor = FieldAccessor<TrackLaneRing, Transform>.GetAccessor("_transform");
        private static readonly FieldAccessor<TrackLaneRing, Vector3>.Accessor _positionOffsetAccessor = FieldAccessor<TrackLaneRing, Vector3>.GetAccessor("_positionOffset");
        private static readonly FieldAccessor<TrackLaneRing, float>.Accessor _posZAccessor = FieldAccessor<TrackLaneRing, float>.GetAccessor("_posZ");

        private readonly EnvironmentEnhancementManager _environmentManager;
        private readonly LightWithIdRegisterer _lightWithIdRegisterer;

        private readonly HashSet<TrackLaneRingsManager> _trackLaneRingsManagers;

        private ComponentInitializer(
            EnvironmentEnhancementManager environmentManager,
            LightWithIdRegisterer lightWithIdRegisterer)
        {
            _environmentManager = environmentManager;
            _lightWithIdRegisterer = lightWithIdRegisterer;
            _trackLaneRingsManagers = Resources.FindObjectsOfTypeAll<TrackLaneRingsManager>().ToHashSet();
        }

        internal bool SkipAwake { get; private set; }

        internal void PrefillComponentsData(Transform root, List<IComponentData> componentDatas)
        {
            SkipAwake = true;

            TrackLaneRingsManager trackLaneRingsManager = root.GetComponent<TrackLaneRingsManager>();
            if (trackLaneRingsManager != null)
            {
                componentDatas.Add(new TrackLaneRingsManagerComponentData
                {
                    OldTrackLaneRingsManager = trackLaneRingsManager
                });
            }

            foreach (Transform transform in root)
            {
                PrefillComponentsData(transform, componentDatas);
            }
        }

        internal void PostfillComponentsData(Transform root, Transform original, List<IComponentData> componentDatas)
        {
            SkipAwake = false;

            TrackLaneRingsManager trackLaneRingsManager = root.GetComponent<TrackLaneRingsManager>();
            if (trackLaneRingsManager != null)
            {
                _trackLaneRingsManagers.Add(trackLaneRingsManager);
                TrackLaneRingsManager originalManager = original.GetComponent<TrackLaneRingsManager>();
                foreach (TrackLaneRingsManagerComponentData componentData in componentDatas.OfType<TrackLaneRingsManagerComponentData>().Where(n => n.OldTrackLaneRingsManager == originalManager))
                {
                    componentData.NewTrackLaneRingsManager = trackLaneRingsManager;
                }
            }

            TrackLaneRingsRotationEffect rotationEffect = root.GetComponent<TrackLaneRingsRotationEffect>();
            if (rotationEffect != null)
            {
                Object.Destroy(rotationEffect);
            }

            foreach (Transform transform in root)
            {
                int index = transform.GetSiblingIndex();
                PostfillComponentsData(transform, original.GetChild(index), componentDatas);
            }
        }

        internal void InitializeComponents(Transform root, Transform original, List<GameObjectInfo> gameObjectInfos, List<IComponentData> componentDatas, int? lightID)
        {
            void GetComponentAndOriginal<T>(Action<T, T> initializeDelegate)
                where T : Component
            {
                T[] rootComponents = root.GetComponents<T>();
                T[] originalComponents = original.GetComponents<T>();

                for (int i = 0; i < rootComponents.Length; i++)
                {
                    initializeDelegate(rootComponents[i], originalComponents[i]);

                    if (ChromaConfig.Instance.PrintEnvironmentEnhancementDebug)
                    {
                        Log.Logger.Log($"Initialized {typeof(T).Name}");
                    }
                }
            }

            GetComponentAndOriginal<LightWithIdMonoBehaviour>((rootComponent, originalComponent) =>
            {
                _lightWithIdMonoBehaviourManagerAccessor(ref rootComponent) = _lightWithIdMonoBehaviourManagerAccessor(ref originalComponent);
                _lightWithIdRegisterer.MarkForTableRegister(rootComponent);
                if (lightID.HasValue)
                {
                    _lightWithIdRegisterer.SetRequestedId(rootComponent, lightID.Value);
                }
            });

            GetComponentAndOriginal<LightWithIds>((rootComponent, originalComponent) =>
            {
                _lightWithIdsManagerAccessor(ref rootComponent) = _lightWithIdsManagerAccessor(ref originalComponent);
                IEnumerable<ILightWithId> lightsWithId = _lightWithIdsAccessor(ref rootComponent);
                foreach (ILightWithId light in lightsWithId)
                {
                    if (lightID.HasValue)
                    {
                        _lightWithIdRegisterer.SetRequestedId(light, lightID.Value);
                    }

                    _lightWithIdRegisterer.MarkForTableRegister(light);
                }
            });

            GetComponentAndOriginal<TrackLaneRing>((rootComponent, originalComponent) =>
            {
                if (_environmentManager.RingRotationOffsets.TryGetValue(originalComponent, out Quaternion offset))
                {
                    _environmentManager.RingRotationOffsets.Add(rootComponent, offset);
                }

                _ringTransformAccessor(ref rootComponent) = root;
                _positionOffsetAccessor(ref rootComponent) = _positionOffsetAccessor(ref originalComponent);
                _posZAccessor(ref rootComponent) = _posZAccessor(ref originalComponent);

                TrackLaneRingsManager? managerToAdd = null;
                foreach (TrackLaneRingsManager manager in _trackLaneRingsManagers)
                {
                    TrackLaneRingsManagerComponentData? componentData = componentDatas
                        .OfType<TrackLaneRingsManagerComponentData>()
                        .FirstOrDefault(n => n.OldTrackLaneRingsManager == manager);
                    if (componentData != null)
                    {
                        managerToAdd = componentData.NewTrackLaneRingsManager;
                    }
                    else
                    {
                        TrackLaneRingsManager managerRef = manager;
                        TrackLaneRing[] rings = _ringsAccessor(ref managerRef);
                        if (rings.Contains(originalComponent))
                        {
                            managerToAdd = manager;
                        }
                    }

                    // ReSharper disable once InvertIf
                    if (managerToAdd != null)
                    {
                        _ringsAccessor(ref managerToAdd) = _ringsAccessor(ref managerToAdd).AddToArray(rootComponent);

                        break;
                    }
                }
            });

            GetComponentAndOriginal<TrackLaneRingsPositionStepEffectSpawner>((rootComponent, _) =>
            {
                foreach (TrackLaneRingsManager manager in _trackLaneRingsManagers)
                {
                    TrackLaneRingsManagerComponentData? componentData = componentDatas
                        .OfType<TrackLaneRingsManagerComponentData>()
                        .FirstOrDefault(n => n.OldTrackLaneRingsManager == manager);
                    if (componentData == null)
                    {
                        continue;
                    }

                    _stepSpawnerRingsManagerAccessor(ref rootComponent) = componentData.NewTrackLaneRingsManager!;

                    break;
                }
            });

            GetComponentAndOriginal<TrackLaneRingsRotationEffectSpawner>((rootComponent, originalComponent) =>
            {
                _rotationEffectSpawnerCallbackControllerAccessor(ref rootComponent) = _rotationEffectSpawnerCallbackControllerAccessor(ref originalComponent);
                _trackLaneRingsRotationEffectAccessor(ref rootComponent) = rootComponent.GetComponent<TrackLaneRingsRotationEffect>();
            });

            GetComponentAndOriginal<Spectrogram>((rootComponent, originalComponent) => _spectrogramDataAccessor(ref rootComponent) = _spectrogramDataAccessor(ref originalComponent));

            GetComponentAndOriginal<LightRotationEventEffect>((rootComponent, originalComponent) => _lightCallbackControllerAccessor(ref rootComponent) = _lightCallbackControllerAccessor(ref originalComponent));

            GetComponentAndOriginal<LightPairRotationEventEffect>((rootComponent, originalComponent) =>
            {
                _lightPairCallbackControllerAccessor(ref rootComponent) = _lightPairCallbackControllerAccessor(ref originalComponent);
                _audioTimeSourceAccessor(ref rootComponent) = _audioTimeSourceAccessor(ref originalComponent);

                Transform transformL = _transformLAccessor(ref originalComponent);
                Transform transformR = _transformRAccessor(ref originalComponent);

                _transformLAccessor(ref rootComponent) = root.GetChild(transformL.GetSiblingIndex());
                _transformRAccessor(ref rootComponent) = root.GetChild(transformR.GetSiblingIndex());

                // We have to enable the object to tell unity to run Start
                rootComponent.enabled = true;
            });

            GetComponentAndOriginal<ParticleSystemEventEffect>((rootComponent, originalComponent) =>
            {
                _particleCallbackControllerAccessor(ref rootComponent) = _particleCallbackControllerAccessor(ref originalComponent);
                _particleSystemAccessor(ref rootComponent) = root.GetComponent<ParticleSystem>();

                rootComponent.enabled = true;
            });

            GetComponentAndOriginal<Mirror>((rootComponent, originalComponent) =>
            {
                _mirrorRendererAccessor(ref rootComponent) = Object.Instantiate(_mirrorRendererAccessor(ref originalComponent));
                _mirrorMaterialAccessor(ref rootComponent) = Object.Instantiate(_mirrorMaterialAccessor(ref originalComponent));
            });

            SaberBurnMarkArea? saberBurnMarkArea = root.GetComponent<SaberBurnMarkArea>();
            if (saberBurnMarkArea != null)
            {
                if (ChromaConfig.Instance.PrintEnvironmentEnhancementDebug)
                {
                    Log.Logger.Log("SaberBurnMarkArea yeeted. Complain to me if you would rather it not.");
                }

                Object.Destroy(saberBurnMarkArea);
            }

            SaberBurnMarkSparkles? saberBurnMarkSparkles = root.GetComponent<SaberBurnMarkSparkles>();
            if (saberBurnMarkSparkles != null)
            {
                if (ChromaConfig.Instance.PrintEnvironmentEnhancementDebug)
                {
                    Log.Logger.Log("SaberBurnMarkSparkles yeeted. Complain to me if you would rather it not.");
                }

                Object.Destroy(saberBurnMarkSparkles);
            }

            GameObjectInfo newGameObjectInfo = new(root.gameObject);
            gameObjectInfos.Add(newGameObjectInfo);

            foreach (Transform transform in root)
            {
                int index = transform.GetSiblingIndex();
                InitializeComponents(transform, original.GetChild(index), gameObjectInfos, componentDatas, lightID);
            }
        }
    }
}
