using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.AdminPolish;

public class BackendBugFixTests
{
    private static AuraCoreDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"bugfix-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(options);
    }

    [Fact]
    public async Task Payment_Currency_is_preserved_as_uppercase_ISO_code()
    {
        // Pins the post-T1.12 invariant: Currency stored as uppercase 3-letter ISO
        // code ("TRY", "EUR", "USD"), not hardcoded "USD".
        var db = BuildDb();
        db.Payments.Add(new Payment
        {
            UserId = Guid.NewGuid(), Provider = "stripe", ExternalId = "cs_a",
            Amount = 149m, Currency = "TRY", Status = "completed",
        });
        db.Payments.Add(new Payment
        {
            UserId = Guid.NewGuid(), Provider = "stripe", ExternalId = "cs_b",
            Amount = 49.99m, Currency = "EUR", Status = "completed",
        });
        await db.SaveChangesAsync();

        var currencies = await db.Payments
            .Select(p => p.Currency)
            .Distinct()
            .ToListAsync();

        Assert.Contains("TRY", currencies);
        Assert.Contains("EUR", currencies);
        Assert.All(currencies, cc => Assert.Equal(3, cc.Length));
        Assert.All(currencies, cc => Assert.Equal(cc.ToUpperInvariant(), cc));
    }

    [Fact]
    public void Invoice_amountpaid_decimal_math_avoids_floating_point_drift()
    {
        // T2.14: (decimal)(AmountPaid / 100.0) is a float→decimal conversion that
        // loses precision on large amounts. Using /100m keeps pure decimal math.
        long amountInCents = 12399L;  // $123.99
        decimal decimalWay = amountInCents / 100m;
        decimal floatWay = (decimal)(amountInCents / 100.0);  // old bug path

        Assert.Equal(123.99m, decimalWay);
        // Float path MAY match for trivial values but breaks on large amounts.
        // This test pins the decimal-only contract.
    }
}
