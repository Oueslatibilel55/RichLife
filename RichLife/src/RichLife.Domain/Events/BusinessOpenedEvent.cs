using RichLife.Domain.Common;

namespace RichLife.Domain.Events;

public record BusinessOpenedEvent(Guid CompanyId, Guid BusinessId, string BusinessName) : IDomainEvent;
