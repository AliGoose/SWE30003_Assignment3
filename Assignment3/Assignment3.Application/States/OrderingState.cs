using System.Text.RegularExpressions;
using Assignment3.Application.Models;
using Assignment3.Application.Services;
using Assignment3.Domain.Data;
using Assignment3.Domain.Enums;
using Assignment3.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment3.Application.States;

internal class OrderingState : AppState
{
    private readonly UserSession _session;
    private readonly IConsoleView _view;
    private readonly IConsoleInputHandler _inputHandler;
    public OrderingState(UserSession session, IConsoleView view, IConsoleInputHandler inputHandler)
    {
        _session = session;
        _view = view;
        _inputHandler = inputHandler;
    }

    /// <inheritdoc />
    public override void Run()
    {
        if (!_session.IsUserSignedIn)
        {
            _view.Error("Invalid access to ordering page");
            OnStateChanged(this, nameof(SignInState));
            return;
        }

        if (!_session.IsUserInRole(Roles.Customer))
        {
            _view.Error("Invalid access to ordering page");
            _view.Info("Signing out");
            _session.SignOut();
            OnStateChanged(this, nameof(MainMenuState));
            return;
        }
        
        var choices = new Dictionary<char, string>()
        {
            // TODO(HUY): implement a back function using a stack?m  
            { 'B', "Back to Browsing" },
        };

        var order = GetExistingOrderOrDefault();
        if (order != null)
        {
            choices.Add('E', "Edit Order");
            choices.Add('D', "Delete existing order");
            choices.Add('C', "Confirm Order");
            choices.Add('V', "View Order");

            var input = _inputHandler.AskUserOption(choices);
            switch (input)
            {
                case 'E':
                    EditOrder(order);
                    break;
                case 'D':
                    DeleteExistingOrder(order.Id);
                    break;
                case 'C':
                    ConfirmOrder(order);
                    break;
                case 'B':
                    OnStateChanged(this, nameof(BrowsingState));
                    break;
                case 'V':
                    ViewOrder(order);
                    break;
            }
        }
        else
        {
            choices.Add('A', "Add Order");
            var input = _inputHandler.AskUserOption(choices);
            switch (input)
            {
                case 'A':
                    AddProductsToShoppingCart();
                    break;
                case 'B':
                    OnStateChanged(this, nameof(BrowsingState));
                    break;
            }
        }
    }

    private void ViewOrder(Order order)
    {
        _view.Info($"Pending order [{order.Id}]");
        _view.Info($"Creation date: {order.Date}");
        _view.Info($"{order.Products.Count} Item(s):");
        foreach (var orderProduct in order.Products) {
            _view.Info($"ID [{orderProduct.ProductId}] {orderProduct.Product.Name} - Quantity:  {orderProduct.ProductQuantity}");
        }
    }

    private void AddProductsToShoppingCart()
    {
        var order = new Order(_session.AuthenticatedUser.Email);
        _view.Info($"Type the list of product ID - quantity pairs of items you'd like to purchase. Type [{ConsoleKey.Backspace}] when you are finished.");
        _view.Info($"For example: type '1-2 [{ConsoleKey.Enter}] 43-1 [{ConsoleKey.Backspace}]' to add 2 products with ID 1 and 1 product with ID 43");
        
        var consoleKey = ConsoleKey.Enter;
        while (consoleKey != ConsoleKey.Backspace)
        {
            if (_inputHandler.TryAskUserTextInput(
                    InputFormatValidator.ValidateHyphenSeparatedNumberPair,
                    InputConvertor.ToHyphenSeparatedIntegerPair,
                    out var result,
                    "Enter the product ID and quantity",
                    "Input must be a pair of hyphen-separated numbers"))
            {
                var (productId, productQuantity) = result;
                var isProductAlreadyAdded = false;
                foreach (var product in order.Products)
                {
                    if (product.ProductId == productId && product.ProductQuantity != productQuantity)
                    {
                        isProductAlreadyAdded = true;
                        product.ProductQuantity = productQuantity;
                        _view.Info($"Quantity of product ID [{product.ProductId}] changed to {product.ProductQuantity}");
                    } 
                }

                if (!isProductAlreadyAdded)
                {
                    order.Products.Add(new OrderProduct
                    {
                        ProductId = productId,
                        ProductQuantity = productQuantity,
                    });
                }

                _view.Info($"Added {productQuantity} of product ID [{productId}]");
            }

            consoleKey = _inputHandler.AskUserKeyInput($"Press any key to continue. Press [{ConsoleKey.Backspace}] to finish.");
        }

        if (order.Products.Count == 0)
        {
            _view.Info("No items added to order");
            return;
        }

        var productIdList = order.Products.Select(x => x.ProductId).ToList();
        using var context = new AppDbContext();
        var products = context.Products
            .Where(x => productIdList.Contains(x.Id) && x.InventoryCount > 0)
            .Select(x => new { x.Id, x.InventoryCount })
            .ToDictionary(
                x => x.Id,
                x => x.InventoryCount);

        if (order.Products.Count > 0 && products.Count > 0 && !ValidateOrderProductQuantity(order, products))
        {
            _view.Error("Ordered items are invalid");
            return;
        }

        try
        {
            _view.Info("Saving new order");
            context.Orders.Add(order);
            context.OrderProducts.AddRange(order.Products);
            context.SaveChanges();
        }
        catch
        {
            _view.Error("Failed to process order");
        }
    }

