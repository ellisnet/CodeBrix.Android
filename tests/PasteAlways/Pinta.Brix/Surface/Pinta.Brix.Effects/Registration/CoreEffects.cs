//
// CoreEffects.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2011 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// Pinta.Brix note: upstream shipped this registration as the Mono.Addins
// extension CoreEffectsExtension. Mono.Addins is gone from the port, so the
// same adjustments and effects are registered through this plain static entry
// point the application shell calls during startup.

using System;
using Pinta.Brix.Engine;

//was previously: namespace Pinta.Effects;
namespace Pinta.Brix.Effects;

/// <summary>
/// Registers the stock adjustments and effects with the application's
/// <see cref="EffectsManager"/>.
/// </summary>
public static class CoreEffects
{
	/// <summary>
	/// Creates every stock adjustment and effect against the supplied service
	/// provider and registers them with the <see cref="EffectsManager"/>
	/// resolved from the same provider.
	/// </summary>
	public static void Register (IServiceProvider services)
	{
		EffectsManager effects = services.GetService<EffectsManager> ();

		// Add the adjustments
		effects.RegisterAdjustment (new AutoLevelEffect (services));
		effects.RegisterAdjustment (new BlackAndWhiteEffect (services));
		effects.RegisterAdjustment (new BrightnessContrastEffect (services));
		effects.RegisterAdjustment (new CurvesEffect (services));
		effects.RegisterAdjustment (new HueSaturationEffect (services));
		effects.RegisterAdjustment (new InvertColorsEffect (services));
		effects.RegisterAdjustment (new LevelsEffect (services));
		effects.RegisterAdjustment (new PosterizeEffect (services));
		effects.RegisterAdjustment (new SepiaEffect (services));

		// Add the effects
		effects.RegisterEffect (new AddNoiseEffect (services));
		effects.RegisterEffect (new AlignObjectEffect (services));
		effects.RegisterEffect (new BulgeEffect (services));
		effects.RegisterEffect (new CellsEffect (services));
		effects.RegisterEffect (new CloudsEffect (services));
		effects.RegisterEffect (new DentsEffect (services));
		effects.RegisterEffect (new DitheringEffect (services));
		effects.RegisterEffect (new EdgeDetectEffect (services));
		effects.RegisterEffect (new EmbossEffect (services));
		effects.RegisterEffect (new FragmentEffect (services));
		effects.RegisterEffect (new FrostedGlassEffect (services));
		effects.RegisterEffect (new GaussianBlurEffect (services));
		effects.RegisterEffect (new GlowEffect (services));
		effects.RegisterEffect (new FeatherEffect (services));
		effects.RegisterEffect (new OutlineObjectEffect (services));
		effects.RegisterEffect (new InkSketchEffect (services));
		effects.RegisterEffect (new JuliaFractalEffect (services));
		effects.RegisterEffect (new MandelbrotFractalEffect (services));
		effects.RegisterEffect (new MedianEffect (services));
		effects.RegisterEffect (new MotionBlurEffect (services));
		effects.RegisterEffect (new OilPaintingEffect (services));
		effects.RegisterEffect (new OutlineEdgeEffect (services));
		effects.RegisterEffect (new PencilSketchEffect (services));
		effects.RegisterEffect (new PixelateEffect (services));
		effects.RegisterEffect (new PolarInversionEffect (services));
		effects.RegisterEffect (new RadialBlurEffect (services));
		effects.RegisterEffect (new RedEyeRemoveEffect (services));
		effects.RegisterEffect (new ReduceNoiseEffect (services));
		effects.RegisterEffect (new ReliefEffect (services));
		effects.RegisterEffect (new SharpenEffect (services));
		effects.RegisterEffect (new SoftenPortraitEffect (services));
		effects.RegisterEffect (new TileEffect (services));
		effects.RegisterEffect (new TwistEffect (services));
		effects.RegisterEffect (new UnfocusEffect (services));
		effects.RegisterEffect (new VignetteEffect (services));
		effects.RegisterEffect (new VoronoiDiagramEffect (services));
		effects.RegisterEffect (new ZoomBlurEffect (services));
	}
}
