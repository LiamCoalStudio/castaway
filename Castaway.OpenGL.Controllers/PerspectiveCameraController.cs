using Castaway.Base;
using Castaway.Level;
using Castaway.Math;
using Castaway.Rendering;

namespace Castaway.OpenGL.Controllers;

[ControllerName("PerspCamera")]
[Imports(typeof(OpenGLImpl))]
public class PerspectiveCameraController : CameraController
{
	[LevelSerialized("VerticalFOV")] public float FOV;

	public override void OnInit(LevelObject parent)
	{
		base.OnInit(parent);
		var win = Graphics.Current.Window!;
		win.WindowResize += OnWindowResize;
		win.GetSize(out var w, out var h);
		OnWindowResize(w, h);
	}

	public override void OnDestroy(LevelObject parent)
	{
		base.OnDestroy(parent);
		Graphics.Current.Window!.WindowResize -= OnWindowResize;
	}

	private void OnWindowResize(int w, int h)
	{
		var win = Graphics.Current.Window!;
		PerspectiveTransform = CameraMath.Persp(win, FarCutoff, NearCutoff, MathEx.ToRadians(FOV), Size);
	}

	public override void PreRenderFrame(LevelObject camera, LevelObject? parent)
	{
		base.PreRenderFrame(camera, parent);
		ViewTransform = camera.Rotation.Normalize().Conjugate().ToMatrix4()
		                * Matrix4.Translate(-camera.Position);
	}
}