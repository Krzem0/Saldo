namespace Saldo.Application.Errors;

public static class ErrorCodes
{
    public static class Transaction
    {
        public const string IdMustBePositive = "Transaction.IdMustBePositive";
        public const string AmountMustBePositive = "Transaction.AmountMustBePositive";
        public const string CategoryRequired = "Transaction.CategoryRequired";
        public const string PayerRequired = "Transaction.PayerRequired";
        public const string CounterpartyRequired = "Transaction.CounterpartyRequired";
        public const string NotFound = "Transaction.NotFound";
    }
}
