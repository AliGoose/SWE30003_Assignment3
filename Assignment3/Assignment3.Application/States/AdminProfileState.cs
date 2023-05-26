using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Enums;
using Assignment3.Domain.Models;
using Assignment3.Domain.Services;

namespace Assignment3.Application.States;

internal class AdminProfileState : AppState
{
    private readonly UserSession _session;
    public AdminProfileState(UserSession session)
    {
        _session = session;
    }
    
    /// <inheritdoc/>
    public override void Run()
    {
        if (!_session.IsUserInRole(Roles.Admin))
        {
            ConsoleInputHandler.PrintError("Invalid access to admin page");
            ConsoleInputHandler.PrintInfo("Signing out");
            _session.SignOut();
            OnStateChanged(this, nameof(MainMenuState));
        }

        var input = ConsoleInputHandler.AskUserOption(
            new Dictionary<char, string>()
            {
                { 'V', "View all staff accounts" },
                { 'A', "Alter a staff account" },
                { 'C', "Create a new staff account" },
                { 'E', "Exit to Main Menu" },
            });

        switch (input)
        {
            case 'V':
                ViewStaffAccounts();
                break;
            case 'A':
                ChangeStaffAccountDetails();
                break;
            case 'C':
                CreateStaffAccount();
                break;
            case 'S':
                ShowSalesData(); 
                break;
            case 'E':
                OnStateChanged(this, nameof(MainMenuState));
                break;
        }
    }

    private void CreateStaffAccount()
    {
        var email = ConsoleInputHandler.AskUserTextInput("Enter the staff member's email");
        var phone = ConsoleInputHandler.AskUserTextInput("Enter the staff member's phone number");
        var password = ConsoleInputHandler.AskUserTextInput("Enter the account password");

        var newStaffAccount = new StaffAccount()
        {
            Email = email,
            Phone = phone,
            Role = Roles.Staff,
        };
        
        newStaffAccount.SetPassword(password);
        var validationResults = ModelValidator.ValidateObject(newStaffAccount);
        if (validationResults.Count != 0)
        {
            ConsoleInputHandler.PrintErrors(validationResults);
            return;
        }

        using var context = new AppDbContext();
        try
        {
            context.UserAccounts.Add(newStaffAccount);
            context.SaveChanges();
        }
        catch (Exception e) // TODO: catch more specific exception
        {
            ConsoleInputHandler.PrintError("Failed to register new staff account. Perhaps an account with this email already exists?");
#if DEBUG
            Console.WriteLine(e.Message);
#endif
            return;
        }

        ConsoleInputHandler.PrintInfo("Successfully created staff account");
    }

    private void ChangeStaffAccountDetails()
    {
        var email = ConsoleInputHandler.AskUserTextInput("Enter the staff member's email");
        var newPhoneNumber = ConsoleInputHandler.AskUserTextInput("Enter the staff member's new phone number. Press [Enter] if you do not want to change their phone number");
        var newPassword = ConsoleInputHandler.AskUserTextInput("Enter the staff member's new password. Press [Enter] if you do not want to change their password");

        if (string.IsNullOrEmpty(email)) 
        {
            ConsoleInputHandler.PrintError("Email cannot be empty");
            return;
        }

        if (string.IsNullOrEmpty(newPhoneNumber) && string.IsNullOrEmpty(newPassword))
        {
            ConsoleInputHandler.PrintInfo("No details changed");
            return;
        }

        using var context = new AppDbContext();
        var staffAccount = context.UserAccounts.FirstOrDefault(x => x.Email == email && x.Role == Roles.Staff);
        if (staffAccount == null)
        {
            ConsoleInputHandler.PrintError($"Unable to find staff account with email '{email}'");
            return;
        }

        if (!string.IsNullOrEmpty(newPhoneNumber))
        {
            staffAccount.Phone = newPhoneNumber;
        } 
        
        if (!string.IsNullOrEmpty(newPassword))
        {
            staffAccount.SetPassword(newPassword);
        } 

        var validationResults = ModelValidator.ValidateObject(staffAccount);
        if (validationResults.Count != 0)
        {
            ConsoleInputHandler.PrintErrors(validationResults);
            return;
        }

        try
        {
            context.UserAccounts.Update(staffAccount);
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

        ConsoleInputHandler.PrintInfo("Successfully changed staff member details");
    }

    private void ViewStaffAccounts()
    {
        using var context = new AppDbContext();
        var staffAccounts = context
            .UserAccounts
            .Where(x => x.Role == Roles.Staff)
            .Select(x => new { x.Email, x.RegistryDate, x.Phone })
            .ToList();

        ConsoleInputHandler.PrintInfo($"Displaying {staffAccounts.Count} staff member(s)");
        foreach (var staff in staffAccounts)
        {
            ConsoleInputHandler.PrintInfo($"Email: {staff.Email} - Phone: {staff.Phone} - Registration Date: {staff.RegistryDate.ToLocalTime()}");
        }
    }

    private void ShowSalesData()
    {
        OnStateChanged(this, nameof(ViewSalesDataState));
    }
}