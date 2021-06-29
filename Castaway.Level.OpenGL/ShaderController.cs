using System;
using System.Linq;
using Castaway.Assets;
using Castaway.Base;
using Castaway.OpenGL;
using Castaway.Rendering;
using Serilog;

namespace Castaway.Level.OpenGL
{
    [ControllerName("Shader")]
    [Imports(typeof(OpenGLImpl))]
    public class ShaderController : Controller
    {
        private static readonly ILogger Logger = CastawayGlobal.GetLogger();

        private ShaderObject? _previous;

        [LevelSerialized("Asset")] public string AssetName = string.Empty;

        [LevelSerialized("Builtin")] public string BuiltinShaderName = string.Empty;
        public ShaderObject? Shader;

        public override void OnInit(LevelObject parent)
        {
            base.OnInit(parent);
            if (AssetName.Any())
            {
                Shader = AssetLoader.Loader!.GetAssetByName(AssetName).To<ShaderObject>();
                Logger.Debug("Initialized shader controller with asset at {Path}", AssetName);
            }
            else
            {
                Shader = typeof(BuiltinShaders).GetField(BuiltinShaderName)?.GetValue(null) as ShaderObject;
                if (Shader == null)
                    throw new InvalidOperationException(
                        $"Invalid builtin shader name encountered: {BuiltinShaderName}");
                Logger.Debug("Initialized shader controller with builtin shader {Name}", BuiltinShaderName);
            }
        }

        public override void PreRender(LevelObject camera, LevelObject parent)
        {
            base.PreRender(camera, parent);
            var g = Graphics.Current;
            _previous = g.BoundShader!;
            if (Shader == null) throw new InvalidOperationException($"Unloaded shader {BuiltinShaderName}");
            Shader.Bind();
            g.SetFloatUniform(Shader, UniformType.TransformPerspective,
                camera.Get<CameraController>()!.PerspectiveTransform);
            g.SetFloatUniform(Shader, UniformType.TransformView, camera.Get<CameraController>()!.ViewTransform);
            g.SetFloatUniform(Shader, UniformType.ViewPosition, camera.Position);
            LightResolver.Push();
        }

        public override void PostRender(LevelObject camera, LevelObject parent)
        {
            base.PostRender(camera, parent);
            _previous?.Bind();
        }
    }
}