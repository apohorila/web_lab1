namespace server.Features;


public interface ISpendProvider
{
    Task<decimal> GetLifetimeSpendAsync(int userId);
}
