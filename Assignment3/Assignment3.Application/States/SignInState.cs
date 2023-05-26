using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Enums;
using Assignment3.Domain.Models;
using Assignment3.Domain.Services;

namespace Assignment3.Application.States;

internal class SignInState : AppState
{
    private readonly UserSession _session;
    public SignInState(
        UserSession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public override void Run()
    {
        var userSignedIn = _session.IsUserSignedIn;
        if (!userSignedIn)
        {
            ShowSignedOutOptions();
        }
        else
        {
            ShowSignedInOptions();
        }

    }

    private void ShowSignedOutOptions()
    {
        var input = ConsoleInputHandler.AskUserOption(
            new Dictionary<char, string>()
            {
                { 'S', "Sign in with an existing account" },
                { 'C', "Create a new customer account" },
                { 'F', "Forgot password" },
                { 'E', "Exit to Main Menu" },
            });

        switch (input)
        {
            case 'S':
                SignIn();
                break;
            case 'C':
                CreateCustomerAccount();
                break;
            case 'F':
                ResetPassword();
                break;
            case 'E':
                OnStateChanged(this, nameof(MainMenuState));
                break;
        }
    }

    private void ResetPassword()
    {
        var email = ConsoleInputHandler.AskUserTextInput("Enter the email of your account");
        while (string.IsNullOrEmpty(email))
        {
            ConsoleInputHandler.PrintError("Email cannot be empty");
            ConsoleInputHandler.AskUserTextInput("Please enter a valid email");
        }

        using var context = new AppDbContext();
        var account = context.UserAccounts.Find(email);
        if (account == null)
        {
            ConsoleInputHandler.PrintError($"No account with the email '{email}' exists");
            return;
        }

        // pretend that the user receives and enters the correct reset code
        _ = ConsoleInputHandler.AskUserTextInput("Enter the reset code sent to your email");
        
        var newPassword = ConsoleInputHandler.AskUserTextInput("Enter your new password");
        account.SetPassword(newPassword);
        
        try
        {
            context.UserAccounts.Update(account);
            context.SaveChanges();
        }
        catch (Exception e) // TODO: catch more specific exception
        {
            ConsoleInputHandler.PrintError("Failed to update the account password");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        _session.SignIn(account);
        ConsoleInputHandler.PrintInfo("Successfully signed in");
    }

    private void ShowSignedInOptions()
    {
        var choices = new Dictionary<char, string>()
        {
            { 'S', "Sign Out"},
            { 'E', "Exit to Main Menu" },
        };

        var (prompt, newStateName) = _session.AuthenticatedUser.Role switch
        {
            // TODO: jump to another state where staff account details can be changed/ created
            Roles.Admin => ("View admin profile", nameof(AdminProfileState)),
            // TODO: jump to another state where refund requests can be viewed and product inventory can be updated
            Roles.Staff => ("View staff profile", nameof(StaffProfileState)),
            Roles.Customer => ("View customer profile", nameof(CustomerProfileState)),
            _ => throw new NotImplementedException(),
        };

        choices.Add('V', prompt);

        var input = ConsoleInputHandler.AskUserOption(choices);

        switch (input)
        {
            case 'S':
                _session.SignOut();
                ConsoleInputHandler.PrintInfo("Signed out successfully");
                break;
            case 'V':
                OnStateChanged(this, newStateName);
                break;               
            case 'E':
                OnStateChanged(this, nameof(MainMenuState));
                break;
        }
    }

    private void CreateCustomerAccount()
    {
        var email = ConsoleInputHandler.AskUserTextInput("Choose your email");
        var phone = ConsoleInputHandler.AskUserTextInput("Choose your phone number");
        var password = ConsoleInputHandler.AskUserTextInput("Choose your password");

        var newUserAccount = new CustomerAccount()
        {
            Email = email,
            Phone = phone,
            Role = Roles.Customer,
        };
        
        newUserAccount.SetPassword(password);
        var validationResults = ModelValidator.ValidateObject(newUserAccount);
        if (validationResults.Count != 0)
        {
            ConsoleInputHandler.PrintErrors(validationResults);
            return;
        }

        using var context = new AppDbContext();
        try
        {
            context.UserAccounts.Add(newUserAccount);
            context.SaveChanges();
        }
        catch (Exception e) // TODO: catch more specific exception
        {
            ConsoleInputHandler.PrintError("Failed to register new customer account. Perhaps an account with this email already exists?");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        _session.SignIn(newUserAccount);
        ConsoleInputHandler.PrintInfo("Successfully signed in");
    }

    private void SignIn()
    {
        var email = ConsoleInputHandler.AskUserTextInput("Enter account email");
        var password = ConsoleInputHandler.AskUserTextInput("Enter password");
        if (string.IsNullOrEmpty(email))
        {
            ConsoleInputHandler.PrintError("Email must not be empty");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ConsoleInputHandler.PrintError("Password must not be empty");
            return;
        }

        using var context = new AppDbContext();
        var userAccount = context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (userAccount == null)
        {
            ConsoleInputHandler.PrintError($"No account with the email '{email}' exists");
            return;
        }

        if (!userAccount.Authenticate(password))
        {
            ConsoleInputHandler.PrintError("Incorrect password");
            return;
        }

        _session.SignIn(userAccount);
        ConsoleInputHandler.PrintInfo("Successfully signed in");
    }
}
