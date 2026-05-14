using Cashflow.Consolidation.IntegrationTests.Infrastructure;
using Cashflow.Contracts.V1;
using Cashflow.TestSupport;
using MassTransit;

namespace Cashflow.Consolidation.IntegrationTests.Tests;

public sealed class ProjectionConsumerTests : ConsolidationWorkerTestBase
{
    public ProjectionConsumerTests(CashflowFixture fixture) : base(fixture) { }

    private static readonly DateOnly TestDate = new(2026, 5, 13);

    // IT-04: Worker publishes EntryRegisteredV1 and the consumer projects it
    // into the daily_balances collection. We assert the document exists with
    // the expected credit and bucket count.
    [Fact]
    public async Task EntryRegisteredV1_IsProjectedIntoDailyBalances()
    {
        var merchantId = Guid.NewGuid();
        var evt = new EntryRegisteredV1(
            EventId: Guid.NewGuid(),
            OccurredOn: DateTimeOffset.UtcNow,
            MerchantId: merchantId,
            EntryId: Guid.NewGuid(),
            Type: "Credit",
            Amount: 250m,
            Currency: "BRL",
            EntryDate: TestDate,
            Category: "Sales");

        await Bus.Publish(evt);

        var doc = await ProjectionWaiter.WaitForAsync(
            Mongo, merchantId, TestDate,
            predicate: d => d.TotalCredits == 250m && d.EntriesCount == 1);

        doc.TotalCredits.Should().Be(250m);
        doc.TotalDebits.Should().Be(0m);
        doc.EntriesCount.Should().Be(1);
        doc.ByCategory.Should().ContainSingle(b => b.Category == "Sales" && b.Credit == 250m);
    }

    // IT-05: A duplicate event (same EventId) does not duplicate the projection.
    // We publish twice; the second hits either the fast-path (processed_events
    // sees the id) or the atomic guard on the document — either way, totals are
    // unchanged from the single-application baseline.
    [Fact]
    public async Task EntryRegisteredV1_DuplicateEventId_DoesNotDoubleProjection()
    {
        var merchantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var evt = new EntryRegisteredV1(
            EventId: eventId,
            OccurredOn: DateTimeOffset.UtcNow,
            MerchantId: merchantId,
            EntryId: Guid.NewGuid(),
            Type: "Credit",
            Amount: 100m,
            Currency: "BRL",
            EntryDate: TestDate,
            Category: "Sales");

        await Bus.Publish(evt);
        await ProjectionWaiter.WaitForAsync(
            Mongo, merchantId, TestDate, d => d.TotalCredits == 100m && d.EntriesCount == 1);

        // Re-publish the same event — the broker treats it as a new delivery, the
        // consumer's idempotency layer is what must keep the projection stable.
        await Bus.Publish(evt);

        // Give the redelivery enough room to land. Then assert the state is still
        // the single-application result. We poll briefly to allow the consumer to
        // ack the second message; the assertion holds even if it never runs.
        await Task.Delay(TimeSpan.FromSeconds(2));

        var doc = await ProjectionWaiter.WaitForAsync(
            Mongo, merchantId, TestDate,
            predicate: d => d.TotalCredits == 100m && d.EntriesCount == 1,
            timeout: TimeSpan.FromSeconds(5));

        doc.TotalCredits.Should().Be(100m, "duplicate event must not double-credit the merchant");
        doc.EntriesCount.Should().Be(1);
    }

    // IT-06: A reversal event decrements the balance. After Register(150) +
    // Reverse(150), TotalCredits must net to 0 and EntriesCount must be 0.
    [Fact]
    public async Task EntryReversedV1_DecrementsDailyBalance()
    {
        var merchantId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        await Bus.Publish(new EntryRegisteredV1(
            EventId: Guid.NewGuid(),
            OccurredOn: DateTimeOffset.UtcNow,
            MerchantId: merchantId,
            EntryId: entryId,
            Type: "Credit",
            Amount: 150m,
            Currency: "BRL",
            EntryDate: TestDate,
            Category: "Sales"));

        await ProjectionWaiter.WaitForAsync(
            Mongo, merchantId, TestDate, d => d.TotalCredits == 150m && d.EntriesCount == 1);

        await Bus.Publish(new EntryReversedV1(
            EventId: Guid.NewGuid(),
            OccurredOn: DateTimeOffset.UtcNow,
            MerchantId: merchantId,
            EntryId: entryId,
            Type: "Credit",
            Amount: 150m,
            Currency: "BRL",
            EntryDate: TestDate,
            Category: "Sales",
            Reason: "test reversal"));

        var doc = await ProjectionWaiter.WaitForAsync(
            Mongo, merchantId, TestDate,
            predicate: d => d.TotalCredits == 0m && d.EntriesCount == 0);

        doc.TotalCredits.Should().Be(0m);
        doc.EntriesCount.Should().Be(0);
    }
}
