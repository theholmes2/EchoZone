using System;
using EchoZone.Audio;
using NUnit.Framework;
using UnityEngine;

/// <summary>실제 음원 없이 사운드 조회·범위 보정·설정 누락 방어를 검증합니다.</summary>
public sealed class GameplaySoundTests
{
    /// <summary>동일 ID는 첫 항목만 사용하며 미정의 값은 버립니다.</summary>
    [Test] public void DuplicateIdsUseFirstEntry()
    {
        var map = GameplaySoundBrick.BuildIndex(new[] { GameplaySoundId.PlayerShot, GameplaySoundId.PlayerShot, (GameplaySoundId)999 });
        Assert.AreEqual(1, map.Count); Assert.AreEqual(0, map[GameplaySoundId.PlayerShot]);
    }

    /// <summary>UI와 두 3D 채널의 역할을 분리합니다.</summary>
    [Test] public void ChannelsAreSeparated()
    {
        Assert.AreEqual(GameplaySoundChannel.Weapon, GameplaySoundBrick.Channel(GameplaySoundId.PlayerShot));
        Assert.AreEqual(GameplaySoundChannel.World, GameplaySoundBrick.Channel(GameplaySoundId.PoliceShot));
        Assert.AreEqual(GameplaySoundChannel.UI, GameplaySoundBrick.Channel(GameplaySoundId.TheftSucceeded));
    }

    /// <summary>정상 범위와 비정상 실수를 안전하게 보정합니다.</summary>
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(-1f, 0.1f)]
    [TestCase(10f, 3f)]
    public void PitchIsSafe(float input, float expected) => Assert.AreEqual(expected, GameplaySoundBrick.Clamp(input, 0.1f, 3, 1));

    /// <summary>설정 정규화는 첫 클립을 유지하고 모든 누락 ID를 준비합니다.</summary>
    [Test] public void NormalizePreservesFirstAndFillsMissing()
    {
        var config = ScriptableObject.CreateInstance<GameplaySoundConfig>();
        try
        {
            var first = new GameplaySoundConfig.Entry { Id = GameplaySoundId.PlayerShot, Volume = 4, Pitch = float.NaN };
            config.Entries = new[] { first, new GameplaySoundConfig.Entry { Id = GameplaySoundId.PlayerShot }, null };
            config.Normalize();
            Assert.AreEqual(Enum.GetValues(typeof(GameplaySoundId)).Length, config.Entries.Length);
            Assert.AreSame(first, config.Entries[0]); Assert.AreEqual(1, first.Volume); Assert.AreEqual(1, first.Pitch);
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    /// <summary>시스템 미배치 요청은 예외 없이 건너뜁니다.</summary>
    [Test] public void MissingSystemIsSilent()
    {
        Assert.DoesNotThrow(() => GameplaySoundGlue.PlayUI(GameplaySoundId.RequestSucceeded));
        Assert.DoesNotThrow(() => GameplaySoundGlue.PlayWorld(GameplaySoundId.PlayerShot, Vector3.zero));
    }

    /// <summary>배치할 프리팹은 세 채널이 다른 AudioSource를 참조하며 자동 재생하지 않습니다.</summary>
    [Test] public void PrefabHasSeparateSerializedVoicesAndConfig()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameplaySoundSystem.prefab");
        Assert.IsNotNull(prefab);
        var glue = prefab.GetComponent<GameplaySoundGlue>();
        var data = new UnityEditor.SerializedObject(glue);
        Assert.IsNotNull(data.FindProperty("config").objectReferenceValue);
        var seen = new System.Collections.Generic.HashSet<AudioSource>();
        foreach (string field in new[] { "uiSources", "weaponSources", "worldSources" })
        {
            var array = data.FindProperty(field); Assert.Greater(array.arraySize, 0);
            for (int i = 0; i < array.arraySize; i++)
            {
                var source = array.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                Assert.IsNotNull(source); Assert.IsTrue(seen.Add(source)); Assert.IsFalse(source.playOnAwake);
                Assert.IsFalse(source.loop); Assert.AreEqual(field == "uiSources" ? 0f : 1f, source.spatialBlend);
            }
        }
    }

    /// <summary>전 항목의 클립을 비워둔 실제 프리팹도 재생 요청에서 예외를 만들지 않습니다.</summary>
    [Test] public void EmptyClipsOnConfiguredPrefabAreSilent()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameplaySoundSystem.prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        var copy = ScriptableObject.CreateInstance<GameplaySoundConfig>(); copy.Normalize();
        try
        {
            var glue = root.GetComponent<GameplaySoundGlue>();
            var data = new UnityEditor.SerializedObject(glue);
            data.FindProperty("config").objectReferenceValue = copy; data.ApplyModifiedPropertiesWithoutUndo();
            typeof(GameplaySoundGlue).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(glue, null);
            foreach (GameplaySoundId id in Enum.GetValues(typeof(GameplaySoundId)))
            {
                Assert.DoesNotThrow(() => GameplaySoundGlue.PlayUI(id));
                Assert.DoesNotThrow(() => GameplaySoundGlue.PlayWorld(id, Vector3.zero));
            }
            foreach (var source in root.GetComponentsInChildren<AudioSource>()) Assert.IsFalse(source.isPlaying);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(copy); }
    }
}
