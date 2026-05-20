using System;

namespace BLL.Exceptions
{
    public class OutstandingUnpaidBookingException : Exception
    {
        public OutstandingUnpaidBookingException()
        {
        }

        public OutstandingUnpaidBookingException(string message) : base(message)
        {
        }

        public OutstandingUnpaidBookingException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
