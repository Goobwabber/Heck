﻿using System;
using System.Collections.Generic;
using System.Linq;
using CustomJSONData.CustomBeatmap;
using Heck.Animation;
using IPA.Logging;
using Zenject;
using static Heck.HeckController;

namespace Heck
{
    public static class DeserializerManager
    {
        private static readonly HashSet<DataDeserializer> _customDataDeserializers = new();

        public static DataDeserializer Register<T>(object? id)
        {
            DataDeserializer deserializer = new(id, typeof(T));
            _customDataDeserializers.Add(deserializer);
            return deserializer;
        }

        internal static void DeserializeBeatmapDataAndBind(
            DiContainer container,
            CustomBeatmapData customBeatmapData,
            IReadonlyBeatmapData untransformedBeatmapData,
            bool leftHanded)
        {
            Log.Logger.Log("Deserializing BeatmapData.", Logger.Level.Trace);

            bool v2 = customBeatmapData.version2_6_0AndEarlier;
            if (v2)
            {
                Log.Logger.Log("BeatmapData is v2, converting...", Logger.Level.Trace);
            }

            // tracks are built based off the untransformed beatmapdata so modifiers like "no walls" do not prevent track creation
            TrackBuilder trackManager = new(v2);
            IReadOnlyList<BeatmapObjectData> untransformedObjectDatas = untransformedBeatmapData.GetBeatmapDataItems<NoteData>()
                .Cast<BeatmapObjectData>()
                .Concat(untransformedBeatmapData.GetBeatmapDataItems<ObstacleData>())
                .ToArray();
            foreach (BeatmapObjectData beatmapObjectData in untransformedObjectDatas)
            {
                CustomData customData = ((ICustomData)beatmapObjectData).customData;
                switch (beatmapObjectData)
                {
                    case CustomObstacleData obstacleData:
                        customData = obstacleData.customData;
                        break;

                    case CustomNoteData noteData:
                        customData = noteData.customData;
                        break;

                    default:
                        continue;
                }

                // for epic tracks thing
                object? trackNameRaw = customData.Get<object>(v2 ? V2_TRACK : TRACK);
                if (trackNameRaw == null)
                {
                    continue;
                }

                IEnumerable<string> trackNames;
                if (trackNameRaw is List<object> listTrack)
                {
                    trackNames = listTrack.Cast<string>();
                }
                else
                {
                    trackNames = new[] { (string)trackNameRaw };
                }

                foreach (string trackName in trackNames)
                {
                    trackManager.AddTrack(trackName);
                }
            }

            // Point definitions
            Dictionary<string, PointDefinition> pointDefinitions = new();

            void AddPoint(string pointDataName, PointDefinition pointData)
            {
                if (!pointDefinitions.ContainsKey(pointDataName))
                {
                    pointDefinitions.Add(pointDataName, pointData);
                }
                else
                {
                    Log.Logger.Log($"Duplicate point defintion name, {pointDataName} could not be registered!", Logger.Level.Error);
                }
            }

            IEnumerable<CustomData>? pointDefinitionsRaw =
                customBeatmapData.customData.Get<List<object>>(v2 ? V2_POINT_DEFINITIONS : POINT_DEFINITIONS)?.Cast<CustomData>();
            if (pointDefinitionsRaw != null)
            {
                foreach (CustomData pointDefintionRaw in pointDefinitionsRaw)
                {
                    string pointName = pointDefintionRaw.Get<string>(v2 ? V2_NAME : NAME) ?? throw new InvalidOperationException("Failed to retrieve point name.");
                    PointDefinition pointData = PointDefinition.ListToPointDefinition(pointDefintionRaw.Get<List<object>>(v2 ? V2_POINTS : POINTS)
                                                                                      ?? throw new InvalidOperationException(
                                                                                          "Failed to retrieve point array."));
                    AddPoint(pointName, pointData);
                }
            }

            // Event definitions
            IDictionary<string, CustomEventData> eventDefinitions = new Dictionary<string, CustomEventData>();

            if (!v2)
            {
                void AddEvent(string eventDefinitionName, CustomEventData eventDefinition)
                {
                    if (!eventDefinitions.ContainsKey(eventDefinitionName))
                    {
                        eventDefinitions.Add(eventDefinitionName, eventDefinition);
                    }
                    else
                    {
                        Log.Logger.Log($"Duplicate event defintion name, {eventDefinitionName} could not be registered!", Logger.Level.Error);
                    }
                }

                IEnumerable<CustomData>? eventDefinitionsRaw =
                    customBeatmapData.customData.Get<List<object>>(EVENT_DEFINITIONS)?.Cast<CustomData>();
                if (eventDefinitionsRaw != null)
                {
                    foreach (CustomData eventDefinitionRaw in eventDefinitionsRaw)
                    {
                        string eventName = eventDefinitionRaw.Get<string>(NAME) ?? throw new InvalidOperationException("Failed to retrieve event name.");
                        string type = eventDefinitionRaw.Get<string>(TYPE) ?? throw new InvalidOperationException("Failed to retrieve event type.");
                        CustomData data = eventDefinitionRaw.Get<CustomData>("_data")
                                                           ?? throw new InvalidOperationException("Failed to retrieve event data.");

                        AddEvent(eventName, new CustomEventData(-1, type, data));
                    }
                }
            }

            // new deserialize stuff should make these unnecessary
            ////customBeatmapData.customData["tracks"] = trackManager.Tracks;
            ////customBeatmapData.customData["pointDefinitions"] = pointDefinitions;
            ////customBeatmapData.customData["eventDefinitions"] = eventDefinitions;

            // Currently used by Chroma.GameObjectTrackController
            container.Bind<Dictionary<string, Track>>().FromInstance(trackManager.Tracks).AsSingle();

            IReadOnlyList<CustomEventData> customEventsData = customBeatmapData
                .GetBeatmapDataItems<CustomEventData>()
                .Concat(eventDefinitions.Values)
                .ToArray();

            IReadOnlyList<BeatmapEventData> beatmapEventDatas = customBeatmapData.GetBeatmapDataItems<BasicBeatmapEventData>()
                .Cast<BeatmapEventData>()
                .Concat(customBeatmapData.GetBeatmapDataItems<SpawnRotationBeatmapEventData>())
                .Concat(customBeatmapData.GetBeatmapDataItems<BPMChangeBeatmapEventData>())
                .Concat(customBeatmapData.GetBeatmapDataItems<LightColorBeatmapEventData>())
                .Concat(customBeatmapData.GetBeatmapDataItems<LightRotationBeatmapEventData>())
                .Concat(customBeatmapData.GetBeatmapDataItems<ColorBoostBeatmapEventData>())
                .ToArray();

            IReadOnlyList<BeatmapObjectData> objectDatas = customBeatmapData.GetBeatmapDataItems<NoteData>()
                .Cast<BeatmapObjectData>()
                .Concat(customBeatmapData.GetBeatmapDataItems<ObstacleData>())
                .ToArray();

            object[] inputs =
            {
                customBeatmapData,
                trackManager,
                pointDefinitions,
                trackManager.Tracks,
                customEventsData.ToList(),
                beatmapEventDatas,
                objectDatas,
                container,
                leftHanded
            };

            DataDeserializer[] deserializers = _customDataDeserializers.Where(n => n.Enabled).ToArray();

            foreach (DataDeserializer deserializer in deserializers)
            {
                deserializer.InjectedInvokeEarly(inputs);
            }

            foreach (DataDeserializer deserializer in deserializers)
            {
                Dictionary<CustomEventData, ICustomEventCustomData> customEventCustomDatas = deserializer.InjectedInvokeCustomEvent(inputs);
                Dictionary<BeatmapEventData, IEventCustomData> eventCustomDatas = deserializer.InjectedInvokeEvent(inputs);
                Dictionary<BeatmapObjectData, IObjectCustomData> objectCustomDatas = deserializer.InjectedInvokeObject(inputs);

                Log.Logger.Log($"Binding [{deserializer.Id}].", Logger.Level.Trace);

                container.Bind<DeserializedData>()
                    .WithId(deserializer.Id)
                    .FromInstance(new DeserializedData(customEventCustomDatas, eventCustomDatas, objectCustomDatas));
            }
        }
    }
}
