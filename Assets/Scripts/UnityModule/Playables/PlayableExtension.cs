using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace UnityModule.Playables {

    public static class PlayableExtension {

        private static readonly Dictionary<Type, Type> REFERENCE_VALUE_TYPE_MAP = new Dictionary<Type, Type>() {
            { typeof(ControlTrack), typeof(GameObject) },
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

        public static void SetGenericBindingByTrackName<T>(this PlayableDirector playableDirector, string trackName, T value) where T : Object {
            playableDirector.SetGenericBindingByTrackNameAndPlayableAssetName(trackName, string.Empty, value);
        }

        public static void SetGenericBindingByPlayableAssetName<T>(this PlayableDirector playableDirector, string playableAssetName, T value) where T : Object {
            playableDirector.SetGenericBindingByTrackNameAndPlayableAssetName(string.Empty, playableAssetName, value);
        }

        public static void SetGenericBindingByTrackNameAndPlayableAssetName<T>(this PlayableDirector playableDirector, string trackName, string playableAssetName, T value) where T : Object {
            playableDirector
                .playableAsset
                // 先ずは Timeline の Track を絞り込み
                .FindAllPlayableAsset(
                    // 渡された型とトラック名に応じて、sourceObject をフィルタリング
                    (streamName, playableAsset) =>
                        playableAsset is TrackAsset
                        && (string.IsNullOrEmpty(trackName) || streamName == trackName)
                        && GENERIC_BINDING_TYPE_MAP.ContainsKey(playableAsset.GetType())
                        && GENERIC_BINDING_TYPE_MAP[playableAsset.GetType()] == typeof(T)
                )
                // 続いて Track 内の TimelineClip をもとに PlayableBinding を絞り込み
                .FindAllPlayableBinding(
                    // TrackAsset が内包する TimelineClip の displayName でフィルタリング
                    (streamName, playableAsset) =>
                        playableAsset is TrackAsset
                        && ((TrackAsset)playableAsset)
                            .GetClips()
                            .Any(
                                timelineClip =>
                                    (string.IsNullOrEmpty(playableAssetName) || timelineClip.displayName == playableAssetName)
                            )
                )
                .ToList()
                // GenericBinding を設定
                .ForEach(playableAsset => playableDirector.SetGenericBinding(playableAsset.sourceObject, value));
            playableDirector.RebuildGraph();
        }

        public static void SetReferenceValueByTrackName<T>(this PlayableDirector playableDirector, string trackName, T value) where T : Object {
            playableDirector.SetReferenceValueByTrackNameAndPlayableAssetName(trackName, string.Empty, value);
        }

        public static void SetReferenceValueByPlayableAssetName<T>(this PlayableDirector playableDirector, string playableAssetName, T value) where T : Object {
            playableDirector.SetReferenceValueByTrackNameAndPlayableAssetName(string.Empty, playableAssetName, value);
        }

        public static void SetReferenceValueByTrackNameAndPlayableAssetName<T>(this PlayableDirector playableDirector, string trackName, string playableAssetName, T value) where T : Object {
            playableDirector
                .playableAsset
                // 先ずは Timeline の Track を絞り込み
                .FindAllPlayableAsset(
                    // 渡された型とトラック名に応じて、sourceObject をフィルタリング
                    (streamName, playableAsset) =>
                        playableAsset is TrackAsset
                        && (string.IsNullOrEmpty(trackName) || streamName == trackName)
                        && REFERENCE_VALUE_TYPE_MAP.ContainsKey(playableAsset.GetType())
                        && REFERENCE_VALUE_TYPE_MAP[playableAsset.GetType()] == typeof(T)
                )
                // 続いて Track 内の TimelineClip をもとに PlayableAsset を絞り込み
                .FindAllPlayableAsset(
                    // TrackAsset が内包する TimelineClip の displayName でフィルタリング
                    (streamName, playableAsset) =>
                        ((TrackAsset)playableAsset)
                            .GetClips()
                            .Any(
                                timelineClip =>
                                    (string.IsNullOrEmpty(playableAssetName) || timelineClip.displayName == playableAssetName)
                                    && GET_PROPERTY_NAME_DELEGATE_MAP.ContainsKey(timelineClip.asset.GetType())
                            )
                )
                // TrackAsset 内の TimelineClip が保持する PlayableAsset に変換
                .Select(playableAsset => ((TrackAsset)playableAsset).GetClips().Select(timelineClip => timelineClip.asset as PlayableAsset))
                // ネストした IEnumerable をフラットにする
                .Aggregate((a, b) => a.Concat(b))
                .ToList()
                // ReferenceValue を設定
                .ForEach(playableAsset => playableDirector.SetReferenceValue(GET_PROPERTY_NAME_DELEGATE_MAP[playableAsset.GetType()](playableAsset), value));
            playableDirector.RebuildGraph();
        }

        public static void SetSpeed(this PlayableDirector playableDirector, int speed, int rootPlayableIndex = 0) {
            playableDirector.playableGraph.GetRootPlayable(rootPlayableIndex).SetSpeed(speed);
        }

        private static IEnumerable<PlayableBinding> FindAllPlayableBinding(this PlayableAsset playableAsset, Func<string, PlayableAsset, bool> conditionForPlayableAsset = null) {
            return playableAsset
                .outputs
                .Where(x => x.sourceObject is PlayableAsset)
                .Where(x => conditionForPlayableAsset == default(Func<string, PlayableAsset, bool>) || conditionForPlayableAsset(x.streamName, x.sourceObject as PlayableAsset));
        }

        private static IEnumerable<PlayableBinding> FindAllPlayableBinding(this IEnumerable<PlayableAsset> enumerable, Func<string, PlayableAsset, bool> conditionForPlayableAsset = null) {
            List<PlayableAsset> playableAssetList = enumerable.ToList();
            if (!playableAssetList.Any()) {
                return new List<PlayableBinding>();
            }
            return playableAssetList
                .Select(x => x.FindAllPlayableBinding(conditionForPlayableAsset))
                .Aggregate((a, b) => a.Concat(b))
                .Distinct();
        }

        private static IEnumerable<PlayableAsset> FindAllPlayableAsset(this PlayableAsset playableAsset, Func<string, PlayableAsset, bool> conditionForPlayableAsset = null) {
            return playableAsset
                .FindAllPlayableBinding(conditionForPlayableAsset)
                .Select(x => x.sourceObject as PlayableAsset);
        }

        private static IEnumerable<PlayableAsset> FindAllPlayableAsset(this IEnumerable<PlayableAsset> enumerable, Func<string, PlayableAsset, bool> conditionForPlayableAsset = null) {
            List<PlayableAsset> playableAssetList = enumerable.ToList();
            if (!playableAssetList.Any()) {
                return new List<PlayableAsset>();
            }
            return playableAssetList
                .Select(x => x.FindAllPlayableAsset(conditionForPlayableAsset))
                .Aggregate((a, b) => a.Concat(b))
                .Distinct();
        }

    }

}