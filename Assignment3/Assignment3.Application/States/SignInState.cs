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
    private readonly IConsoleView _view;
    private readonly IConsoleInputHandler _inputHandler;
    public SignInState(
        UserSession session, 
        IConsoleView view, 
        IConsoleInputHandler inputHandler)
    {
        _session = session;
        _view = view;
        _inputHandler = inputHandler;
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
        var input = _inputHandler.AskUserOption(
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
        var email = _inputHandler.AskUserTextInput("Enter the email of your account");
        while (string.IsNullOrEmpty(email))
        {
            _view.Error("Email cannot be empty");
            _inputHandler.AskUserTextInput("Please enter a valid email");
        }

        using var context = new AppDbContext();
        var account = context.UserAccounts.Find(email);
        if (account == null)
        {
            _view.Error($"No account with the email '{email}' exists");
            return;
        }

        // pretend that the user receives and enters the correct reset code
        _ = _inputHandler.AskUserTextInput("Enter the reset code sent to your email");
        
        var newPassword = _inputHandler.AskUserTextInput("Enter your new password");
        account.SetPassword(newPassword);
        
        try
        {
            context.UserAccounts.Update(account);
            context.SaveChanges();
        }
        catch (Exception e) // TODO: catch more specific exception
        {
            _view.Error("Failed to update the account password");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        _session.SignIn(account);
        _view.Info("Successfully signed in");
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

        var input = _inputHandler.AskUserOption(choices);

        switch (input)
        {
            case 'S':
                _session.SignOut();
                _view.Info("Signed out successfully");
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
        var email = _inputHandler.AskUserTextInput("Choose your email");
        var phone = _inputHandler.AskUserTextInput("Choose your phone number");
        var password = _inputHandler.AskUserTextInput("Choose your password");

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
            _view.Errors(validationResults);
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
            _view.Error("Failed to register new customer account. Perhaps an account with this email already exists?");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        _session.SignIn(newUserAccount);
        _view.Info("Successfully signed in");
    }

    private void SignIn()
    {
        var email = _inputHandler.AskUserTextInput("Enter account email");
        var password = _inputHandler.AskUserTextInput("Enter password");
        if (string.IsNullOrEmpty(email))
        {
            _view.Error("Email must not be empty");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            _view.Error("Password must not be empty");
            return;
        }

        using var context = new AppDbContext();
        var userAccount = context.UserAccounts.FirstOrDefault(x => x.Email == email);
        if (userAccount == null)
        {
            _view.Error($"No account with the email '{email}' exists");
            return;
        }

        if (!userAccount.Authenticate(password))
        {
            _view.Error("Incorrect password");
            return;
        }

        _session.SignIn(userAccount);
        _view.Info("Successfully signed in");
    }
}
