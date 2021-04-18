using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Castaway.Assets;
using Castaway.Core;
using Castaway.Levels;
using Castaway.Levels.Controllers.Rendering;
using Castaway.Levels.Controllers.Storage;
using Castaway.Math;
using Castaway.Render;
using static Castaway.Assets.AssetManager;

namespace Castaway.Serializable
{
    public class LevelAssetLoader : IAssetLoader
    {
        private static readonly IEnumerable<Type> ControllerTypes = 
            AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
                .Concat(CastawayCore.StartupAssembly.GetTypes())
                .Where(t => t.BaseType == typeof(Controller));
        
        public IEnumerable<string> FileExtensions { get; } = new[] {"lvl"};
        private static readonly Dictionary<string, string> Variables = new Dictionary<string, string>();
        
        public object LoadFile(string path)
        {
            Variables.Clear();
            var level = new Level();
            var lines = File.ReadAllLines(path).Select(s => s.Trim());
            ReadLevel(ref level, lines.GetEnumerator());
            return level;
        }

        private static void ReadLevel(ref Level level, IEnumerator<string> lines)
        {
            while (lines.MoveNext())
            {
                var line = lines.Current;
                if(line!.StartsWith('#') || line.Length == 0) continue;
                foreach (var (k, v) in Variables) line = line.Replace($"${{{k}}}", v);
                var parts = line.Split(' ', 3);
                
                switch (parts[0])
                {
                    case "Set" when parts.Length == 3:
                        Variables[parts[1]] = parts[2];
                        break;

                    case "InitialCamera" when parts.Length == 2:
                        level.CurrentCamera = uint.Parse(parts[1]);
                        break;

                    case "Object" when parts.Length == 1:
                    {
                        var @ref = level.Create();
                        var o = @ref.Object;
                        ReadObject(ref o, lines);
                        @ref.Object = o;
                        break;
                    }

                    case "Object" when parts.Length == 2:
                    {
                        var @ref = level.Create(DeserializeObject(typeof(string), parts) as string);
                        var o = @ref.Object;
                        ReadObject(ref o, lines);
                        @ref.Object = o;
                        break;
                    }
                    
                    default:
                        throw new ApplicationException($"Invalid line in level file: `{line}`");
                }
            }
        }

