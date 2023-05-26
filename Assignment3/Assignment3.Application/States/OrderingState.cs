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
            choices.Add('D', "Delete existing order and make a new one");
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
                    AddProductsToShoppingCart();
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
        _view.Info($"Items:");
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
            .Select(selector: x => $"Invalid purchase quantity for product with ID [{x.ProductId}] (only {availableProducts[x.ProductId]} are available)")
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
        if (!_inputHandler.TryAskUserTextInput(
                InputFormatValidator.ValidateCommaSeparatedNumberList,
                InputConvertor.ToCommaSeparatedIntegerList,
                out var productIdsToRemove,
                $"Enter a comma separated list of IDs of products to be removed. Press [{ConsoleKey.Enter}] if you do not wish to remove any product"))
        {
            _view.Error("Invalid input. Please type in a list of comma-separated product IDs or press [Enter]");
            return;
        }

        var orderProductIds = order.Products.Select(x => x.ProductId).ToHashSet();
        var invalidProductIdsToRemove = productIdsToRemove.Except(productIdsToRemove.Intersect(orderProductIds)).ToList();
        if (invalidProductIdsToRemove.Count > 0)
        {
            _view.Error($"The following product IDs cannot be removed because they are not in the order: {string.Join(",", invalidProductIdsToRemove)}");
            return;
        }

        _view.Info($"Type the list of product ID - quantity pairs of items you'd like to update or add to order. Type [{ConsoleKey.Escape}] when you are finished.");
        _view.Info($"For example: type '1-2 [{ConsoleKey.Enter}] 43-1 [{ConsoleKey.Escape}]' to add 2 products with ID 1 and 1 product with ID 43");

        var productIdQuantityPairs = new Dictionary<int, int>();
        var consoleKey = ConsoleKey.Enter;
        while (consoleKey != ConsoleKey.Escape)
        {
            if (_inputHandler.TryAskUserTextInput(
                    InputFormatValidator.ValidateHyphenSeparatedNumberPair,
                    InputConvertor.ToHyphenSeparatedIntegerPair, 
                    out var result,
                    "Enter the product ID and new quantity",
                    "Input must be a pair of hyphen-separated numbers"))
            {
                var (productId, quantity) = result;
                productIdQuantityPairs.Add(productId, quantity);
            }

            consoleKey = _inputHandler.AskUserKeyInput($"Press any key to continue. Press [{ConsoleKey.Escape}] to quit.");
        }

        var productIdsToAdd = productIdQuantityPairs
            .Where(x => !orderProductIds.Contains(x.Key))
            .ToDictionary(x => x.Key, x=>x.Value);

        foreach (var (productId, quantity) in productIdsToAdd)
        {
            _view.Info($"Adding [{quantity}] of new product with ID [{productId}]");
        }
        
        foreach (var orderProduct in order.Products)
        {
            if (productIdQuantityPairs.TryGetValue(orderProduct.ProductId, out var updatedQuantity))
            {
                orderProduct.ProductQuantity = updatedQuantity;
            }
        }

        using var context = new AppDbContext();
        var productIdList = order.Products.Select(x => x.ProductId).ToList();
        var products = context.Products
            .Where(x => productIdList.Contains(x.Id) && x.InventoryCount > 0)
            .Select(x => new { x.Id, x.InventoryCount })
            .ToDictionary(
                x => x.Id,
                x => x.InventoryCount);

        if (!ValidateOrderProductQuantity(order, products))
        {
            _view.Error("Ordered items are invalid");
            return;
        }

        try
        {
            context.Orders.Update(order);
            context.OrderProducts.UpdateRange(order.Products);
            context.SaveChanges();
        }
        catch
        {
            _view.Error("Failed to process order");
        }
    }

    private void ConfirmOrder(Order order)
    {
        var deliveryMethod = AskUserForDeliveryMethod(order.Id);
        var transactionMethod = AskUserForPaymentMethod(order.Id);
        // TODO: move to CustomerAccount class per assignment 2
        var invoice = order.Prepare(deliveryMethod, transactionMethod);
        invoice.EmailToCustomer();
        var success = invoice.MakePayment();
        if (success)
        {
            ConsoleInputHandler.PrintInfo("Order successfully placed");
            order.StartDelivery();
            return;
        }
        
        ConsoleInputHandler.PrintError("An error occurred whilst processing your order");
    }

    private IDeliveryMethod AskUserForDeliveryMethod(int orderId)
    {
        var choice = ConsoleInputHandler.AskUserOption(new Dictionary<char, string>()
            {
                { 'P', "Pick up at store" },
                { 'D', "Postal delivery" },
            },
            "Please select a delivery method");
        return choice switch
        {
            'P' => ProcessPickupMethod(orderId),
            'D' => ProcessPostalDelivery(orderId),
            _ => throw new InvalidOperationException(),
        };
    }

    private IDeliveryMethod ProcessPostalDelivery(int orderId)
    { 
        // TODO(HUY): VALIDATE INPUT
        var streetNumber = ConsoleInputHandler.AskUserTextInput("Enter your address number");
        var streetName =  ConsoleInputHandler.AskUserTextInput("Enter your address street name");
        var postalCode = ConsoleInputHandler.AskUserTextInput("Enter your postcode");
        var apartmentNumber = ConsoleInputHandler.AskUserTextInput("Enter your apartment number (if applicable)");

        return new PostalDelivery(
            orderId, 
            int.Parse(streetNumber), 
            streetName, 
             int.Parse(postalCode), 
            apartmentNumber);
    }

    private IDeliveryMethod ProcessPickupMethod(int orderId)
    {
        return new Pickup(orderId);
    }

    private ITransactionMethod AskUserForPaymentMethod(int orderId)
    {                                                
        var choice = ConsoleInputHandler.AskUserOption(new Dictionary<char, string>()          
            {                                                                            
                { 'P', "Paypal" },                                             
                { 'A', "Cash" },                   
                { 'B', "Bank Transfer" },  
                { 'C', "Credit Card" },                                              
            },                                                                           
            "Please select a delivery method");
        return choice switch
        {
            'P' => ProcessPaypalTransaction(orderId),
            'A' => ProcessCashTransaction(orderId),
            'B' => ProcessBankTransfer(orderId),  
            'C' => ProcessCardTransaction(orderId),    
            _ => throw new InvalidOperationException(),
        };
    }

    private ITransactionMethod ProcessBankTransfer(int orderId)
    {
        // TODO(HUY): VALIDATE INPUT
        var bsb = ConsoleInputHandler.AskUserTextInput("Enter your BSB");     
        var accountNo =  ConsoleInputHandler.AskUserTextInput("Enter your account number");
        return new BankTransaction(bsb, accountNo);
    }

    private ITransactionMethod ProcessCardTransaction(int orderId)
    {
        // TODO(HUY): VALIDATE INPUT
        var cardNo = ConsoleInputHandler.AskUserTextInput("Enter your card number");
        var cvc = ConsoleInputHandler.AskUserTextInput("Enter your card CVC");
        var expiryDate = ConsoleInputHandler.AskUserTextInput("Enter your card expiry date");
        return new CreditCardTransaction(cardNo, cvc, DateOnly.FromDateTime(DateTime.Parse(expiryDate))); 
    }

    private ITransactionMethod ProcessCashTransaction(int orderId)
    {
        throw new NotImplementedException();
    }

    private ITransactionMethod ProcessPaypalTransaction(int orderId)
    {
        throw new NotImplementedException();
    }
}
