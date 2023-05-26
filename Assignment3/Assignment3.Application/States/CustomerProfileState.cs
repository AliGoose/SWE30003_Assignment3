using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Enums;
using Assignment3.Domain.Services;

namespace Assignment3.Application.States;

internal class CustomerProfileState : AppState
{
    private readonly UserSession _session;
    public CustomerProfileState(UserSession session)
    {
        _session = session;
    }

    /// <inheritdoc /> 
    public override void Run()
    {
        if (!_session.IsUserInRole(Roles.Customer))
        {
            ConsoleInputHandler.PrintError("Invalid access to customer page");
            ConsoleInputHandler.PrintInfo("Signing out");
            _session.SignOut();
            OnStateChanged(this, nameof(MainMenuState));
        }

        var choices = new Dictionary<char, string>()
        {
            { 'V', "View orders" },
            { 'C', "Change account details" },
            { 'E', "Exit to Main Menu" },
            { 'R', "Request refund" },
        };

        ConsoleInputHandler.PrintInfo("Customer Profile");
        ConsoleInputHandler.PrintInfo($"Email: {_session.AuthenticatedUser.Email}");
        ConsoleInputHandler.PrintInfo($"Phone: {_session.AuthenticatedUser.Phone}");
        ConsoleInputHandler.PrintInfo($"Registration Date: {_session.AuthenticatedUser.RegistryDate.ToLocalTime()}");
        var input = ConsoleInputHandler.AskUserOption(choices);

        switch (input)
        {
            case 'E':
                OnStateChanged(this, nameof(MainMenuState));
                break;
            case 'V':
                ViewOrders();
                break;
            case 'C':
                ChangeAccountDetails();
                break;
            case 'R':
                RequestRefund();
                break;
        }        
    }

    private void ChangeAccountDetails()
    {
        var newPhoneNumber = ConsoleInputHandler.AskUserTextInput("Enter your new phone number or press enter if you do not want to change your phone number");
        var newPassword = ConsoleInputHandler.AskUserTextInput("Enter your new password or press enter if you do not want to change your password");

        // TODO(HUY): VALIDATE INPUT
        if (string.IsNullOrEmpty(newPhoneNumber) && string.IsNullOrEmpty(newPassword))
        {
            ConsoleInputHandler.PrintInfo("No details changed");
            return;
        }

        using var context = new AppDbContext();
        var userAccount = context.UserAccounts.Find(_session.AuthenticatedUser.Email);
        if (userAccount == null)
        {
            ConsoleInputHandler.PrintError("Unable to find customer account");
            ConsoleInputHandler.PrintInfo("Signing out");
            _session.SignOut();
            OnStateChanged(this, nameof(MainMenuState));
            return;
        }

        if (!string.IsNullOrEmpty(newPhoneNumber))
        {
            userAccount.Phone = newPhoneNumber;
        } 
        
        if (!string.IsNullOrEmpty(newPassword))
        {
            userAccount.SetPassword(newPassword);
        } 

        var validationResults = ModelValidator.ValidateObject(userAccount);
        if (validationResults.Count != 0)
        {
            ConsoleInputHandler.PrintErrors(validationResults);
            return;
        }

        try
        {
            context.UserAccounts.Update(userAccount);
            context.SaveChanges();
        }
        catch (Exception e) // TODO: catch more specific exception
        {
            ConsoleInputHandler.PrintError("Failed to change customer details.");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        ConsoleInputHandler.PrintInfo("Successfully changed customer details");
    }

    private void RequestRefund()
    {
        throw new NotImplementedException();
    }

    private void ViewOrders()
    {
        throw new NotImplementedException();
    }
}