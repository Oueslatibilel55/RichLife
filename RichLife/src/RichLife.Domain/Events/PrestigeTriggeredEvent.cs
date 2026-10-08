using RichLife.Domain.Common;
using RichLife.Domain.Enums;

namespace RichLife.Domain.Events;

public record PrestigeTriggeredEvent(Guid CompanyId, PrestigeLevel NewLevel, decimal Multiplier) : IDomainEvent;
