﻿using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Enums;
using Assignment3.Domain.Models;

namespace Assignment3.Application.States
{
    internal class ManageInventoryState : AppState
    {
        private readonly Catalogue _catalogue;
        private readonly UserSession _session;
        private readonly IConsoleView _view;
        private readonly IConsoleInputHandler _inputHandler;

        public ManageInventoryState(
            Catalogue catalogue, 
            UserSession session, 
            IConsoleView view, 
            IConsoleInputHandler inputHandler)
        {
            _catalogue = catalogue;
            _session = session;
            _view = view;
            _inputHandler = inputHandler;
        }
        
        /// <inheritdoc/>
        public override void Run()
        {
            if (!_session.IsUserSignedIn)
            {
                _view.Error("Invalid access to ordering page");
                OnStateChanged(this, nameof(SignInState));
                return;
            }

            if (!_session.IsUserInRole(Roles.Staff))
            {
                _view.Error("Invalid access to ordering page");
                _view.Info("Signing out");
                _session.SignOut();
                OnStateChanged(this, nameof(MainMenuState));
                return;
            }
            
            // TODO: refactor this to make the behavior similar to BrowsingState for consistency
            var products = _catalogue.GetProducts();
            _view.Info($"Displaying {products.Count} available products:");
            foreach (var product in products)
            {
                ShowProduct(product);
            }

            while (!SelectOption())
            {}
        }

        private void ShowProduct(Product product)
        {

            _view.Info(string.Empty);
            _view.Info($"ID [{product.Id}] - Availability: {product.InventoryCount}");
            _view.Info($"{product.Name} - {product.Price} AUD");
            _view.Info($"{product.Description}");
        }

        private bool SelectOption()
        {
            var options = new Dictionary<char, string>()
                {
                    { 'C', "Add a New Product to the catalogue" },
                    { 'U', "Update a Products Price" },
                    { 'Q', "Update a Products Quantity" },
                    { 'D', "Delete a Product from the catalogue" },
                    { 'E', "Exit to Main Menu" }
                };

            var input = _inputHandler.AskUserOption(options);

            switch (input)
            {
                case 'C':
                    CreateProduct();
                    break;
                case 'U':
                    UpdateProductPrice();
                    break;
                case 'Q':
                    UpdateProductQuantity();
                    break;
                case 'D':
                    DeleteProduct();
                    break;
                case 'E':
                    OnStateChanged(this, nameof(MainMenuState));
                    return true;
            }
            return false;

        }

        private void CreateProduct()
        {
            var name = _inputHandler.AskUserTextInput("Enter the name of the product");
            var description = _inputHandler.AskUserTextInput("Enter the description of the product");
            
            decimal price;
            while (!_inputHandler.TryAskUserTextInput(
                   x => decimal.TryParse(x, out _),
                   decimal.Parse,
                   out price,
                   $"Please type the price of the product",
                   "Invalid input. Input must be empty or a valid number"))
            {}
            
            uint inventoryCount;
            while (!_inputHandler.TryAskUserTextInput(
                   x => uint.TryParse(x, out _),
                   x => uint.Parse(x),
                   out inventoryCount,
                   $"Please type the quantity of the product",
                   "Invalid input. Input must be empty or a valid number"))
            {}
            
            using var context = new AppDbContext();
            context.Products.Add(new Product()
            {
                Name = name, 
                Description = description, 
                Price = price, 
                InventoryCount = inventoryCount,
            });
            
            context.SaveChanges();
        }

        private void UpdateProductPrice()
        {
            int id = -1;
            decimal price = 0;
            while (!_inputHandler.TryAskUserTextInput(
                   x => int.TryParse(x, out _),
                   x => int.Parse(x),
                   out id,
                   $"Please type the ID of the product",
                   "Invalid input. Input must be empty or a valid number"))
            { }
            while (!_inputHandler.TryAskUserTextInput(
                   x => decimal.TryParse(x, out _),
                   x => decimal.Parse(x),
                   out price,
                   $"Please type the price of the product",
                   "Invalid input. Input must be empty or a valid number"))
            { }

            using var context = new AppDbContext();
            var product = context.Products.Find(id);
            if (product == null)
            {
                _view.Error("Could not find product with that ID.");
                return;
            }
            
            product.Price = price;
            context.Products.Update(product);
            context.SaveChanges();
            ShowProduct(product);
        }
        
        private void UpdateProductQuantity()
        {
            int id;
            while (!_inputHandler.TryAskUserTextInput(
                   x => int.TryParse(x, out _),
                   int.Parse,
                   out id,
                   $"Please type the ID of the product",
                   "Invalid input. Input must be empty or a valid number"))
            { }
            
            uint inventoryCount;
            while (!_inputHandler.TryAskUserTextInput(
                   x => uint.TryParse(x, out _),
                   convertFunc: uint.Parse,
                   out inventoryCount,
                   $"Please type the quantity of the product",
                   "Invalid input. Input must be empty or a valid number"))
            { }

            using var context = new AppDbContext();
            var product = context.Products.Find(id);
            if (product == null)
            {
                _view.Error("Could not find product with that ID.");
                return;
            }

            product.InventoryCount = inventoryCount;
            context.Products.Update(product);
            context.SaveChanges();
        }
        private void DeleteProduct()
        {
            int id;
            while (!_inputHandler.TryAskUserTextInput(
                       x => int.TryParse(x, out _),
                       x => int.Parse(x),
                       out id,
                       $"Please type the ID of the product",
                       "Invalid input. Input must be empty or a valid number"))
            {
            }
            
            using var context = new AppDbContext();
            var product = context.Products.Find(id);
            if (product == null)
            {
                _view.Error("Could not find product with that ID.");
                return;
            }

            context.Products.Remove(product);
            context.SaveChanges();
        }
    }
}
