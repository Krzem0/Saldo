namespace Saldo.Application.Errors;

public static class ErrorCodes
{
    public static class Transaction
    {
        public const string IdMustBePositive = "Transaction.IdMustBePositive";
        public const string AmountMustBePositive = "Transaction.AmountMustBePositive";
        public const string CategoryRequired = "Transaction.CategoryRequired";
        public const string PayerInvalid = "Transaction.PayerInvalid";
        public const string CounterpartyInvalid = "Transaction.CounterpartyInvalid";
        public const string LocationInvalid = "Transaction.LocationInvalid";
        public const string DescriptionRequired = "Transaction.DescriptionRequired";
        public const string DescriptionTooLong = "Transaction.DescriptionTooLong";
        public const string DateRequired = "Transaction.DateRequired";
        public const string TypeInvalid = "Transaction.TypeInvalid";
        public const string NotFound = "Transaction.NotFound";
    }
}