    private bool ValidateOrderProductQuantity(Order order, IReadOnlyDictionary<int, uint> availableProducts)
    {
        var errorMessages = new List<string>();
        IEnumerable<OrderProduct> validProducts = order.Products;

        var invalidProductIds = order.Products
            .Select(x => x.ProductId)
            .Where(x => !availableProducts.ContainsKey(x))
            .ToList();

        if (invalidProductIds.Count > 0)
        {
            validProducts = validProducts.ExceptBy(invalidProductIds, x => x.ProductId);
            errorMessages.Add($"The following product IDs do not exist: {string.Join(", ", invalidProductIds)}");
        }
        
        errorMessages.AddRange(
            validProducts
            .Where(x => x.ProductQuantity > availableProducts[x.ProductId])
            .Select(x => $"Invalid purchase quantity for product with ID [{x.ProductId}] (only {availableProducts[x.ProductId]} are available)")
            .ToList());

        if (errorMessages.Count <= 0)
        {
            return true;
        }
        
        _view.Errors(errorMessages);
        return false;
    }

    private Order? GetExistingOrderOrDefault()
    {
        using var context = new AppDbContext();
        return context.Orders
            .AsNoTracking()
            .Include(x => x.Products)
            .ThenInclude(x => x.Product)
            .Where(x => x.CustomerEmail == _session.AuthenticatedUser.Email && x.Status == OrderStatus.Unconfirmed)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();
    }

    private void DeleteExistingOrder(int orderId)
    {
        using var context = new AppDbContext();
        var order = context.Orders.Find(orderId);
        if (order == null)
        {
            return;
        }
        
        _view.Info($"Erasing order [{order.Id}]");
        context.Orders.Remove(order);
        context.SaveChanges();
    }

    private void EditOrder(Order order)
    {
        using var context = new AppDbContext();
        var consoleKey = ConsoleKey.Enter;
        int? id;
        while (consoleKey != ConsoleKey.Backspace)
        {
            if (_inputHandler.TryAskUserTextInput(
                       x => string.IsNullOrEmpty(x) || int.TryParse(x, out _),
                       x => string.IsNullOrEmpty(x) ? null : int.Parse(x),
                       out id,
                       $"Please type the ID of the product you would like to remove. Press [{ConsoleKey.Enter}] to exit.",
                       "Invalid input. Input must be empty or a valid number"))
            {
                if (id != null)
                {
                    var product = context.OrderProducts.Find(order.Id, id);
                    if (product == null)
                    {
                        _view.Error($"Unable to find product [{id}]");
                    }
                    else
                    {
                        order.Products.Remove(product);
                        context.OrderProducts.Remove(product);
                        context.SaveChanges();
                        _view.Info($"Successfully removed product ID [{id}]");
                    }
                    consoleKey = _inputHandler.AskUserKeyInput($"Press any key to input another product ID. Press [{ConsoleKey.Backspace}] to exit.");
                }
                else
                {
                    consoleKey = ConsoleKey.Backspace;
                }
            }
        }

        AddProductsToShoppingCart();
    }

    private void ConfirmOrder(Order order)
    {
        var deliveryMethod = AskUserForDeliveryMethod(order.Id);
        if (deliveryMethod == null)
        {
            _view.Info("No delivery method selected");
            return;
        }
        
        var transactionMethod = AskUserForPaymentMethod();
        if (transactionMethod == null)
        {
            _view.Info("No transaction method selected");
            return;
        }
        
        // TODO: move to CustomerAccount class per assignment 2
        order.Finalize(deliveryMethod, transactionMethod);
        var success = order.Confirm();
        if (success)
        {
            _view.Info("Order successfully placed");
            return;
        }
        
        _view.Info("An error occurred whilst processing your order");
    }

    private IDeliveryMethod? AskUserForDeliveryMethod(int orderId)
    {
        var choice = _inputHandler.AskUserOption(new Dictionary<char, string>()
            {
                { 'P', "Pick up at store" },
                { 'D', "Postal delivery" },
                { 'E', "Exit" },
            },
            "Please select a delivery method");
        return choice switch
        {
            'P' => ProcessPickupMethod(orderId),
            'D' => ProcessPostalDelivery(orderId),
            'E' => null,
            _ => throw new InvalidOperationException(),
        };
    }

