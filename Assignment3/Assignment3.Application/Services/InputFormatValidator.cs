using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Assignment3.Application.Models;
using Assignment3.Domain.Models;

namespace Assignment3.Application.Services;

public static class InputFormatValidator
{
    /// <summary>
    /// Validate that the input is a comma-separated number list.
    /// </summary>
    /// <param name="input">Input string.</param>
    /// <returns><c>True</c> if the input is a comma-separated number list, otherwise <c>False</c>.</returns>
    public static bool ValidateCommaSeparatedNumberList(string input)
    {
        return string.IsNullOrEmpty(input) ||
               Regex.IsMatch(input, RegexPatterns.CommaSeparatedDigits) &&
               input
                   .Split(",")
                   .Select(x => int.TryParse(x, out var r))
                   .All(x => x);
    }

    /// <summary>
    /// Validate that the input is a hypen-separated pair of number.
    /// </summary>
    /// <param name="input">Input string.</param>
    /// <returns><c>True</c> if the input is a hypen-separated pair of number, otherwise <c>False</c>.</returns>
    public static bool ValidateHyphenSeparatedNumberPair(string input)
    {
        return Regex.IsMatch(input, RegexPatterns.HyphenSeparatedDigits) &&
               input
                   .Split("-")
                   .Select(x => int.TryParse(x, out var r))
                   .All(x => x);
    }

    /// <summary>
    /// Validate that the credit card number.
    /// </summary>
    /// <param name="cardNumber">Input string.</param>
    /// <returns></returns>
    public static bool ValidateCardNumber(string cardNumber)
    {
        // Check if the card number matches a valid credit card pattern
        return Regex.IsMatch(cardNumber, RegexPatterns.CardNo);

        // Apply the Luhn algorithm to validate the card number
        // However, in the context of this project, this is unnecessary.
        // var sum = 0;
        // var length = cardNumber.Length;
        // for (var i = 0; i < length; i++)
        // {
        //     var digit = int.Parse(cardNumber[i].ToString());
        //
        //     if (i % 2 == length % 2)
        //     {
        //         digit *= 2;
        //         if (digit > 9)
        //         {
        //             digit -= 9;
        //         }
        //     }
        //
        //     sum += digit;
        // }
        //
        // return sum % 10 == 0;
    }

    /// <summary>
    /// Validate that the input is a valid card expiry date
    /// </summary>
    /// <param name="expiryDate">Input string.</param>
    /// <returns></returns>
    public static bool ValidateCardExpiryDate(string expiryDate)
    {
        const string format = "MM/yyyy";

        // Check if the expiry date follows the format
        if (!DateTime.TryParseExact(
                expiryDate,
                format,
                null,
                System.Globalization.DateTimeStyles.None,
                out var parsedDate))
        {
            return false;
        }
        
        // Check if the parsed date is in the future / card not expired
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;
        var expiryMonth = parsedDate.Month;
        var expiryYear = parsedDate.Year;
        return expiryYear > currentYear || (expiryYear == currentYear && expiryMonth > currentMonth);
    }

    /// <summary>
    /// Validate that the input is a valid Bank BSB.
    /// </summary>
    /// <param name="bsb">Input string</param>
    /// <returns></returns>
    public static bool ValidateBsb(string bsb)
    {
        // Check if the BSB has only digits
        if (!Regex.IsMatch(bsb, RegexPatterns.DigitsOnly))
        {
            return false;
        }

        // Convert to array
        bsb = new string(bsb.ToArray());

        // Check if the BSB has six digits
        if (bsb.Length != 6)
        {
            return false;
        }
        
        /*  The Australia BSB has 6 digits with this format
         *  First 2 numbers: bank code
         *  3rd number: state code
         *  3 last number: branch code
         **/
        
        // Extract bank, state, and branch codes
        var bankCode = bsb.Substring(0, 2);
        var stateCode = bsb.Substring(2, 1);
        var branchCode = bsb.Substring(3, 3);

        // Validate bank code (between 01 and 99)
        if (!int.TryParse(bankCode, out var bank) || bank < 1 || bank > 99)
        {
            return false;
        }

        // Validate state code (between 0 and 9)
        if (!int.TryParse(stateCode, out var state) || state < 0 || state > 9)
        {
            return false;
        }

        // Validate branch code (between 001 and 999)
        if (!int.TryParse(branchCode, out var branch) || branch < 1 || branch > 999)
        {
            return false;
        }

        return true;
    }
}
