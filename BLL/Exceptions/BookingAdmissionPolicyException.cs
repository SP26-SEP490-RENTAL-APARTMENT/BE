using Common.DTOs;

namespace BLL.Exceptions;

public class BookingAdmissionPolicyException : Exception
{
    public BookingAdmissionEvaluationDto Evaluation { get; }

    public BookingAdmissionPolicyException(BookingAdmissionEvaluationDto evaluation)
        : base(evaluation.BlockReason ?? "Booking admission is not allowed.")
    {
        Evaluation = evaluation;
    }
}