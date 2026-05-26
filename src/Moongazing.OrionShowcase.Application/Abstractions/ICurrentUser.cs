namespace Moongazing.OrionShowcase.Application.Abstractions;

public interface ICurrentUser
{
    string Id { get; }
    string Username { get; }
    bool IsAuthenticated { get; }
}
