﻿using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Models;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assignment3.Application.States
{
    internal class ViewSalesDataState : AppState
    {
        private readonly UserSession _session;
        private readonly IConsoleView _view;
        private readonly IConsoleInputHandler _inputHandler;
        public ViewSalesDataState(
            UserSession session, IConsoleView view, IConsoleInputHandler inputHandler)
        {
            _session = session;
            _view = view;
            _inputHandler = inputHandler;
        }
        public void ShowDataOptions()
        {
            var options = new Dictionary<char, string>()
            {
                { 'E', "Exit to Main Menu" },
                { 'P', "Print Sales Data" }
            };

            var input = ConsoleHelper.AskUserOption(options);

            switch (input)
            {
                case 'P':
                    PrintSalesData();
                    break;
                case 'E':
                    OnStateChanged(this, nameof(MainMenuState));
                    break;
            }
        }

        private void PrintSalesData()
        {
            //We can change the path to print out later
            var currentDir = Directory.GetCurrentDirectory();
            var filePath = Path.GetFullPath(Path.Combine(currentDir, @"..\..\..\"));

            //TO DO: (HUY) helps me to fix this. I think it's a bad practice but
            //OOP type fucks me up hehe

            using var context = new AppDbContext();
            var receipts = context.Receipts;

            try
            {
                using var writer = new StreamWriter($"{filePath}\\fileTest.csv");
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(receipts);
                }

                ConsoleHelper.PrintInfo("Successfully export CSV file for Sales Data");


            } catch (Exception ex) {
                throw;
            }

        }

        private void ShowReceipts()
        {
            using var context = new AppDbContext();
            var receipts = context.Receipts
                .Include(x => x.Order)
                .AsNoTracking()
                .OrderByDescending(x => x.Order.Date);

            foreach (var receipt in receipts)
            {
                _view.Info(string.Empty);
                _view.Info($"ID [{receipt.Id}]");
                foreach (var product in receipt.Order.Products)
                {
                    _view.Info($"{product.Product.Name}-{product.ProductQuantity}");
                }
                _view.Info($"Customer: {receipt.Order.CustomerEmail}");
                _view.Info($"Time: {receipt.Order.Date.ToString()}");
            }
        }

        public override void Run()
        {
            ShowReceipts();
            ShowDataOptions();
        }
    }
}
