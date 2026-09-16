using Nop.Core.Domain.Orders;
using Nop.Services.Events;

namespace Paytness.Reference;

public sealed class ReferenceOrderPaidConsumer(ReferenceState state) : IConsumer<OrderPaidEvent>
{
    public Task HandleEventAsync(OrderPaidEvent eventMessage)
    {
        ArgumentNullException.ThrowIfNull(eventMessage);
        state.Get(eventMessage.Order.OrderGuid).RecordOrderPaid();
        return Task.CompletedTask;
    }
}
