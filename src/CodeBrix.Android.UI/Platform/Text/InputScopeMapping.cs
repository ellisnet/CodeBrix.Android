// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI/UI/Xaml/Controls/TextBox/InputScopeHelper.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System.Linq;
using Microsoft.UI.Xaml.Input;
using AInputTypes = global::Android.Text.InputTypes;

namespace CodeBrix.Android.UI.Platform.Text; //was previously: Microsoft.UI.Xaml.Controls;

/// <summary>
/// InputScope -> Android input type (plan 2.14): the keyboard layout a TextBox asks the IME for, plus the
/// suggestion/auto-correct flags of IsSpellCheckEnabled / IsTextPredictionEnabled.
/// </summary>
internal static class InputScopeMapping
{
    /// <summary>The first input scope name of an InputScope (Default when there is none).</summary>
    /// <param name="scope">The input scope.</param>
    /// <returns>The name value.</returns>
    internal static InputScopeNameValue FirstName(InputScope scope) =>
        scope?.Names?.FirstOrDefault()?.NameValue ?? InputScopeNameValue.Default;

    /// <summary>The Android input class and variation of an InputScope.</summary>
    /// <param name="scope">The input scope.</param>
    /// <returns>The input type.</returns>
    internal static AInputTypes Convert(InputScope scope)
    {
        switch (FirstName(scope))
        {
            case InputScopeNameValue.Number:
            case InputScopeNameValue.NumericPin:
                return AInputTypes.ClassNumber;

            case InputScopeNameValue.NumberFullWidth:
                // Android has no input type that accepts numbers and punctuation other than Phone and Text.
                return AInputTypes.ClassPhone;

            case InputScopeNameValue.CurrencyAmount:
                return AInputTypes.ClassNumber | AInputTypes.NumberFlagDecimal;

            case InputScopeNameValue.Url:
                return AInputTypes.ClassText | AInputTypes.TextVariationUri;

            case InputScopeNameValue.TelephoneNumber:
                return AInputTypes.ClassPhone;

            case InputScopeNameValue.EmailNameOrAddress:
            case InputScopeNameValue.EmailSmtpAddress:
                return AInputTypes.ClassText | AInputTypes.TextVariationEmailAddress;

            case InputScopeNameValue.Default:
            case InputScopeNameValue.PersonalFullName:
                return AInputTypes.ClassText | AInputTypes.TextFlagCapSentences;

            default:
                return AInputTypes.ClassText;
        }
    }

    /// <summary>
    /// The full input type of a TextBox: the InputScope's class, multi-line for AcceptsReturn, and the
    /// suggestion flags (no suggestions when both spell checking and prediction are off; auto-correct when
    /// spell checking is on).
    /// </summary>
    /// <param name="scope">The input scope.</param>
    /// <param name="acceptsReturn">AcceptsReturn.</param>
    /// <param name="spellCheck">IsSpellCheckEnabled.</param>
    /// <param name="prediction">IsTextPredictionEnabled.</param>
    /// <returns>The input type.</returns>
    internal static AInputTypes ForTextBox(InputScope scope, bool acceptsReturn, bool spellCheck, bool prediction)
    {
        var type = Convert(scope);
        if ((type & AInputTypes.MaskClass) == AInputTypes.ClassText)
        {
            if (acceptsReturn)
            {
                type |= AInputTypes.TextFlagMultiLine;
            }

            if (!spellCheck && !prediction)
            {
                type |= AInputTypes.TextFlagNoSuggestions;
            }
            else if (spellCheck)
            {
                type |= AInputTypes.TextFlagAutoCorrect;
            }
        }

        return type;
    }

    /// <summary>The input type of a PasswordBox (text, password variation, no suggestions).</summary>
    /// <param name="scope">The input scope (NumericPin gives a numeric password).</param>
    /// <returns>The input type.</returns>
    internal static AInputTypes ForPassword(InputScope scope) =>
        FirstName(scope) == InputScopeNameValue.NumericPin
            ? AInputTypes.ClassNumber | AInputTypes.NumberVariationPassword
            : AInputTypes.ClassText | AInputTypes.TextVariationPassword | AInputTypes.TextFlagNoSuggestions;
}