    private IDeliveryMethod? ProcessPostalDelivery(int orderId)
    {
        do
        {
            if (_inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.DigitsOnly),
                    int.Parse,
                    out var streetNumber,
                    $"Enter your address number",
                    "Street number is invalid.") &&
                _inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.StreetName),
                    x => x,
                    out var streetName,
                    $"Enter your address street name",
                    "Street name is invalid.") &&
                _inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.Postal),
                    int.Parse,
                    out var postalCode,
                    $"Enter your postal code",
                    "Postal code is invalid.") &&
                _inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.ApartmentNo),
                    x => string.IsNullOrEmpty(x) ? null : x,
                    out var apartmentNo,
                    $"Enter your apartment number (if applicable)",
                    "Apartment number is invalid."))
            {
                return new PostalDelivery(
                    orderId,
                    streetNumber,
                    streetName,
                    postalCode,
                    apartmentNo);
            }
        } while (_inputHandler.AskUserKeyInput(
                     $"Press any key to continue. Press [{ConsoleKey.Backspace}] to return to delivery options") !=
                 ConsoleKey.Backspace);
        return AskUserForDeliveryMethod(orderId);
    }

    private IDeliveryMethod ProcessPickupMethod(int orderId)
    {
        return new Pickup(orderId);
    }

    private ITransactionMethod? AskUserForPaymentMethod()
    {                                                
        var choice = _inputHandler.AskUserOption(new Dictionary<char, string>()          
            {                                                                            
                { 'P', "Paypal" },                                             
                { 'A', "Cash" },                   
                { 'B', "Bank Transfer" },  
                { 'C', "Credit Card" },             
                { 'E', "Exit"},
            },                                                                           
            "Please select a payment method");
        return choice switch
        {
            'P' => ProcessPaypalTransaction(),
            'A' => ProcessCashTransaction(),
            'B' => ProcessBankTransfer(),  
            'C' => ProcessCardTransaction(),    
            'E' => null,
            _ => throw new InvalidOperationException(),
        };
    }

    private ITransactionMethod? ProcessBankTransfer()
    {
        do
        {
            if (_inputHandler.TryAskUserTextInput(
                    InputFormatValidator.ValidateBsb,
                    x => x,
                    out var bsb,
                    $"Enter your BSB:",
                    "BSB is invalid.") &&
                _inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.DigitsOnly),
                    x => x,
                    out var accountNo,
                    $"Enter your account number:",
                    "Account number is invalid."))
            {
                return new BankTransaction(bsb, accountNo);
            }
        } while (_inputHandler.AskUserKeyInput(
                     $"Press any key to continue. Press [{ConsoleKey.Backspace}] to return to payment options") ==
                 ConsoleKey.Backspace);
        
        return AskUserForPaymentMethod();
    }

    private ITransactionMethod? ProcessCardTransaction()
    {
        string? cardNo;
        while (!_inputHandler.TryAskUserTextInput(
                   x => string.IsNullOrEmpty(x) || InputFormatValidator.ValidateCardNumber(x),
                   x => x,
                   out cardNo,
                   $"Enter your card number in the xxxx-xxxx-xxxx-xxxx format. Press [{ConsoleKey.Enter}] if you do not want one",
                   "Invalid input. Input must be empty or a valid card number"))
        { }

        if (string.IsNullOrEmpty(cardNo))
        {
            _view.Info("No card number entered.");
            return null;
        }
        
        string? cvc;
        while (!_inputHandler.TryAskUserTextInput(
                   x => string.IsNullOrEmpty(x) || Regex.IsMatch(x, RegexPatterns.Cvc),
                   x => x,
                   out cvc,
                   $"Enter your card CVC. Press [{ConsoleKey.Enter}] to exit",
                   "Invalid input. Input must be empty or a valid CVC"))
        { }

        if (string.IsNullOrEmpty(cvc))
        {
            _view.Info("No card CVC entered.");
            return null;
        }
        
        // TODO: make the date format (mm/yyyy)
        DateOnly? expiryDate;
        while (!_inputHandler.TryAskUserTextInput(
                   x => string.IsNullOrEmpty(x) || InputFormatValidator.ValidateCardExpiryDate(x),
                   x => string.IsNullOrEmpty(x) ? null : DateOnly.FromDateTime(DateTime.Parse(x)),
                   out expiryDate,
                   $"Enter your card expiry date. Press [{ConsoleKey.Enter}] to exit.",
                   "Invalid input. Input must be empty or a valid card expiry date"))
        { }

        if (expiryDate == null)
        {
            _view.Info("No card number entered.");
            return null;
        }
        
        return new CreditCardTransaction(cardNo, cvc, expiryDate.Value);
    }

    private ITransactionMethod? ProcessCashTransaction()
    {
        return new CashTransaction();
    }

    private ITransactionMethod? ProcessPaypalTransaction()
    {
        do
        {
            if (_inputHandler.TryAskUserTextInput(
                    x => Regex.IsMatch(x, RegexPatterns.Email) || 
                         Regex.IsMatch(x, RegexPatterns.Phone),
                    x => x,
                    out var paypal,
                    $"Enter your PayPal email or phone number:",
                    "PayPal username is invalid."))
            {
                return new PaypalTransaction(paypal);
            }
            
        }
        while (_inputHandler.AskUserKeyInput(
                   $"Press any key to continue. Press [{ConsoleKey.Backspace}] to return to payment options") != ConsoleKey.Backspace);
        
        return AskUserForPaymentMethod();
    }
}
