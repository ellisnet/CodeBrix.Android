namespace PicoScope.Brix.ScopeData.Model;

/// <summary>
/// Which line of device information to read, matching the driver's
/// <c>PS2000_INFO</c> enumeration.
/// </summary>
public enum UnitInfoLine
{
    /// <summary>The version of the ps2000 driver library.</summary>
    DriverVersion = 0,

    /// <summary>The USB version the device negotiated, for example "2.0".</summary>
    UsbVersion = 1,

    /// <summary>The device's hardware revision.</summary>
    HardwareVersion = 2,

    /// <summary>The model, for example "2204A". This is how the model is identified.</summary>
    VariantInfo = 3,

    /// <summary>The device's batch and serial number.</summary>
    BatchAndSerial = 4,

    /// <summary>The date the device was last calibrated.</summary>
    CalibrationDate = 5,

    /// <summary>The device's last error code, as a string.</summary>
    ErrorCode = 6,

    /// <summary>The version of the kernel-mode driver.</summary>
    KernelDriverVersion = 7,

    /// <summary>The full path of the driver library that is loaded.</summary>
    DriverPath = 8
}

/// <summary>
/// Everything the device reports about itself.
/// </summary>
/// <param name="Variant">The model name, for example "2204A".</param>
/// <param name="BatchAndSerial">The batch and serial number.</param>
/// <param name="CalibrationDate">The date of last calibration.</param>
/// <param name="DriverVersion">The ps2000 driver version.</param>
/// <param name="HardwareVersion">The hardware revision.</param>
/// <param name="UsbVersion">The negotiated USB version.</param>
/// <param name="KernelDriverVersion">The kernel driver version.</param>
/// <param name="DriverPath">The full path of the loaded driver library.</param>
/// <param name="ErrorCode">The device's last reported error code.</param>
public readonly record struct UnitInfo(
    string Variant,
    string BatchAndSerial,
    string CalibrationDate,
    string DriverVersion,
    string HardwareVersion,
    string UsbVersion,
    string KernelDriverVersion,
    string DriverPath,
    string ErrorCode)
{
    /// <summary>
    /// A one-line summary suitable for a status bar.
    /// </summary>
    /// <returns>A display string such as "PicoScope 2204A (serial 10066/1927)".</returns>
    public override string ToString()
        => $"PicoScope {Variant} (serial {BatchAndSerial}, calibrated {CalibrationDate})";
}
