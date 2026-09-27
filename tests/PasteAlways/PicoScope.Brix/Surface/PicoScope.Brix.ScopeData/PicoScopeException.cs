using System;

namespace PicoScope.Brix.ScopeData;

/// <summary>
/// Thrown when an operation against a scope fails.
/// </summary>
/// <remarks>
/// <para>
/// The ps2000 driver has no rich status code. Almost every entry point returns a
/// bare 16-bit integer where zero means failure and anything else means success,
/// and there is no accompanying message. That is a real step down from the newer
/// Pico drivers, which return a <c>PICO_STATUS</c> naming the problem.
/// </para>
/// <para>
/// What the driver does offer is line 6 of its unit-info block, which carries a
/// device error code. An implementation should read it when a call fails and put
/// it in <see cref="DeviceErrorCode"/>, because it is the only diagnostic the
/// API provides.
/// </para>
/// </remarks>
public class PicoScopeException : Exception
{
    /// <summary>
    /// Creates an exception with a message.
    /// </summary>
    /// <param name="message">A description of what failed.</param>
    public PicoScopeException(string message) : base(message) { }

    /// <summary>
    /// Creates an exception with a message and an underlying cause.
    /// </summary>
    /// <param name="message">A description of what failed.</param>
    /// <param name="innerException">The underlying cause.</param>
    public PicoScopeException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Creates an exception describing a failed driver call.
    /// </summary>
    /// <param name="message">A description of what failed.</param>
    /// <param name="driverFunction">The name of the driver entry point that failed.</param>
    /// <param name="deviceErrorCode">The device error code read from unit-info line 6, if any.</param>
    public PicoScopeException(string message, string driverFunction, string deviceErrorCode)
        : base(BuildMessage(message, driverFunction, deviceErrorCode))
    {
        DriverFunction = driverFunction;
        DeviceErrorCode = deviceErrorCode;
    }

    /// <summary>
    /// The driver entry point that failed, when the failure came from one.
    /// </summary>
    public string DriverFunction { get; }

    /// <summary>
    /// The device error code reported on unit-info line 6 at the time of the
    /// failure, when one was available.
    /// </summary>
    public string DeviceErrorCode { get; }

    private static string BuildMessage(string message, string driverFunction, string deviceErrorCode)
    {
        string text = message;
        if (!string.IsNullOrEmpty(driverFunction))
        {
            text += $" (driver call: {driverFunction})";
        }
        if (!string.IsNullOrEmpty(deviceErrorCode) && deviceErrorCode != "0")
        {
            text += $" (device error code: {deviceErrorCode})";
        }
        return text;
    }
}

/// <summary>
/// Thrown when an operation is attempted on a scope that is not open.
/// </summary>
public sealed class ScopeNotOpenException : PicoScopeException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="operation">The operation that was attempted.</param>
    public ScopeNotOpenException(string operation)
        : base($"Cannot {operation}: the scope is not open. Call OpenScope() first.") { }
}

/// <summary>
/// Thrown when a requested setting is not supported by the attached device.
/// </summary>
public sealed class ScopeCapabilityException : PicoScopeException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">A description of the unsupported request.</param>
    public ScopeCapabilityException(string message) : base(message) { }
}
