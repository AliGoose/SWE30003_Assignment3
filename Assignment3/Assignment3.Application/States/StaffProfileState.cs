using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assignment3.Application.States
{
    internal class StaffProfileState : AppState
    {
        private readonly UserSession _session;
        public StaffProfileState(UserSession session)
        {
            _session = session;
        }
        public override void Run()
        {
            if (!_session.IsUserInRole(Roles.Staff))
            {
                ConsoleInputHandler.PrintError("Invalid access to customer page");
                ConsoleInputHandler.PrintInfo("Signing out");
                _session.SignOut();
                OnStateChanged(this, nameof(MainMenuState));
            }

            var input = ConsoleInputHandler.AskUserOption(
            new Dictionary<char, string>()
            {
                { 'V', "View Data Sales"},
                { 'E', "Exit to Main Menu" },
            });

            switch (input)
            {
                case 'V':
                    ViewSalesData();
                    break;
                case 'E':
                    OnStateChanged(this, nameof(MainMenuState));
                    break;
            }
        }

        private void ViewSalesData()
        {
            OnStateChanged(this, nameof(ViewSalesDataState));
        }
    }
}
