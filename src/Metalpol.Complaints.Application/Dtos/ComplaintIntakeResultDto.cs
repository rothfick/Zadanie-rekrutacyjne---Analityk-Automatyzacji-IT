using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Dtos;

public sealed record ComplaintIntakeResultDto(
    ComplaintId ComplaintId,
    ComplaintStatus Status);
