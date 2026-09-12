using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project333.Runtime.Infrastructure.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project333.Editor
{
    public static class Project333ReleasedCardVisualBuilder
    {
        private const string Root = "Assets/Project333/ScriptableObjects/StarterTen";
        private const string ArtworkRoot = "Assets/Project333/Resources/Project333/CardArtwork";
        private const float FramesPerSecond = 12f;
        public static readonly string[] CardIds =
        {
            "A-212", "A-301", "OrcWarrior", "Skeleton", "Werewolf", "Zombie",
            "BiochemicalBomb", "HuanShu", "TimedBomb", "GaebangBranch",
            "MerchantCaravan", "NuclearPowerPlant", "PowerPlant", "Gu",
            "TenThousandYearSnowGinseng", "PowerBank", "ManaStone"
        };

        [MenuItem("Tools/Project333/Cards/Connect Released Card Visuals")]
        public static void Build()
        {
            BuildCards(CardIds);
        }

        [MenuItem("Tools/Project333/Cards/Connect A-212 Visuals")]
        public static void BuildA212()
        {
            BuildCards(new[] { "A-212" });
        }

        private static void BuildCards(IEnumerable<string> cardIds)
        {
            var database = JsonCardDefinitionDatabase.FromJson(File.ReadAllText(
                "Assets/Project333/Resources/Project333/Data/cards.json"));
            var catalog = AssetDatabase.LoadAssetAtPath<CardDefinitionCatalogAsset>(
                Root + "/StarterTenCardCatalog.asset");
            if (catalog == null)
                throw new InvalidOperationException("The existing card catalog was not found.");
            var cards = new List<CardDefinitionAsset>(catalog.Cards ?? Array.Empty<CardDefinitionAsset>());

            foreach (var id in cardIds)
            {
                var record = database.Cards.Single(c => c.Id == id);
                ImportArtwork(id);
                var path = Root + "/Cards/" + id + ".asset";
                var asset = cards.FirstOrDefault(c => c != null && c.CardId == id)
                    ?? AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>(path);
                if (asset == null)
                {
                    asset = CreateCardAsset(record.DefinitionType);
                    AssetDatabase.CreateAsset(asset, path);
                }
                ConfigureCard(asset, record);
                if (record.DefinitionType == JsonCardDefinitionKind.Unit ||
                    record.DefinitionType == JsonCardDefinitionKind.Building)
                {
                    var directory = record.DefinitionType == JsonCardDefinitionKind.Unit
                        ? "Assets/Unit/" + (id == "OrcWarrior" ? "Orc" : id)
                        : "Assets/Building/" + id;
                    var idle = ImportFrames(directory + "/Idle.png");
                    var controller = asset.BoardAnimatorController
                        ?? CreateController(directory, id);
                    // Keep user-assigned artwork/controllers on subsequent menu runs.
                    asset.ConfigureBoardVisualsForTests(asset.BoardSprite ?? idle[0], controller);
                }
                if (!cards.Contains(asset))
                    cards.Add(asset);
                EditorUtility.SetDirty(asset);
            }
            catalog.ConfigureForTests(cards.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Connected released card artwork and occupant animations.");
        }

        private static CardDefinitionAsset CreateCardAsset(JsonCardDefinitionKind kind)
        {
            switch (kind)
            {
                case JsonCardDefinitionKind.Unit: return ScriptableObject.CreateInstance<UnitCardDefinitionAsset>();
                case JsonCardDefinitionKind.Building: return ScriptableObject.CreateInstance<BuildingCardDefinitionAsset>();
                case JsonCardDefinitionKind.ScriptedSpell: return ScriptableObject.CreateInstance<ScriptedSpellCardDefinitionAsset>();
                case JsonCardDefinitionKind.PersistentResourceSpell: return ScriptableObject.CreateInstance<PersistentResourceSpellCardDefinitionAsset>();
                default: throw new InvalidOperationException("Unsupported release card kind: " + kind);
            }
        }

        private static ResourceSetData Resources(JsonResourceSet value)
        {
            value = value ?? new JsonResourceSet();
            return new ResourceSetData(value.Mana, value.Qi, value.Power, value.Gold);
        }

        private static void ConfigureCard(CardDefinitionAsset asset, JsonCardDefinitionRecord r)
        {
            asset.ConfigureBaseForTests(r.Id, r.DisplayName, Resources(r.Cost));
            asset.ConfigureMetadataForTests(r.Rarity, r.Affiliation, r.ChargeTileFootprint,
                r.EffectText, r.SpecialEffectText);
            asset.ConfigureReplicateForTests(r.HasReplicate);
            if (asset is UnitCardDefinitionAsset unit)
                unit.ConfigureForTests(r.AttackType, r.Attack, r.Health, r.CanMove, r.IsScience,
                    r.SciencePowerUpkeep, Resources(r.TurnStartResourceGain), r.MaxAttacksPerTurn,
                    r.HasRush || r.CanAttackOnSummon, r.HitsPerAttack, r.HasBerserker,
                    r.HasEndure, r.HasShielder, r.HasLifeSteal, r.DamageType,
                    r.PhysicalDefense, r.MagicDefense, r.HasRobot, r.SealboundOwnerTurnStarts,
                    r.HasHiding, r.HasFlying, r.SpellPower, r.InvincibleDuration,
                    r.InvincibleOwnerTurns, r.HasPiercing);
            else if (asset is BuildingCardDefinitionAsset building)
                building.ConfigureForTests(r.CanAttack, r.Attack, r.Health,
                    Resources(r.TurnStartResourceGain), r.CanAttackOnSummon, r.DamageType,
                    r.PhysicalDefense, r.MagicDefense, r.SciencePowerUpkeep,
                    r.SealboundOwnerTurnStarts, r.HasFlying, r.SpellPower,
                    r.InvincibleDuration, r.InvincibleOwnerTurns, r.HasPiercing);
            else if (asset is ScriptedSpellCardDefinitionAsset spell)
                spell.ConfigureForTests(r.EffectId, r.Damage, r.DamageType, r.TriggerCount);
            else if (asset is PersistentResourceSpellCardDefinitionAsset persistent)
                persistent.ConfigureForTests(r.EffectId, Resources(r.TurnStartResourceGain),
                    r.EndConditionText, r.OwnerTurnStartsRemaining);
        }

        private static void ImportArtwork(string id)
        {
            var path = ArtworkRoot + "/" + id + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing artwork: " + path);
            if (Project333CardArtworkImporter.Normalize(importer))
                importer.SaveAndReimport();
        }

        private static Sprite[] ImportFrames(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing animation sheet: " + path);
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (height != 512 || width % 512 != 0)
                throw new InvalidOperationException("Expected a horizontal 512x512 frame strip: " + path);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 8192;
                var frames = new SpriteMetaData[width / 512];
                for (var i = 0; i < frames.Length; i++)
                    frames[i] = new SpriteMetaData
                    {
                        name = Path.GetFileNameWithoutExtension(path) + "_" + i,
                        rect = new Rect(i * 512, 0, 512, 512),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
#pragma warning disable CS0618
                importer.spritesheet = frames;
#pragma warning restore CS0618
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => s.rect.x).ToArray();
            if (sprites.Length != width / 512)
                throw new InvalidOperationException("Unexpected sliced frame count: " + path);
            return sprites;
        }

        private static AnimatorController CreateController(string directory, string id)
        {
            var path = directory + "/" + id + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var action in new[] { "Idle", "Run", "Attack", "BeAttacked", "Death", "Disabled" })
            {
                var sheet = directory + "/" + action + ".png";
                if (!File.Exists(sheet))
                    continue;
                var sprites = ImportFrames(sheet);
                var clipPath = directory + "/" + action + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = action, frameRate = FramesPerSecond };
                    var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
                    for (var i = 0; i < sprites.Length; i++)
                        keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = sprites[i] };
                    // Hold the final frame for a complete frame interval.
                    keys[sprites.Length] = new ObjectReferenceKeyframe
                        { time = sprites.Length / FramesPerSecond, value = sprites[sprites.Length - 1] };
                    AnimationUtility.SetObjectReferenceCurve(clip,
                        EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = action == "Idle" || action == "Run" || action == "Disabled";
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clips[action] = clip;
                AddState(machine, action, clip);
            }
            AddState(machine, "Drained", clips.TryGetValue("Disabled", out var drained) ? drained : clips["Idle"]);
            if (!clips.ContainsKey("Run"))
                AddState(machine, "Run", clips["Idle"]);
            machine.defaultState = machine.states.Single(s => s.state.name == "Idle").state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            if (machine.states.Any(s => s.state.name == name))
                return;
            machine.AddState(name).motion = clip;
        }
    }
}