        private static void ReadObject(ref LevelObject obj, IEnumerator<string> lines)
        {
            while (lines.MoveNext())
            {
                var line = lines.Current;
                if(line!.StartsWith('#') || line.Length == 0) continue;
                foreach (var (k, v) in Variables) line = line.Replace($"${{{k}}}", v);
                var parts = line.Split(' ');
                switch (parts[0])
                {
                    case "Position" when parts.Length == 3:
                        obj.Position = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), 0);
                        break;
                    case "Position" when parts.Length == 4:
                        obj.Position = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                        break;
                    case "Rotation" when parts.Length == 1:
                        obj.Rotation = new Vector3(0, 0, float.Parse(parts[1]));
                        break;
                    case "Rotation" when parts.Length == 4:
                        obj.Rotation = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                        break;
                    case "Scale" when parts.Length == 3:
                        obj.Scale = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), 0);
                        break;
                    case "Scale" when parts.Length == 4:
                        obj.Scale = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                        break;
                    case "Controller" when parts.Length == 3 && parts[2] == "Empty":
                        obj.Add(CreateController(parts[1]));
                        break;
                    case "Controller" when parts.Length == 2:
                    {
                        var c = CreateController(parts[1]);
                        ReadController(ref c, lines);
                        obj.Add(c);
                        break;
                    }
                    case "Mesh" when parts.Length == 2:
                        obj.Add(new MeshLoaderController {Asset = parts[1]});
                        break;
                    case "Mesh" when parts.Length == 3 && parts[2] == "Z_Up":
                        obj.Add(new MeshLoaderController {Asset = parts[1], ZUp = true});
                        break;
                    case "Texture" when parts.Length == 3 && parts[2] == "Empty":
                        obj.Add(new TextureController {Texture = Get<Texture>(Index(parts[1]))});
                        break;
                    case "Texture" when parts.Length == 2:
                    {
                        Controller c = new TextureController {Texture = Get<Texture>(Index(parts[1]))};
                        ReadController(ref c, lines);
                        obj.Add(c);
                        break;
                    }
                    case "End" when parts.Length == 1:
                        return;
                    default:
                        throw new ApplicationException($"Invalid line in level file: `{line}` (Object {obj.Ref.Index})");
                }
            }
        }

        private static void ReadController(ref Controller controller, IEnumerator<string> lines)
        {
            var type = controller.GetType();
            while (lines.MoveNext())
            {
                var line = lines.Current;
                if(line!.StartsWith('#') || line.Length == 0) continue;
                foreach (var (key, val) in Variables)
                    line = line.Replace($"${{{key}}}", val);
                var parts = line.Split(' ');
                if (parts[0] == "End") return;
                
                var setting = type.GetMember(parts[0]).Single();
                if (setting.MemberType != MemberTypes.Property && setting.MemberType != MemberTypes.Field)
                    throw new ApplicationException($"Cannot set setting {parts[0]}");
                
                var t = setting.MemberType switch
                {
                    MemberTypes.Field => ((FieldInfo)setting).FieldType,
                    MemberTypes.Property => ((PropertyInfo)setting).PropertyType,
                    _ => throw new ArgumentOutOfRangeException()
                };
                var v = setting.MemberType switch
                {
                    MemberTypes.Field => ((FieldInfo)setting).GetValue(controller),
                    MemberTypes.Property => ((PropertyInfo)setting).GetValue(controller),
                    _ => throw new ArgumentOutOfRangeException()
                };

                object value;
                if (t.IsArray)
                {
                    var list = new List<object>();
                    while (lines.MoveNext())
                    {
                        line = lines.Current;
                        if (line == "End") break;
                        parts = line!.Split(' ');
                        list.Add(DeserializeObject(t, parts, 0));
                    }
                    value = list.ToArray();
                }
                else if ($"{t.Namespace}.{t.Name}" == "System.Collections.Generic.Dictionary`2")
                {
                    var types = t.GenericTypeArguments;
                    while (lines.MoveNext())
                    {
                        line = lines.Current;
                        if(line!.StartsWith('#') || line.Length == 0) continue;
                        foreach (var (key, val) in Variables)
                            line = line.Replace($"${{{key}}}", val);
                        if (line == "End") break;
                        parts = line!.Split(' ');
                        var a = DeserializeObject(types[0], parts, 0, false);
                        var b = DeserializeObject(types[1], parts);
                        t.GetProperty("Item")!.SetValue(v, b, new []{a});
                    }
                    value = v;
                }
                else value = DeserializeObject(t, parts);

                switch (setting.MemberType)
                {
                    case MemberTypes.Property:
                        ((PropertyInfo) setting).SetValue(controller, value);
                        break;
                    case MemberTypes.Field:
                        ((FieldInfo) setting).SetValue(controller, value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static object DeserializeObject(Type t, IReadOnlyList<string> parts, int o = 1, bool multiValue = true)
        {
            object value;
            if (t == typeof(byte)) value         = byte.Parse(parts[o]);
            else if (t == typeof(short)) value   = short.Parse(parts[o]);
            else if (t == typeof(int)) value     = int.Parse(parts[o]);
            else if (t == typeof(long)) value    = long.Parse(parts[o]);
            else if (t == typeof(sbyte)) value   = sbyte.Parse(parts[o]);
            else if (t == typeof(ushort)) value  = ushort.Parse(parts[o]);
            else if (t == typeof(uint)) value    = uint.Parse(parts[o]);
            else if (t == typeof(ulong)) value   = ulong.Parse(parts[o]);
            else if (t == typeof(float)) value   = float.Parse(parts[o]);
            else if (t == typeof(double)) value  = double.Parse(parts[o]);
            else if ((t == typeof(string) || parts[o].StartsWith('"') && parts[^1].EndsWith('"')) && multiValue) value
                                                 = Regex.Unescape(parts[o][1..^1]);
            else if (t == typeof(bool)) value    = bool.Parse(parts[o]);
            else if (t == typeof(Vector2) && multiValue) value = new Vector2(float.Parse(parts[o]), float.Parse(parts[o+1]));
            else if (t == typeof(Vector3) && multiValue) value = new Vector3(float.Parse(parts[o]), float.Parse(parts[o+1]), float.Parse(parts[o+2]));
            else if (t == typeof(Vector4) && multiValue) value = new Vector4(float.Parse(parts[o]), float.Parse(parts[o+1]), float.Parse(parts[o+2]), float.Parse(parts[o+3]));
            else if (t.IsEnum) value             = Enum.Parse(t, parts[o]);
            else throw new ApplicationException($"Non-serializable type {t}");
            return value;
        }
        
        private static Controller CreateController(string name)
        {
            try
            {
                return (Controller) Activator.CreateInstance(ControllerTypes.First(t =>
                    t.FullName == name ||
                    t.FullName == $"Castaway.Levels.Controllers.{name}Controller" ||
                    t.FullName == $"Castaway.Levels.Controllers.Rendering.{name}Controller" ||
                    t.FullName == $"Castaway.Levels.Controllers.Controls.{name}Controller" ||
                    t.FullName == $"Castaway.Levels.Controllers.Renderers.{name}Controller" ||
                    t.FullName == $"Castaway.Levels.Controllers.Lights.{name}Controller" ||
                    t.FullName == $"Castaway.Levels.Controllers.Storage.{name}Controller"));
            }
            catch (InvalidOperationException e)
            {
                throw new ApplicationException($"Controller was not found: {name}", e);
            }
        }
    }
}