// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuDeviceInfo.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim except one ANDROID PORT line: the Adapter type is qualified (a framework namespace of that name hides it).

#nullable enable

using System;
using System.Collections.Generic;
using CodeBrix.WebGpu.Native;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// What a <see cref="WebGpuRenderView"/>'s device actually is: its adapter, its backend, and the features and limits
/// it was created with, as CodeBrix.WebGpu.Native reports them. Read once when the device is created (on its own
/// device or on a head's device it adopted alike) and never changed afterwards; a new device brings a new instance.
/// </summary>
/// <remarks>
/// A module or library with requirements (a feature, a minimum limit) checks them here in
/// <see cref="WebGpuRenderView.DeviceReady"/> before it initialises. Features cannot be added to a device after it was
/// created: what the view's own device is created with is decided by <see cref="WebGpuRenderView.DeviceOptions"/>, and
/// <see cref="WebGpuRenderView.RequireOwnedDevice"/> makes the view create its own device instead of adopting a head's.
/// </remarks>
public sealed class WebGpuDeviceInfo
{
	/// <summary>Where the engine's native feature names start (values below it are standard WebGPU features).</summary>
	private const uint NativeFeatureBase = 0x00030000;

	private readonly HashSet<uint> _all;
	private readonly HashSet<uint> _adapterAll;

	private WebGpuDeviceInfo(string adapterName, string adapterVendor, string adapterDescription, AdapterType adapterType,
		uint vendorId, uint deviceId, BackendType backend, List<uint> features, Limits limits, List<uint> adapterFeatures,
		Limits adapterLimits)
	{
		AdapterName = adapterName;
		AdapterVendor = adapterVendor;
		AdapterDescription = adapterDescription;
		AdapterType = adapterType;
		VendorId = vendorId;
		DeviceId = deviceId;
		Backend = backend;

		_all = new HashSet<uint>(features);
		(Features, NativeFeatures) = Split(features);
		Limits = limits;
		_adapterAll = new HashSet<uint>(adapterFeatures);
		(AdapterFeatures, AdapterNativeFeatures) = Split(adapterFeatures);
		AdapterLimits = adapterLimits;
	}

	private static (IReadOnlyList<FeatureName> Standard, IReadOnlyList<NativeFeature> Native) Split(List<uint> features)
	{
		var standard = new List<FeatureName>();
		var native = new List<NativeFeature>();
		foreach (var feature in features)
		{
			if (feature >= NativeFeatureBase)
			{
				native.Add((NativeFeature)feature);
			}
			else
			{
				standard.Add((FeatureName)feature);
			}
		}

		standard.Sort();
		native.Sort();
		return (standard.AsReadOnly(), native.AsReadOnly());
	}

	/// <summary>The adapter's device name (e.g. the GPU's marketing name); empty when the engine reports none.</summary>
	public string AdapterName { get; }

	/// <summary>The adapter's vendor as the engine reports it; may be empty.</summary>
	public string AdapterVendor { get; }

	/// <summary>The adapter's driver description; may be empty.</summary>
	public string AdapterDescription { get; }

	/// <summary>The kind of adapter (discrete, integrated, CPU, unknown).</summary>
	public AdapterType AdapterType { get; }

	/// <summary>The adapter's PCI vendor id (0 when unknown).</summary>
	public uint VendorId { get; }

	/// <summary>The adapter's PCI device id (0 when unknown).</summary>
	public uint DeviceId { get; }

	/// <summary>The backend the device runs on.</summary>
	public BackendType Backend { get; }

	/// <summary>The standard WebGPU features the device was created with, in value order.</summary>
	public IReadOnlyList<FeatureName> Features { get; }

	/// <summary>The engine's native features the device was created with, in value order.</summary>
	public IReadOnlyList<NativeFeature> NativeFeatures { get; }

	/// <summary>The device's limits (its <c>NextInChain</c> is always null).</summary>
	public Limits Limits { get; }

	/// <summary>
	/// The standard WebGPU features the ADAPTER offers, in value order: what a device on it COULD be created with. A
	/// feature here but not in <see cref="Features"/> is available but was not enabled when the device was created.
	/// </summary>
	public IReadOnlyList<FeatureName> AdapterFeatures { get; }

	/// <summary>The engine's native features the adapter offers, in value order.</summary>
	public IReadOnlyList<NativeFeature> AdapterNativeFeatures { get; }

	/// <summary>The adapter's limits: the most a device on it could be created with (<c>NextInChain</c> is null).</summary>
	public Limits AdapterLimits { get; }

