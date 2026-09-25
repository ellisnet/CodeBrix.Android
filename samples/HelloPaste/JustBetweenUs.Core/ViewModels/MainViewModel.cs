using CodeBrix.Platform.Simple;
using JustBetweenUs.Encryption.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace JustBetweenUs.ViewModels;

public interface ICopyToClipboard { Action<string> CopyTextToClipboard { get; set; }}

/// <summary>
/// Lets the hosting page tell the view model that its UI is on screen and can host a dialog. Each
/// head calls <see cref="NotifyPageReady"/> from its page's loaded event, and that is what releases
/// the startup dialog; a head that never calls it simply never shows that dialog.
/// </summary>
public interface IPageReadyNotifier
{
    /// <summary>Tells the view model that the page is loaded and can host a dialog.</summary>
    void NotifyPageReady();
}

#if HAS_CODEBRIX
[Microsoft.UI.Xaml.Data.Bindable]
#endif
public class MainViewModel : SimpleViewModel, ICopyToClipboard, IPageReadyNotifier
{
    private IEncryptionService _encryptSvc;
    private bool _copyMessageShown;
    private SimpleOsInfo _osInfo;

    private readonly TaskCompletionSource _pageReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MainViewModel()
    {
        if (!IsDesignMode(true))
        {
            Debug.WriteLine("Main view model startup.");

            _encryptSvc = GetService<IEncryptionService>();

            EncryptionModes = _encryptionModeDictionary.Values
                .Where(w => w != null)
                .ToArray();
            SelectedEncryptionMode = EncryptionModes.FirstOrDefault();

            Initialization = InitializeAsync();
        }
    }

    /// <summary>
    /// The startup work that the constructor begins: reading the default encryption key and showing
    /// the application's first informational dialog. A page or a test can await this to find out when
    /// that work has finished.
    /// </summary>
    public Task Initialization { get; private set; } = Task.CompletedTask;

    private async Task InitializeAsync()
    {
        try
        {
            var defaultKey = await _encryptSvc.GetDefaultKey();
            //We can't set a value to EncryptionKey except on the main (UI) thread, because this causes problems on Linux and macOS
            InvokeOnMainThread(() => EncryptionKey = defaultKey);

            //A dialog needs a UI anchor that does not exist until the page has been laid out, so wait
            //  for the page to say that it is ready instead of guessing how long that takes.
            await _pageReady.Task;

            await ShowInfo("This application is adapted from a sample provided by Paul Ainsworth.");
        }
        catch (OperationCanceledException)
        {
            //The view model was disposed before the page became ready - there is nothing left to show
        }
        catch (Exception e)
        {
            //Startup work must never be able to bring the application down
            Debug.WriteLine($"Main view model startup failed: {e.Message}");
        }
    }

    #region | Bindable properties |

    #region Select encryption mode

    private readonly Dictionary<EncryptionMode.CryptAlgorithm, EncryptionMode> _encryptionModeDictionary =
        EncryptionMode.GetDictionary();

    /// <summary>The algorithms the picker offers, in enum order; the picker displays their Description.</summary>
    public IReadOnlyList<EncryptionMode> EncryptionModes
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>The algorithm that the picker currently has selected.</summary>
    public EncryptionMode SelectedEncryptionMode
    {
        get;
        set => SetProperty(ref field, value);
    }

    #endregion

    [AffectsCommands(nameof(EncryptCommand), nameof(DecryptCommand))]
    public string EncryptionKey
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    [AffectsCommands(nameof(EncryptCommand), nameof(DecryptCommand))]
    public string EnteredText
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    [AffectsCommands(nameof(CopyToClipboardCommand))]
    public string ProcessedText
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    #endregion

    #region | Commands and their implementations |

    #region EncryptCommand

    private SimpleCommand _encryptCommand;
    public SimpleCommand EncryptCommand => 
        (_encryptCommand ??= new SimpleCommand(CanEncrypt, DoEncrypt));

    private bool CanEncrypt() => 
        (!string.IsNullOrWhiteSpace(EncryptionKey)) && (!string.IsNullOrWhiteSpace(EnteredText));

    private async Task DoEncrypt()
    {
        if (CanEncrypt())
        {
            try
            {
                switch (SelectedEncryptionMode?.Member)
                {
                    case EncryptionMode.CryptAlgorithm.Aes:
                        ProcessedText = await _encryptSvc.AES_EncryptToBase64(EncryptionKey.Trim(), EnteredText);
                        break;

                    case EncryptionMode.CryptAlgorithm.TripleDes:
                        ProcessedText = await _encryptSvc.OriginalTripleDES_EncryptToBase64(EncryptionKey.Trim(), EnteredText);
                        break;
                    
                    case EncryptionMode.CryptAlgorithm.Twofish:
                        ProcessedText = await _encryptSvc.Twofish_EncryptToBase64(EncryptionKey.Trim(), EnteredText);
                        break;
                }
            }
            catch (Exception e)
            {
                await ShowError($"Error while encrypting: {e.Message}");
            }
        }
    }

    #endregion

    #region DecryptCommand

