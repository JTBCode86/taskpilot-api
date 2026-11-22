using System;

namespace TaskPilot.Domain.Exceptions
{
    public sealed class DuplicateUserException : Exception
    {
        public DuplicateUserException() : base("Já existe uma conta com este e-mail.") { }
    }
}
