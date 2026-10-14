using System;

namespace CodeBrix.Android.UI.Input;

/// <summary>What is known about a gamepad's device: its name, identity and the axes it reports.</summary>
public sealed class GamepadDeviceInfo
{
    /// <summary>Creates a description.</summary>
    /// <param name="name">The device's name as Android reports it.</param>
    /// <param name="origin">Where the gamepad comes from.</param>
    /// <param name="axisProfile">The axes it reports, or null for <see cref="GamepadAxisProfile.Empty"/>.</param>
    /// <param name="vendorId">The USB / Bluetooth vendor id, 0 when unknown.</param>
    /// <param name="productId">The USB / Bluetooth product id, 0 when unknown.</param>
    /// <param name="descriptor">Android's stable descriptor of the device, or null.</param>
    public GamepadDeviceInfo(string name, GamepadOrigin origin, GamepadAxisProfile axisProfile = null, int vendorId = 0, int productId = 0, string descriptor = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Gamepad" : name;
        Origin = origin;
        AxisProfile = axisProfile ?? GamepadAxisProfile.Empty;
        VendorId = vendorId;
        ProductId = productId;
        Descriptor = descriptor ?? string.Empty;
    }

    /// <summary>The device's name as Android reports it.</summary>
    public string Name { get; }

    /// <summary>Where the gamepad comes from.</summary>
    public GamepadOrigin Origin { get; }

    /// <summary>The axes the device reports and how they are read.</summary>
    public GamepadAxisProfile AxisProfile { get; }

    /// <summary>The USB / Bluetooth vendor id, 0 when unknown.</summary>
    public int VendorId { get; }

    /// <summary>The USB / Bluetooth product id, 0 when unknown.</summary>
    public int ProductId { get; }

    /// <summary>Android's stable descriptor of the device (empty when unknown): the same across reconnections.</summary>
    public string Descriptor { get; }

    /// <summary>A one-line description: the name, the ids and the axis layout.</summary>
    public string Describe() =>
        VendorId == 0 && ProductId == 0
            ? $"{Name} [{Origin}]: {AxisProfile.Describe()}"
            : $"{Name} [{Origin}, vendor 0x{VendorId:X4} product 0x{ProductId:X4}]: {AxisProfile.Describe()}";

    /// <inheritdoc />
    public override string ToString() => Describe();
}