    private SimpleCommand _decryptCommand;
    public SimpleCommand DecryptCommand =>
        (_decryptCommand ??= new SimpleCommand(CanDecrypt, DoDecrypt));

    private bool CanDecrypt() =>
        (!string.IsNullOrWhiteSpace(EncryptionKey)) 
        && (!string.IsNullOrWhiteSpace(EnteredText));
        //&& IsBase64Text(EnteredText); //Too distracting to have the button flash on and off

    private async Task DoDecrypt()
    {
        if (CanDecrypt())
        {
            if (!_encryptSvc.IsBase64Text(EnteredText))
            {
                await ShowInfo("The specified text does not look like it is encrypted.");
            }
            else
            {
                try
                {
                    switch (SelectedEncryptionMode?.Member)
                    {
                        case EncryptionMode.CryptAlgorithm.Aes:
                            ProcessedText = await _encryptSvc.AES_DecryptFromBase64(EncryptionKey.Trim(), EnteredText);
                            break;

                        case EncryptionMode.CryptAlgorithm.TripleDes:
                            ProcessedText = await _encryptSvc.OriginalTripleDES_DecryptFromBase64(EncryptionKey.Trim(), EnteredText);
                            break;

                        case EncryptionMode.CryptAlgorithm.Twofish:
                            ProcessedText = await _encryptSvc.Twofish_DecryptFromBase64(EncryptionKey.Trim(), EnteredText);
                            break;
                    }
                }
                catch (Exception e)
                {
                    await ShowError($"Error while decrypting: {e.Message}");
                }
            }
        }
    }

    #endregion

    #region CopyToClipboardCommand

    private SimpleCommand _copyToClipboardCommand;
    public SimpleCommand CopyToClipboardCommand =>
        (_copyToClipboardCommand ??= new SimpleCommand(CanCopyToClipboard, DoCopyToClipboard));

    private bool CanCopyToClipboard() => (!string.IsNullOrWhiteSpace(ProcessedText));

    private async Task DoCopyToClipboard()
    {
        if (CanCopyToClipboard())
        {
            if (CopyTextToClipboard != null)
            {
                InvokeOnMainThread(() => CopyTextToClipboard(ProcessedText));
                if (!_copyMessageShown)
                {
                    _copyMessageShown = true;
                    await ShowInfo("The processed text has been copied to the system clipboard.");
                }
            }
            else
            {
                await ShowError(
                    "This platform implementation does not have the Copy-to-clipboard functionality enabled.");
            }
        }
    }

    #endregion

    #region ShowOsInfoCommand

    private SimpleCommand _showOsInfoCommand;
    public SimpleCommand ShowOsInfoCommand =>
        (_showOsInfoCommand ??= new SimpleCommand(DoShowOsInfo));

    private async Task DoShowOsInfo()
    {
        _osInfo ??= await SimpleOsInfo.GatherInfo(withConsoleOutput: false);
        var sb = new StringBuilder();
        sb.AppendLine($"Currently running on: {_osInfo.PlatformOsName}");
        sb.AppendLine($"Operating system description: {_osInfo.OsDescription}");
        sb.AppendLine($"Operating system version: {_osInfo.OsVersion}");
        sb.AppendLine($"Product name: {_osInfo.ProductName}");
        sb.AppendLine($"Product name (for display): {_osInfo.ProductNameDisplay}");

        //Note that when running via CodeBrix.Platform on Android, the following lines will not be displayed - since
        //  the SimpleDialog text is truncated on this platform - it must have a maximum number of lines.
        //  Further note: CodeBrix.Platform is not supported on Android
        sb.AppendLine($"Running as user: {_osInfo.RunningAsUser}{((_osInfo.IsAdminUser is true) ? " (local admin)" : "")}");
        sb.AppendLine($"DotNet version: {_osInfo.DotNetVersion}");
        sb.AppendLine($"Platform architecture: {_osInfo.PlatformArchitecture}");

        await ShowInfo(sb.ToString());
    }
    
    #endregion
    
    #endregion

    #region | ICopyToClipboard implementation |

    public Action<string> CopyTextToClipboard { get; set; }

    #endregion

    #region | IPageReadyNotifier implementation |

    /// <summary>
    /// Called by the hosting page once its UI is on screen. The startup dialog waits for this,
    /// because a dialog needs a UI anchor that does not exist while the page is still being built.
    /// </summary>
    public void NotifyPageReady() => _pageReady.TrySetResult();

    #endregion

    #region | IDisposable implementation |

    public override void Dispose()
    {
        //Releases the startup task if it is still waiting for the page to become ready
        _pageReady.TrySetCanceled();
        _encryptSvc = null;
        _encryptCommand?.Dispose();
        _encryptCommand = null;
        _decryptCommand?.Dispose();
        _decryptCommand = null;
        _copyToClipboardCommand?.Dispose();
        _copyToClipboardCommand = null;
        _showOsInfoCommand?.Dispose();
        _showOsInfoCommand = null;
        CopyTextToClipboard = null;
        base.Dispose();
    }

    #endregion
}
