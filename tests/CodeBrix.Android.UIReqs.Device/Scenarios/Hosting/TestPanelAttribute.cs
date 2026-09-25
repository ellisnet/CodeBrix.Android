using System;
using CodeBrix.Android.UIReqs.Device.TestTarget;

namespace CodeBrix.Platform.UI.Core.UIReqs.Hosting;

/// <summary>
/// Declares which panel the assembly it is applied to runs its scenarios against.
/// One process is one orientation, so the two entry projects of a UIReqs source tree
/// differ in nothing but the file that carries this attribute. There is no environment
/// variable and no runtime rotation: <see cref="TestTargetFixture"/> reads this
/// attribute off the test assembly once, before the first scenario.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class TestPanelAttribute : Attribute
{
	/// <summary>Declares the panel orientation for the assembly.</summary>
	/// <param name="orientation">Landscape (1920 x 1080) or Portrait (1080 x 1920).</param>
	public TestPanelAttribute(TestDisplayOrientation orientation) => Orientation = orientation;

	/// <summary>The orientation the assembly's scenarios run against.</summary>
	public TestDisplayOrientation Orientation { get; }
}
