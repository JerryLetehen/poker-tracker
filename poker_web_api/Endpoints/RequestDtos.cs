namespace poker_web_api.Endpoints;

public record CreateGameRequest(string? GameType, decimal BuyInAmount);
public record BuyInRequest(int PlayerId, decimal Amount);
public record CashOutRequest(int PlayerId, decimal Amount);
public record CreatePlayerRequest(string Name);