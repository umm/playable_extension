using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace UnityModule.Playables {

    public static class PlayableExtension {

        private static readonly Dictionary<Type, Type> EXPOSED_REFERENCE_TYPE_MAP = new Dictionary<Type, Type>() {
            { typeof(ControlPlayableAsset), typeof(GameObject) },
        };

        private static readonly Dictionary<Type, Func<PlayableAsset, PropertyName>> GET_PROPERTY_NAME_DELEGATE_MAP = new Dictionary<Type, Func<PlayableAsset, PropertyName>>() {
            {
                typeof(ControlPlayableAsset),
                (playableAsset) => ((ControlPlayableAsset)playableAsset).sourceGameObject.exposedName
            }
        };

        private static readonly Dictionary<Type, Type> GENERIC_BINDING_TYPE_MAP = new Dictionary<Type, Type>() {
            { typeof(AnimationTrack), typeof(Animator) },
            { typeof(AudioTrack), typeof(AudioSource) },
            { typeof(ActivationTrack), typeof(GameObject) },
        };

        public static void SetReferenceValueByPlayableAssetName<T>(this PlayableDirector playableDirector, string playableAssetName, T value) where T : Object {
            (playableDirector.playableAsset as TimelineAsset)?
                .GetOutputTracks()
                .ToList()
                .SelectMany(x => x.GetClips())
                .ToList()
                .FindAll(x =>
                    x.displayName == (string.IsNullOrEmpty(playableAssetName) ? x.asset.GetType().Name : playableAssetName)
                    && EXPOSED_REFERENCE_TYPE_MAP.ContainsKey(x.asset.GetType())
                    && EXPOSED_REFERENCE_TYPE_MAP[x.asset.GetType()] == typeof(T)
                    && GET_PROPERTY_NAME_DELEGATE_MAP.ContainsKey(x.asset.GetType())
                )
                .Select(x => (PlayableAsset)x.asset)
                .ToList()
                .ForEach(
                    (playableAsset) => {
                        playableDirector.SetReferenceValue(GET_PROPERTY_NAME_DELEGATE_MAP[playableAsset.GetType()](playableAsset), value);
                    }
                );
        }

        public static void SetGenericBindingByTrackName<T>(this PlayableDirector playableDirector, string trackName, T value) where T : Object {
            playableDirector.SetGenericBindingByTrackNameAndPlayableAssetName(trackName, string.Empty, value);
        }

        public static void SetGenericBindingByPlayableAssetName<T>(this PlayableDirector playableDirector, string playableAssetName, T value) where T : Object {
            playableDirector.SetGenericBindingByTrackNameAndPlayableAssetName(string.Empty, playableAssetName, value);
        }

        public static void SetGenericBindingByTrackNameAndPlayableAssetName<T>(this PlayableDirector playableDirector, string trackName, string playableAssetName, T value) where T : Object {
            playableDirector
                .playableAsset
                .outputs
                .ToList()
                // 渡された型とトラック名に応じて、sourceObject をフィルタリングする
                .FindAll(x =>
                    x.sourceObject is TrackAsset
                    && (string.IsNullOrEmpty(trackName) || x.streamName == trackName)
                    && GENERIC_BINDING_TYPE_MAP.ContainsKey(x.sourceObject.GetType())
                    && GENERIC_BINDING_TYPE_MAP[x.sourceObject.GetType()] == typeof(T)
                )
                // TrackAsset が内包する TimelineClip の displayName でフィルタリング
                .Where(x => string.IsNullOrEmpty(playableAssetName) || ((TrackAsset)x.sourceObject).GetClips().Any(y => y.displayName == playableAssetName))
                .ToList()
                .ForEach(
                    (playableBinding) => {
                        playableDirector.SetGenericBinding(playableBinding.sourceObject, value);
                    }
                );
        }

    }

}