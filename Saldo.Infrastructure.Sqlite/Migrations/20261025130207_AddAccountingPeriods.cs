using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saldo.Infrastructure.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDateExclusive = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedIncomeTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ClosedExpenseTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    ClosedTransactionCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ClosedDataHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    HashVersion = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                    table.CheckConstraint("CK_AccountingPeriods_CalendarMonth", "date(StartDate, 'start of month') IS NOT NULL AND StartDate = date(StartDate, 'start of month') AND date(StartDate, '+1 month') IS NOT NULL AND EndDateExclusive = date(StartDate, '+1 month') AND StartDate < EndDateExclusive");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_StartDate",
                table: "AccountingPeriods",
                column: "StartDate",
                unique: true);

            // Preserve historical transactions and leave unoccupied months absent.
            migrationBuilder.Sql("""
                INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
                SELECT DISTINCT date("Date", 'start of month'),
                                date("Date", 'start of month', '+1 month'), 0
                FROM Transactions;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingPeriods");
        }
    }
}
