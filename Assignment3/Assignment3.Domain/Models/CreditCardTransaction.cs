namespace Assignment3.Domain.Models;

public class CreditCardTransaction : ITransactionMethod
{
    private readonly string _accountNo;
    private readonly string _cvc;
    private readonly DateOnly _expiryDate;

    public CreditCardTransaction(string accountNo, string cvc, DateOnly expiryDate)
    {
        _accountNo = accountNo;
        _cvc = cvc;
        _expiryDate = expiryDate;
    }
    public Receipt Execute(Transaction transaction, Order order)
    {
        var receipt = new Receipt
        {
            Order = order,
            Transaction = transaction
        };

        return receipt;
    }
}