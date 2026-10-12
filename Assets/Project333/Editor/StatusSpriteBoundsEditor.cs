using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project333.Runtime.Infrastructure.Data;
using Project333.Runtime.Presentation.Battle;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Project333.Editor
{
    // Bakes numeric metadata only. The Resources asset holds no Sprite/Texture/controller dependencies.
    public static class StatusSpriteBoundsEditor
    {
        public const string AssetPath="Assets/Project333/Resources/Project333/StatusEffects/BodyBounds.asset";
        public const byte AlphaThreshold=16;
        static bool _pending;
        public static void QueueRebuild()
        {
            if(_pending)return;
            _pending=true;EditorApplication.delayCall+=RebuildDelayed;
        }
        static void RebuildDelayed()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=RebuildDelayed;return;}
            _pending=false;try { Debug.Log(Rebuild()); }catch(Exception error){Debug.LogException(error);}
        }
        [MenuItem("After333/Visuals/Rebuild Status Sprite Bounds")]
        public static void RebuildFromMenu() { Debug.Log(Rebuild()); }
        public static string Rebuild()
        {
            var cards=AssetDatabase.FindAssets("t:CardDefinitionAsset",new[]{"Assets/Project333/ScriptableObjects"})
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path=>path,StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<CardDefinitionAsset>).Where(card=>card!=null).ToArray();
            var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Unit"}).Select(AssetDatabase.GUIDToAssetPath)
                .Concat(cards.Where(card=>card.BoardSprite!=null).Select(card=>AssetDatabase.GetAssetPath(card.BoardSprite)))
                .Where(path=>path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)&&File.Exists(path)).Distinct().OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            var bounds=new Dictionary<Sprite,Rect>();int textures=0,empty=0;
            foreach(var path in paths)
            {
                var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite=>sprite.name,StringComparer.Ordinal).ToArray();
                if(sprites.Length==0)continue;
                var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    if(!raw.LoadImage(File.ReadAllBytes(path),false))throw new InvalidOperationException("Cannot read source PNG: "+path);
                    textures++;var pixels=raw.GetPixels32();
                    foreach(var sprite in sprites){var rect=ReadBounds(sprite,raw.width,raw.height,pixels);bounds.Add(sprite,rect);if(rect.width<=0||rect.height<=0)empty++;}
                }
                finally{UnityEngine.Object.DestroyImmediate(raw);}
            }
            if(bounds.Count==0)throw new InvalidOperationException("Cannot bake an empty source sprite bounds catalog.");
            var rows=new Dictionary<string,StatusSpriteBoundsAsset.Entry>(StringComparer.Ordinal);
            var rowSources=new Dictionary<string,Sprite>(StringComparer.Ordinal);
            Action<string,Sprite> add=(visualKey,sprite)=>
            {
                if(sprite==null||!bounds.TryGetValue(sprite,out var normalized))return;
                var row=new StatusSpriteBoundsAsset.Entry {VisualKey=visualKey,TextureName=sprite.texture.name,SpriteName=sprite.name,SpriteRect=sprite.rect,NormalizedBounds=normalized};
                var key=StatusSpriteBoundsAsset.CreateKey(row.VisualKey,row.TextureName,row.SpriteName,row.SpriteRect);
                if(rows.TryGetValue(key,out var previous))
                {
                    if(rowSources[key]!=sprite||previous.NormalizedBounds!=normalized)
                        throw new InvalidOperationException("Ambiguous sprite bounds key; preserve controller/art names and report: "+key);
                    return;
                }
                rows.Add(key,row);rowSources.Add(key,sprite);
            };
            // Coverage for every native Unit Sprite, including unused older clips, without ambiguous filename-only runtime lookup.
            foreach(var pair in bounds)add("unit:"+Path.GetDirectoryName(AssetDatabase.GetAssetPath(pair.Key)).Replace('\\','/'),pair.Key);
            foreach(var card in cards)
            {
                var controller=card.BoardAnimatorController;
                if(card.BoardSprite!=null)
                {
                    add(card.CardId,card.BoardSprite);
                    if(controller!=null)add(controller.name,card.BoardSprite);
                }
                if(controller==null)continue;
                foreach(var clip in controller.animationClips.Distinct())
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach(var frame in AnimationUtility.GetObjectReferenceCurve(clip,binding))
                    if(frame.value is Sprite sprite){add(controller.name,sprite);add(card.CardId,sprite);}
            }
            var entries=rows.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>pair.Value).ToArray();
            var asset=AssetDatabase.LoadAssetAtPath<StatusSpriteBoundsAsset>(AssetPath);
            if(asset==null)
            {
                if(File.Exists(AssetPath))throw new InvalidOperationException("Existing bounds asset has an unexpected type; preserve it.");
                EnsureFolder("Assets/Project333/Resources/Project333/StatusEffects");asset=ScriptableObject.CreateInstance<StatusSpriteBoundsAsset>();AssetDatabase.CreateAsset(asset,AssetPath);
            }
            bool changed=asset.Entries.Count!=entries.Length;
            if(!changed)for(int i=0;i<entries.Length;i++)
            {
                var previous=asset.Entries[i];var next=entries[i];
                if(previous==null||previous.VisualKey!=next.VisualKey||previous.TextureName!=next.TextureName||previous.SpriteName!=next.SpriteName||
                    previous.SpriteRect!=next.SpriteRect||previous.NormalizedBounds!=next.NormalizedBounds){changed=true;break;}
            }
            if(changed)
            {
                var serialized=new SerializedObject(asset);var array=serialized.FindProperty("_entries");
                if(array==null||!array.isArray)throw new InvalidOperationException("StatusSpriteBoundsAsset metadata-only schema mismatch.");
                array.arraySize=entries.Length;
                for(int i=0;i<entries.Length;i++)
                {
                    var row=array.GetArrayElementAtIndex(i);var entry=entries[i];
                    row.FindPropertyRelative("VisualKey").stringValue=entry.VisualKey;
                    row.FindPropertyRelative("TextureName").stringValue=entry.TextureName;
                    row.FindPropertyRelative("SpriteName").stringValue=entry.SpriteName;
                    row.FindPropertyRelative("SpriteRect").rectValue=entry.SpriteRect;
                    row.FindPropertyRelative("NormalizedBounds").rectValue=entry.NormalizedBounds;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
            }
            var imageDependencies=AssetDatabase.GetDependencies(AssetPath,true).Where(path=>path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(imageDependencies.Length!=0)throw new InvalidOperationException("BodyBounds metadata must not reference/load texture assets.");
            return "BodyBounds: "+entries.Length+" metadata rows for "+bounds.Count+" unique source Sprites / "+textures+" PNGs, "+empty+" empty-alpha frames; alpha>=16; zero PNG dependencies; original PNG/.meta/import/readable preserved; "+(changed?"updated":"unchanged")+"; GUID "+AssetDatabase.AssetPathToGUID(AssetPath);
        }
        static Rect ReadBounds(Sprite sprite,int width,int height,Color32[] pixels)
        {
            double scaleX=width/(double)sprite.texture.width,scaleY=height/(double)sprite.texture.height;var rect=sprite.rect;
            int x0=Math.Max(0,(int)Math.Floor(rect.xMin*scaleX)),y0=Math.Max(0,(int)Math.Floor(rect.yMin*scaleY));
            int x1=Math.Min(width,(int)Math.Ceiling(rect.xMax*scaleX)),y1=Math.Min(height,(int)Math.Ceiling(rect.yMax*scaleY));
            if(x1<=x0||y1<=y0)throw new InvalidOperationException("Invalid original source rect: "+sprite.name);
            int minX=x1,minY=y1,maxX=-1,maxY=-1;
            for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
            {
                if(pixels[y*width+x].a<AlphaThreshold)continue;
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
            }
            if(maxX<0)return Rect.zero;
            return new Rect((minX-x0)/(float)(x1-x0),(minY-y0)/(float)(y1-y0),
                (maxX-minX+1)/(float)(x1-x0),(maxY-minY+1)/(float)(y1-y0));
        }
        static void EnsureFolder(string path)
        {
            var parts=path.Split('/');var current=parts[0];for(int i=1;i<parts.Length;i++)
            {var next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
    }
    // No InitializeOnLoad bake: adding/reloading this script cannot race the isolated initial import.
    public sealed class StatusSpriteBoundsPostprocessor : AssetPostprocessor
    {
        static bool VisualInput(string path)
        {
            if(string.IsNullOrEmpty(path))return false;
            path=path.Replace('\\','/');
            if(path.StartsWith("Assets/Unit/",StringComparison.OrdinalIgnoreCase))
                return path.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".controller",StringComparison.OrdinalIgnoreCase);
            // Card links and controller aliases can change without a PNG edit. Excludes BodyBounds.asset to prevent recursion.
            return path.StartsWith("Assets/Project333/ScriptableObjects/",StringComparison.OrdinalIgnoreCase)&&
                path.IndexOf("/Cards/",StringComparison.OrdinalIgnoreCase)>=0&&path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase);
        }
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(imported.Any(VisualInput)||deleted.Any(VisualInput)||moved.Any(VisualInput)||movedFrom.Any(VisualInput))StatusSpriteBoundsEditor.QueueRebuild();
        }
    }
    public sealed class StatusSpriteBoundsBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report){Debug.Log(StatusSpriteBoundsEditor.Rebuild());}
    }
}
