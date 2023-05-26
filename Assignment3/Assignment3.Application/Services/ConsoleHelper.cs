using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Assignment3.Application.Services;

// TODO(HUY): separate into IConsoleView and IConsoleInputHandler
[Obsolete]
internal static class ConsoleInputHandler
{
    /// <summary>
    /// Ask the user to select an option from the provided list.
    /// </summary>
    /// <param name="choices">List of choices and their descriptions.</param>
    /// <param name="prompt">Optional prompt.</param>
    /// <returns>The selected choice which is guaranteed to belong in the provided <paramref name="choices"/></returns>
    [Obsolete("See IConsoleInputHandler.AskUserOption")]
    public static char AskUserOption(
        IReadOnlyDictionary<char, string> choices,
        string prompt = "Please select an option:")
    {
        PrintInfo(prompt);
        foreach (var (choice, description) in choices)
        {
            Console.WriteLine($"[{char.ToUpper(choice, CultureInfo.InvariantCulture)}] - {description}");
        }

        var input = Console.ReadLine();
        while (
            string.IsNullOrEmpty(input) ||
            input.Length != 1 ||
            !choices.ContainsKey(char.ToUpper(input.First(), CultureInfo.InvariantCulture)))
        {
            PrintError("Please select a valid option");
            input = Console.ReadLine();
        }

        return char.ToUpper(input.First(), CultureInfo.InvariantCulture);
    }
    
    /// <summary>
    /// Ask user for any text input.
    /// </summary>
    /// <param name="prompt">Optional prompt.</param>
    /// <returns>The raw input text or <c>string.Empty</c>.</returns>
    [Obsolete("See IConsoleInputHandler.AskUserTextInput")]
    public static string AskUserTextInput(string prompt = "Please type your input:")
    {
        PrintInfo(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    /// <summary>
    /// Ask user for a single key input.
    /// </summary>
    /// <param name="prompt">Optional prompt.</param>
    /// <returns>The pressed key.</returns>
    /// <remarks>
    /// This method is used to ask user to enter a single key and is intended to deal with special keys such as Escape or Enter.
    /// To ask user to choose from a list of choices, see <see cref="ConsoleInputHandler.AskUserOption(IReadOnlyDictionary{char, string}, string)"/>.
    /// </remarks>
    [Obsolete("See IConsoleInputHandler.AskUserKeyInput")]
    public static ConsoleKey AskUserKeyInput(string prompt = "Please enter your key:")
    {
        PrintInfo(prompt);
        var result = Console.ReadKey(false).Key;
        Console.WriteLine();
        return result;
    }

    /// <summary>
    /// Ask user for text input and try converting it to a specified data type.
    /// </summary>
    /// <typeparam name="T">The output data type.</typeparam>
    /// <param name="validateFunc">The function that validates the input string.</param>
    /// <param name="convertFunc">The function that validates the output string to the type <typeparamref name="T"/>.</param>
    /// <param name="result">The converted result.</param>
    /// <param name="prompt">The optional prompt.</param>
    /// <param name="validationErrorMessage">The optional validation error message.</param>
    /// <param name="conversionErrorMessage">The optional conversion error message.</param>
    /// <returns><c>True</c> if the input is valid and successfully converted. Otherwise <c>False</c>.</returns>
    [Obsolete("See IConsoleInputHandler.TryAskUserTextInput")]
    public static bool TryAskUserTextInput<T>(
        Func<string, bool> validateFunc,
        Func<string, T> convertFunc,
        [MaybeNullWhen(false)]
        out T result,
        string prompt = "Please type your input:",
        string validationErrorMessage = "Invalid input",
        string conversionErrorMessage = "Failed to process input")
    {
        var inputStr = AskUserTextInput(prompt);
        try
        {
            if (!validateFunc(inputStr))
            {
                ConsoleInputHandler.PrintError(validationErrorMessage);
                result = default;
                return false;
            }
        }
        catch
        {
            ConsoleInputHandler.PrintError(validationErrorMessage);
            result = default;
            return false;
        }

        try
        {
            result = convertFunc(inputStr);
            return true;
        }
        catch
        {
            ConsoleInputHandler.PrintError(conversionErrorMessage);
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Print an info message to the console.
    /// </summary>
    /// <param name="prompt">Prompt.</param>
    [Obsolete("See IConsoleView.Info")]
    public static void PrintInfo(string prompt)
    {
        Console.WriteLine($">>> {prompt}");
    }

    /// <summary>
    /// Print an error message to the console.
    /// </summary>
    /// <param name="error">Error message.</param>
    [Obsolete("See IConsoleView.Error")]
    public static void PrintError(string error)
    {
        Console.WriteLine($"!!! {error}");
    }

    /// <summary>
    /// Print a list of errors to the console.
    /// </summary>
    /// <param name="errors">Error list.</param>
    [Obsolete("See IConsoleView.Errors")]
    public static void PrintErrors(IEnumerable<string> errors)
    {
        ConsoleInputHandler.PrintError("Error(s):");
        foreach (var error in errors)
        {
            ConsoleInputHandler.PrintError(error);
        }
    }
}
