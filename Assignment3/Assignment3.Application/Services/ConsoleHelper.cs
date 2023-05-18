namespace Assignment3.Application.Services;

internal static class ConsoleHelper
{
    /// <summary>
    /// Ask the user to select an option from the provided list.
    /// </summary>
    /// <param name="choices">List of choices and their description.</param>
    /// <param name="prompt">Optional prompt.</param>
    /// <returns>The selected choice which is guaranteed to belong in the provided <paramref name="choices"/></returns>
    public static char AskUserOption(
        IReadOnlyDictionary<char, string> choices,
        string prompt = "Please select an option:")
    {
        PrintPrompt(prompt);
        foreach (var (choice, description) in choices)
        {
            Console.WriteLine($"[{char.ToUpper(choice)}] - {description}");
        }

        var input = Console.ReadLine();
        while (
            string.IsNullOrEmpty(input) ||
            input.Length != 1 ||
            !choices.ContainsKey(char.ToUpper(input.First())))
        {
            PrintPrompt("Please select a valid option");
            input = Console.ReadLine();
        }

        return char.ToUpper(input.First());
    }
    
    /// <summary>
    /// Ask user for any text input.
    /// </summary>
    /// <param name="prompt">Optional prompt.</param>
    /// <returns>The entered text or <c>string.Empty</c>.</returns>
    public static string AskUserTextInput(string prompt = "Please type your input:")
    {
        PrintPrompt(prompt);
        return Console.ReadLine() ?? string.Empty;
    }
    
    public static void PrintPrompt(string prompt)
    {
        Console.WriteLine($"> {prompt}");
    }

    public static void PrintError(string prompt)
    {
        Console.WriteLine($"! {prompt}");
    }
}