	/// <summary>Whether the device has a standard feature.</summary>
	/// <param name="feature">The feature.</param>
	/// <returns>True when the device was created with it.</returns>
	public bool HasFeature(FeatureName feature) => _all.Contains((uint)feature);

	/// <summary>Whether the device has a native feature.</summary>
	/// <param name="feature">The feature.</param>
	/// <returns>True when the device was created with it.</returns>
	public bool HasFeature(NativeFeature feature) => _all.Contains((uint)feature);

	/// <summary>Whether the adapter offers a standard feature (enabled on the device or not).</summary>
	/// <param name="feature">The feature.</param>
	/// <returns>True when a device on this adapter could have it.</returns>
	public bool AdapterHasFeature(FeatureName feature) => _adapterAll.Contains((uint)feature);

	/// <summary>Whether the adapter offers a native feature (enabled on the device or not).</summary>
	/// <param name="feature">The feature.</param>
	/// <returns>True when a device on this adapter could have it.</returns>
	public bool AdapterHasFeature(NativeFeature feature) => _adapterAll.Contains((uint)feature);

	/// <summary>A one-line description.</summary>
	/// <returns>For example <c>Intel(R) UHD Graphics (Vulkan, IntegratedGPU), features 0 + 0 native enabled of 12 + 20
	/// native available</c>.</returns>
	public override string ToString() =>
		$"{AdapterName} ({Backend}, {AdapterType}), features {Features.Count} + {NativeFeatures.Count} native enabled " +
		$"of {AdapterFeatures.Count} + {AdapterNativeFeatures.Count} native available";

	/// <summary>
	/// Reads what a device is. Uses only queries the engine implements (AdapterGetInfo - never DeviceGetAdapterInfo,
	/// which ends the process -, AdapterGetFeatures, AdapterGetLimits, DeviceGetFeatures, DeviceGetLimits). Call on the
	/// thread that uses the device.
	/// </summary>
	/// <param name="adapter">The device's adapter (may be null: no adapter information then).</param>
	/// <param name="device">The device.</param>
	/// <param name="backend">The device's backend.</param>
	/// <returns>The description.</returns>
	internal static unsafe WebGpuDeviceInfo Query(CodeBrix.WebGpu.Native.Adapter adapter, Device device, BackendType backend) // ANDROID PORT: qualified - CodeBrix.Platform.UI.Adapter (a namespace of the re-shipped framework) hides the type here.
	{
		string name = string.Empty, vendor = string.Empty, description = string.Empty;
		var type = AdapterType.Unknown;
		uint vendorId = 0, deviceId = 0;
		AdapterInfo info = default;
		if (!adapter.IsNull && Wgpu.AdapterGetInfo(adapter, &info) == Status.Success)
		{
			name = WgpuString.ToManaged(info.Device) ?? string.Empty;
			vendor = WgpuString.ToManaged(info.Vendor) ?? string.Empty;
			description = WgpuString.ToManaged(info.Description) ?? string.Empty;
			type = info.AdapterType;
			vendorId = info.VendorID;
			deviceId = info.DeviceID;
			Wgpu.AdapterInfoFreeMembers(info);
		}

		SupportedFeatures supported = default;
		Wgpu.DeviceGetFeatures(device, &supported);
		var features = Copy(supported);

		Limits limits = default;
		if (Wgpu.DeviceGetLimits(device, &limits) != Status.Success)
		{
			limits = default;
		}

		limits.NextInChain = null;

		var adapterFeatures = new List<uint>();
		Limits adapterLimits = default;
		if (!adapter.IsNull)
		{
			SupportedFeatures offered = default;
			Wgpu.AdapterGetFeatures(adapter, &offered);
			adapterFeatures = Copy(offered);
			if (Wgpu.AdapterGetLimits(adapter, &adapterLimits) != Status.Success)
			{
				adapterLimits = default;
			}

			adapterLimits.NextInChain = null;
		}

		return new WebGpuDeviceInfo(name, vendor, description, type, vendorId, deviceId, backend, features, limits,
			adapterFeatures, adapterLimits);

		static List<uint> Copy(SupportedFeatures list)
		{
			var copy = new List<uint>((int)list.FeatureCount);
			for (nuint i = 0; i < list.FeatureCount; i++)
			{
				copy.Add((uint)list.Features[i]);
			}

			Wgpu.SupportedFeaturesFreeMembers(list);
			return copy;
		}
	}
}
