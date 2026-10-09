using RichLife.Domain.Entities;

namespace RichLife.Domain.Banking;

/// <summary>What one collection run took (it may cover several installments).</summary>
public sealed record LoanCollection(Loan Loan, decimal Paid, decimal Penalty);
